// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

package main

import (
	"context"
	"crypto/tls"
	"encoding/json"
	"errors"
	"flag"
	"fmt"
	"io"
	"math"
	"net"
	neturl "net/url"
	"os"
	"sort"
	"strings"
	"sync"
	"time"

	quic "github.com/quic-go/quic-go"
)

const (
	defaultALPN        = "plab-raw-quic"
	defaultOpTimeout   = 15 * time.Second
	resultTool         = "quic-go-raw-load"
	resultCategory     = "managed-lab"
	loopbackBypassNote = "loopback certificate bypass enabled for local QUIC target"
)

type config struct {
	TargetURL            string
	TargetAddr           string
	TargetHost           string
	ALPN                 string
	Behavior             string
	StreamType           string
	PayloadDirection     string
	OpenPattern          string
	PayloadSizeBytes     int
	Connections          int
	StreamsPerConnection int
	Duration             time.Duration
	Warmup               time.Duration
}

type resultDocument struct {
	Tool             string         `json:"tool"`
	Category         string         `json:"category"`
	Protocol         string         `json:"protocol,omitempty"`
	Target           string         `json:"target,omitempty"`
	Behavior         string         `json:"behavior,omitempty"`
	StreamType       string         `json:"streamType,omitempty"`
	PayloadDirection string         `json:"payloadDirection,omitempty"`
	OpenPattern      string         `json:"openPattern,omitempty"`
	ALPN             string         `json:"alpn,omitempty"`
	Metrics          map[string]any `json:"metrics"`
	Warnings         []string       `json:"warnings"`
	Notes            []string       `json:"notes"`
	Errors           []string       `json:"errors"`
}

type runMetrics struct {
	mu                   sync.Mutex
	totalRequests        int64
	successfulRequests   int64
	failedRequests       int64
	timeoutRequests      int64
	bytesReceived        int64
	bytesSent            int64
	requestLatenciesMs   []float64
	connectLatenciesMs   []float64
	firstByteLatenciesMs []float64
}

type latencyStats struct {
	Min  float64
	Mean float64
	P50  float64
	P75  float64
	P90  float64
	P95  float64
	P99  float64
	Max  float64
}

func main() {
	cfg, err := parseConfig(os.Args[1:])
	if err != nil {
		if emitErr := emitDocument(newErrorDocument(err.Error())); emitErr != nil {
			fmt.Fprintln(os.Stderr, emitErr)
		}
		fmt.Fprintln(os.Stderr, err)
		os.Exit(2)
	}

	doc, err := run(cfg)
	if err != nil {
		if emitErr := emitDocument(doc.withError(err.Error())); emitErr != nil {
			fmt.Fprintln(os.Stderr, emitErr)
		}
		fmt.Fprintln(os.Stderr, err)
		os.Exit(1)
	}

	if err := emitDocument(doc); err != nil {
		fmt.Fprintln(os.Stderr, err)
		os.Exit(1)
	}
}

func parseConfig(args []string) (config, error) {
	fs := flag.NewFlagSet(resultTool, flag.ContinueOnError)
	fs.SetOutput(io.Discard)

	cfg := config{
		ALPN:                 defaultALPN,
		Connections:          1,
		StreamsPerConnection: 1,
		Duration:             30 * time.Second,
	}

	durationArg := fs.String("duration", "30s", "measurement duration")
	warmupArg := fs.String("warmup", "0s", "warmup duration")

	fs.StringVar(&cfg.ALPN, "alpn", defaultALPN, "QUIC ALPN")
	fs.StringVar(&cfg.Behavior, "behavior", "", "scenario behavior")
	fs.StringVar(&cfg.StreamType, "stream-type", "", "stream type")
	fs.StringVar(&cfg.PayloadDirection, "payload-direction", "", "payload direction")
	fs.StringVar(&cfg.OpenPattern, "open-pattern", "", "stream open pattern")
	fs.IntVar(&cfg.PayloadSizeBytes, "payload-size-bytes", 0, "payload size in bytes")
	fs.IntVar(&cfg.Connections, "connections", 1, "concurrent connections")
	fs.IntVar(&cfg.StreamsPerConnection, "streams-per-connection", 1, "streams per connection")

	if err := fs.Parse(args); err != nil {
		return config{}, err
	}

	if fs.NArg() != 1 {
		return config{}, errors.New("target QUIC URL is required as the final argument")
	}

	targetURL, err := neturl.Parse(fs.Arg(0))
	if err != nil {
		return config{}, fmt.Errorf("invalid target URL %q: %w", fs.Arg(0), err)
	}
	if !strings.EqualFold(targetURL.Scheme, "quic") {
		return config{}, fmt.Errorf("target URL must use quic://, got %q", targetURL.String())
	}

	host := targetURL.Hostname()
	port := targetURL.Port()
	if host == "" || port == "" {
		return config{}, fmt.Errorf("target URL must include a host and port, got %q", targetURL.String())
	}

	duration, err := time.ParseDuration(*durationArg)
	if err != nil {
		return config{}, fmt.Errorf("invalid duration %q: %w", *durationArg, err)
	}

	warmup, err := time.ParseDuration(*warmupArg)
	if err != nil {
		return config{}, fmt.Errorf("invalid warmup %q: %w", *warmupArg, err)
	}

	if cfg.Connections <= 0 {
		return config{}, errors.New("connections must be greater than zero")
	}
	if cfg.StreamsPerConnection < 0 {
		return config{}, errors.New("streams-per-connection cannot be negative")
	}
	if cfg.PayloadSizeBytes < 0 {
		return config{}, errors.New("payload-size-bytes cannot be negative")
	}

	cfg.TargetURL = targetURL.String()
	cfg.TargetAddr = net.JoinHostPort(host, port)
	cfg.TargetHost = host
	cfg.Duration = duration
	cfg.Warmup = warmup
	cfg.ALPN = strings.TrimSpace(cfg.ALPN)
	if cfg.ALPN == "" {
		cfg.ALPN = defaultALPN
	}

	return cfg, nil
}

func run(cfg config) (resultDocument, error) {
	doc := newResultDocument(cfg)
	resolvedBehavior := resolveBehavior(cfg)
	doc.Behavior = resolvedBehavior
	if !strings.EqualFold(strings.TrimSpace(cfg.Behavior), resolvedBehavior) && strings.TrimSpace(cfg.Behavior) != "" {
		doc.Notes = append(doc.Notes, fmt.Sprintf("resolved requested behavior %q to %q", cfg.Behavior, resolvedBehavior))
	}

	if cfg.Warmup > 0 {
		executePhase(cfg, resolvedBehavior, time.Now().Add(cfg.Warmup), nil)
		doc.Notes = append(doc.Notes, fmt.Sprintf("warmup phase completed for %s", cfg.Warmup))
	}

	metrics := &runMetrics{}
	measureStart := time.Now()
	executePhase(cfg, resolvedBehavior, measureStart.Add(cfg.Duration), metrics)
	elapsed := time.Since(measureStart)
	if elapsed <= 0 {
		elapsed = time.Nanosecond
	}

	doc.Metrics = metrics.snapshot(elapsed)
	metrics.mu.Lock()
	totalRequests := metrics.totalRequests
	successfulRequests := metrics.successfulRequests
	failedRequests := metrics.failedRequests
	timeoutRequests := metrics.timeoutRequests
	metrics.mu.Unlock()

	if failedRequests > 0 || timeoutRequests > 0 {
		doc.Warnings = append(doc.Warnings, fmt.Sprintf("%d requests failed or timed out during the measurement window", failedRequests+timeoutRequests))
	}

	if totalRequests == 0 || successfulRequests == 0 {
		message := "no successful raw QUIC benchmark transactions were recorded"
		doc.Errors = append(doc.Errors, message)
		return doc, errors.New(message)
	}
	return doc, nil
}

func resolveBehavior(cfg config) string {
	behavior := strings.ToLower(strings.TrimSpace(cfg.Behavior))
	if behavior != "" {
		return canonicalBehavior(behavior)
	}

	switch {
	case strings.EqualFold(cfg.StreamType, "none"):
		return "handshake-cold"
	case strings.EqualFold(cfg.OpenPattern, "churn"):
		return "connection-churn"
	case strings.EqualFold(cfg.OpenPattern, "concurrent") && cfg.StreamsPerConnection > 1:
		if strings.EqualFold(cfg.PayloadDirection, "bidirectional") {
			return "duplex-streams"
		}
		return "multiplex"
	default:
		return "stream-throughput"
	}
}

func executePhase(cfg config, behavior string, until time.Time, metrics *runMetrics) {
	switch canonicalBehavior(behavior) {
	case "handshake-cold":
		runWorkers(cfg.Connections, func() {
			runHandshakeWorker(cfg, until, metrics)
		})
	case "connection-churn":
		runWorkers(cfg.Connections, func() {
			runConnectionChurnWorker(cfg, until, metrics)
		})
	default:
		concurrentStreams := strings.EqualFold(canonicalBehavior(behavior), "multiplex") ||
			strings.EqualFold(canonicalBehavior(behavior), "duplex-streams") ||
			strings.EqualFold(cfg.OpenPattern, "concurrent")
		runWorkers(cfg.Connections, func() {
			runReusedConnectionWorker(cfg, until, metrics, concurrentStreams)
		})
	}
}

func runWorkers(count int, worker func()) {
	var wg sync.WaitGroup
	for i := 0; i < count; i++ {
		wg.Add(1)
		go func() {
			defer wg.Done()
			worker()
		}()
	}
	wg.Wait()
}

func runHandshakeWorker(cfg config, until time.Time, metrics *runMetrics) {
	for time.Now().Before(until) {
		conn, connectLatency, err := dialConnection(cfg)
		if err != nil {
			if metrics != nil {
				metrics.recordRequest(false, isTimeoutErr(err), connectLatency, 0, 0, 0)
			}
			continue
		}

		if metrics != nil {
			metrics.recordConnect(connectLatency)
			metrics.recordRequest(true, false, connectLatency, 0, 0, 0)
		}

		closeConn(conn)
	}
}

func runConnectionChurnWorker(cfg config, until time.Time, metrics *runMetrics) {
	batchSize := cfg.StreamsPerConnection
	if batchSize < 1 {
		return
	}

	for time.Now().Before(until) {
		for i := 0; i < batchSize && time.Now().Before(until); i++ {
			success, connectLatency, requestLatency, bytesSent, bytesReceived, ttfb, timeout := doFreshConnectionRequest(cfg)
			if metrics != nil {
				if success {
					metrics.recordConnect(connectLatency)
				}
				metrics.recordRequest(success, timeout, requestLatency, bytesSent, bytesReceived, ttfb)
			}
		}
	}
}

func runReusedConnectionWorker(cfg config, until time.Time, metrics *runMetrics, concurrentStreams bool) {
	batchSize := cfg.StreamsPerConnection
	if batchSize < 1 {
		return
	}

	for time.Now().Before(until) {
		conn, connectLatency, err := dialConnection(cfg)
		if err != nil {
			if metrics != nil {
				metrics.recordRequest(false, isTimeoutErr(err), connectLatency, 0, 0, 0)
			}
			continue
		}

		if metrics != nil {
			metrics.recordConnect(connectLatency)
		}

		ok := executeBatch(cfg, conn, until, metrics, batchSize, concurrentStreams)
		closeConn(conn)
		if !ok {
			continue
		}
	}
}

func executeBatch(cfg config, conn *quic.Conn, until time.Time, metrics *runMetrics, batchSize int, concurrent bool) bool {
	if concurrent {
		return executeConcurrentBatch(cfg, conn, until, metrics, batchSize)
	}
	return executeSequentialBatch(cfg, conn, until, metrics, batchSize)
}

func executeSequentialBatch(cfg config, conn *quic.Conn, until time.Time, metrics *runMetrics, batchSize int) bool {
	payload := buildPayload(cfg.PayloadSizeBytes)
	for i := 0; i < batchSize; i++ {
		if time.Now().After(until) {
			return true
		}

		success, requestLatency, bytesSent, bytesReceived, ttfb, timeout := doStreamRequest(cfg, conn, payload)
		if metrics != nil {
			metrics.recordRequest(success, timeout, requestLatency, bytesSent, bytesReceived, ttfb)
		}
		if !success {
			return false
		}
	}

	return true
}

func executeConcurrentBatch(cfg config, conn *quic.Conn, until time.Time, metrics *runMetrics, batchSize int) bool {
	payload := buildPayload(cfg.PayloadSizeBytes)
	var wg sync.WaitGroup
	failures := make(chan struct{}, batchSize)

	for i := 0; i < batchSize; i++ {
		if time.Now().After(until) {
			break
		}

		wg.Add(1)
		go func() {
			defer wg.Done()
			success, requestLatency, bytesSent, bytesReceived, ttfb, timeout := doStreamRequest(cfg, conn, payload)
			if metrics != nil {
				metrics.recordRequest(success, timeout, requestLatency, bytesSent, bytesReceived, ttfb)
			}
			if !success {
				select {
				case failures <- struct{}{}:
				default:
				}
			}
		}()
	}

	wg.Wait()
	return len(failures) == 0
}

func doStreamRequest(cfg config, conn *quic.Conn, payload []byte) (bool, time.Duration, int64, int64, time.Duration, bool) {
	start := time.Now()
	opCtx, cancel := context.WithTimeout(context.Background(), defaultOpTimeout)
	defer cancel()

	stream, err := conn.OpenStreamSync(opCtx)
	if err != nil {
		return false, time.Since(start), 0, 0, 0, isTimeoutErr(err)
	}

	_ = stream.SetDeadline(time.Now().Add(defaultOpTimeout))

	bytesSent, err := writeAll(stream, payload)
	if err != nil {
		_ = stream.Close()
		return false, time.Since(start), bytesSent, 0, 0, isTimeoutErr(err)
	}

	if !expectsResponse(cfg.PayloadDirection) {
		if err := stream.Close(); err != nil {
			return false, time.Since(start), bytesSent, 0, 0, isTimeoutErr(err)
		}
		return true, time.Since(start), bytesSent, 0, 0, false
	}

	if err := stream.Close(); err != nil {
		return false, time.Since(start), bytesSent, 0, 0, isTimeoutErr(err)
	}

	bytesReceived, ttfb, err := readEcho(stream, len(payload))
	if err != nil {
		return false, time.Since(start), bytesSent, bytesReceived, ttfb, isTimeoutErr(err)
	}

	return true, time.Since(start), bytesSent, bytesReceived, ttfb, false
}

func doFreshConnectionRequest(cfg config) (bool, time.Duration, time.Duration, int64, int64, time.Duration, bool) {
	start := time.Now()
	conn, connectLatency, err := dialConnection(cfg)
	if err != nil {
		return false, 0, time.Since(start), 0, 0, 0, isTimeoutErr(err)
	}
	defer closeConn(conn)

	if strings.EqualFold(cfg.StreamType, "none") || !expectsResponse(cfg.PayloadDirection) && strings.EqualFold(cfg.PayloadDirection, "none") {
		return true, connectLatency, connectLatency, 0, 0, 0, false
	}

	payload := buildPayload(cfg.PayloadSizeBytes)
	success, requestLatency, bytesSent, bytesReceived, firstByteLatency, timeout := doStreamRequest(cfg, conn, payload)
	if !success {
		return false, connectLatency, connectLatency + requestLatency, bytesSent, bytesReceived, firstByteLatency, timeout
	}

	return true, connectLatency, connectLatency + requestLatency, bytesSent, bytesReceived, firstByteLatency, false
}

func dialConnection(cfg config) (*quic.Conn, time.Duration, error) {
	start := time.Now()
	opCtx, cancel := context.WithTimeout(context.Background(), defaultOpTimeout)
	defer cancel()

	tlsConfig := &tls.Config{
		InsecureSkipVerify: true,
		NextProtos:         []string{cfg.ALPN},
		ServerName:         cfg.TargetHost,
		MinVersion:         tls.VersionTLS13,
	}

	conn, err := quic.DialAddr(opCtx, cfg.TargetAddr, tlsConfig, &quic.Config{})
	return conn, time.Since(start), err
}

func writeAll(stream *quic.Stream, payload []byte) (int64, error) {
	var written int64
	for len(payload) > 0 {
		n, err := stream.Write(payload)
		written += int64(n)
		payload = payload[n:]
		if err != nil {
			return written, err
		}
	}
	return written, nil
}

func readEcho(stream *quic.Stream, expected int) (int64, time.Duration, error) {
	if expected <= 0 {
		return 0, 0, nil
	}

	start := time.Now()
	scratch := make([]byte, 32*1024)
	var readBytes int64
	var firstByteLatency time.Duration
	firstByteCaptured := false

	for readBytes < int64(expected) {
		remaining := int64(expected) - readBytes
		chunk := scratch
		if remaining < int64(len(chunk)) {
			chunk = chunk[:int(remaining)]
		}

		n, err := stream.Read(chunk)
		if n > 0 {
			readBytes += int64(n)
			if !firstByteCaptured {
				firstByteCaptured = true
				firstByteLatency = time.Since(start)
			}
		}

		if err != nil {
			if errors.Is(err, io.EOF) && readBytes == int64(expected) {
				break
			}
			return readBytes, firstByteLatency, err
		}
	}

	return readBytes, firstByteLatency, nil
}

func buildPayload(size int) []byte {
	if size <= 0 {
		return nil
	}

	payload := make([]byte, size)
	for i := range payload {
		payload[i] = byte((i*31 + 17) % 251)
	}
	return payload
}

func closeConn(conn *quic.Conn) {
	_ = conn.CloseWithError(0, "")
}

func canonicalBehavior(behavior string) string {
	switch strings.ToLower(strings.TrimSpace(behavior)) {
	case "multiplex-streams", "multiplex":
		return "multiplex"
	case "duplex", "duplex-streams":
		return "duplex-streams"
	default:
		return strings.ToLower(strings.TrimSpace(behavior))
	}
}

func expectsResponse(payloadDirection string) bool {
	switch strings.ToLower(strings.TrimSpace(payloadDirection)) {
	case "bidirectional", "bidirectional-stream", "server-to-client":
		return true
	default:
		return false
	}
}

func isTimeoutErr(err error) bool {
	if err == nil {
		return false
	}

	var timeoutErr interface{ Timeout() bool }
	if errors.As(err, &timeoutErr) {
		return timeoutErr.Timeout()
	}

	return errors.Is(err, context.DeadlineExceeded)
}

func (m *runMetrics) recordConnect(latency time.Duration) {
	m.mu.Lock()
	defer m.mu.Unlock()
	m.connectLatenciesMs = append(m.connectLatenciesMs, durationToMilliseconds(latency))
}

func (m *runMetrics) recordRequest(success bool, timeout bool, latency time.Duration, bytesSent, bytesReceived int64, firstByteLatency time.Duration) {
	m.mu.Lock()
	defer m.mu.Unlock()
	m.totalRequests++
	if success {
		m.successfulRequests++
		m.requestLatenciesMs = append(m.requestLatenciesMs, durationToMilliseconds(latency))
		if bytesReceived > 0 {
			m.firstByteLatenciesMs = append(m.firstByteLatenciesMs, durationToMilliseconds(firstByteLatency))
		}
	} else {
		m.failedRequests++
	}
	if timeout {
		m.timeoutRequests++
	}
	m.bytesSent += bytesSent
	m.bytesReceived += bytesReceived
}

func (m *runMetrics) snapshot(elapsed time.Duration) map[string]any {
	m.mu.Lock()
	defer m.mu.Unlock()

	requestStats := summarize(m.requestLatenciesMs)
	connectStats := summarize(m.connectLatenciesMs)
	firstByteStats := summarize(m.firstByteLatenciesMs)
	elapsedSeconds := elapsed.Seconds()
	if elapsedSeconds <= 0 {
		elapsedSeconds = 1e-9
	}

	return map[string]any{
		"requestsPerSecond":        float64(m.totalRequests) / elapsedSeconds,
		"totalRequests":            m.totalRequests,
		"successfulRequests":       m.successfulRequests,
		"failedRequests":           m.failedRequests,
		"timeoutRequests":          m.timeoutRequests,
		"bytesReceived":            m.bytesReceived,
		"bytesSent":                m.bytesSent,
		"throughputBytesPerSecond": float64(m.bytesReceived+m.bytesSent) / elapsedSeconds,
		"latencyMinMs":             requestStats.Min,
		"latencyMeanMs":            requestStats.Mean,
		"latencyP50Ms":             requestStats.P50,
		"latencyP75Ms":             requestStats.P75,
		"latencyP90Ms":             requestStats.P90,
		"latencyP95Ms":             requestStats.P95,
		"latencyP99Ms":             requestStats.P99,
		"latencyMaxMs":             requestStats.Max,
		"connectTimeMeanMs":        connectStats.Mean,
		"timeToFirstByteMeanMs":    firstByteStats.Mean,
	}
}

func summarize(samples []float64) latencyStats {
	if len(samples) == 0 {
		return latencyStats{}
	}

	ordered := append([]float64(nil), samples...)
	sort.Float64s(ordered)

	var sum float64
	for _, sample := range ordered {
		sum += sample
	}

	return latencyStats{
		Min:  ordered[0],
		Mean: sum / float64(len(ordered)),
		P50:  percentile(ordered, 0.50),
		P75:  percentile(ordered, 0.75),
		P90:  percentile(ordered, 0.90),
		P95:  percentile(ordered, 0.95),
		P99:  percentile(ordered, 0.99),
		Max:  ordered[len(ordered)-1],
	}
}

func percentile(sorted []float64, p float64) float64 {
	if len(sorted) == 0 {
		return 0
	}

	if p <= 0 {
		return sorted[0]
	}
	if p >= 1 {
		return sorted[len(sorted)-1]
	}

	index := int(math.Ceil(p*float64(len(sorted)))) - 1
	if index < 0 {
		index = 0
	}
	if index >= len(sorted) {
		index = len(sorted) - 1
	}
	return sorted[index]
}

func durationToMilliseconds(duration time.Duration) float64 {
	return float64(duration) / float64(time.Millisecond)
}

func newResultDocument(cfg config) resultDocument {
	return resultDocument{
		Tool:             resultTool,
		Category:         resultCategory,
		Protocol:         "quic",
		Target:           cfg.TargetURL,
		Behavior:         canonicalBehavior(resolveBehavior(cfg)),
		StreamType:       cfg.StreamType,
		PayloadDirection: cfg.PayloadDirection,
		OpenPattern:      cfg.OpenPattern,
		ALPN:             cfg.ALPN,
		Metrics:          map[string]any{},
		Warnings:         []string{loopbackBypassNote},
		Notes:            []string{},
		Errors:           []string{},
	}
}

func newErrorDocument(message string) resultDocument {
	return resultDocument{
		Tool:     resultTool,
		Category: resultCategory,
		Protocol: "quic",
		Metrics:  map[string]any{},
		Warnings: []string{},
		Notes:    []string{},
		Errors:   []string{message},
	}
}

func (doc resultDocument) withError(message string) resultDocument {
	if len(doc.Warnings) == 0 {
		doc.Warnings = []string{}
	}
	if len(doc.Notes) == 0 {
		doc.Notes = []string{}
	}
	doc.Errors = append(doc.Errors, message)
	if doc.Metrics == nil {
		doc.Metrics = map[string]any{}
	}
	return doc
}

func emitDocument(doc resultDocument) error {
	if doc.Metrics == nil {
		doc.Metrics = map[string]any{}
	}
	if doc.Warnings == nil {
		doc.Warnings = []string{}
	}
	if doc.Notes == nil {
		doc.Notes = []string{}
	}
	if doc.Errors == nil {
		doc.Errors = []string{}
	}

	encoder := json.NewEncoder(os.Stdout)
	encoder.SetEscapeHTML(false)
	return encoder.Encode(doc)
}
