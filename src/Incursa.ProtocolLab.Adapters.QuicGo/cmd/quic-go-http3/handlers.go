// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

package main

import (
	"crypto/rand"
	"crypto/rsa"
	"crypto/sha256"
	"crypto/tls"
	"crypto/x509"
	"crypto/x509/pkix"
	"encoding/hex"
	"encoding/json"
	"encoding/pem"
	"fmt"
	"io"
	"math/big"
	"net"
	"net/http"
	"os"
	"strconv"
	"strings"
	"time"
)

func registerHandlers(mux *http.ServeMux, implementation string) {
	mux.HandleFunc("/plaintext", func(w http.ResponseWriter, r *http.Request) {
		if r.Method != http.MethodGet {
			http.Error(w, http.StatusText(http.StatusMethodNotAllowed), http.StatusMethodNotAllowed)
			return
		}

		writeTextResponse(w, "text/plain", "Hello, World!")
	})

	mux.HandleFunc("/json", func(w http.ResponseWriter, r *http.Request) {
		if r.Method != http.MethodGet {
			http.Error(w, http.StatusText(http.StatusMethodNotAllowed), http.StatusMethodNotAllowed)
			return
		}

		writeJSONResponse(w, messageResponse{Message: "Hello, World!"})
	})

	mux.HandleFunc("/status", func(w http.ResponseWriter, r *http.Request) {
		if r.Method != http.MethodGet {
			http.Error(w, http.StatusText(http.StatusMethodNotAllowed), http.StatusMethodNotAllowed)
			return
		}

		writeJSONResponse(w, statusResponse{
			Server:         "quic-go-http3",
			Implementation: implementation,
			Protocol:       r.Proto,
			UTC:            time.Now().UTC(),
			ProcessID:      os.Getpid(),
		})
	})

	mux.HandleFunc("/bytes/", func(w http.ResponseWriter, r *http.Request) {
		if r.Method != http.MethodGet {
			http.Error(w, http.StatusText(http.StatusMethodNotAllowed), http.StatusMethodNotAllowed)
			return
		}

		size, err := parseTrailingInt(r.URL.Path, "/bytes/")
		if err != nil || size < 0 {
			http.Error(w, http.StatusText(http.StatusBadRequest), http.StatusBadRequest)
			return
		}

		writeBinaryResponse(w, createDeterministicBytes(size))
	})

	mux.HandleFunc("/stream/bytes", func(w http.ResponseWriter, r *http.Request) {
		if r.Method != http.MethodGet {
			http.Error(w, http.StatusText(http.StatusMethodNotAllowed), http.StatusMethodNotAllowed)
			return
		}

		chunks, err := parseOptionalQueryInt(r, "chunks")
		if err != nil || chunks < 0 {
			http.Error(w, http.StatusText(http.StatusBadRequest), http.StatusBadRequest)
			return
		}

		size, err := parseOptionalQueryInt(r, "size")
		if err != nil || size < 0 {
			http.Error(w, http.StatusText(http.StatusBadRequest), http.StatusBadRequest)
			return
		}

		delayMs, err := parseOptionalQueryInt(r, "delayMs")
		if err != nil || delayMs < 0 {
			http.Error(w, http.StatusText(http.StatusBadRequest), http.StatusBadRequest)
			return
		}

		w.Header().Set("Content-Type", "application/octet-stream")
		chunk := createDeterministicBytes(size)

		flusher, _ := w.(http.Flusher)
		for index := 0; index < chunks; index++ {
			if _, err := w.Write(chunk); err != nil {
				return
			}

			if flusher != nil {
				flusher.Flush()
			}

			if delayMs > 0 && index+1 < chunks {
				timer := time.NewTimer(time.Duration(delayMs) * time.Millisecond)
				select {
				case <-r.Context().Done():
					timer.Stop()
					return
				case <-timer.C:
				}
			}
		}
	})

	mux.HandleFunc("/sink", func(w http.ResponseWriter, r *http.Request) {
		if r.Method != http.MethodPost {
			http.Error(w, http.StatusText(http.StatusMethodNotAllowed), http.StatusMethodNotAllowed)
			return
		}

		body, err := io.ReadAll(r.Body)
		if err != nil {
			http.Error(w, http.StatusText(http.StatusInternalServerError), http.StatusInternalServerError)
			return
		}

		writeJSONResponse(w, bytesReadResponse{BytesRead: len(body)})
	})

	mux.HandleFunc("/hash", func(w http.ResponseWriter, r *http.Request) {
		if r.Method != http.MethodPost {
			http.Error(w, http.StatusText(http.StatusMethodNotAllowed), http.StatusMethodNotAllowed)
			return
		}

		body, err := io.ReadAll(r.Body)
		if err != nil {
			http.Error(w, http.StatusText(http.StatusInternalServerError), http.StatusInternalServerError)
			return
		}

		sum := sha256.Sum256(body)
		writeJSONResponse(w, hashResponse{
			BytesRead: len(body),
			Sha256:    hex.EncodeToString(sum[:]),
		})
	})

	mux.HandleFunc("/echo", func(w http.ResponseWriter, r *http.Request) {
		if r.Method != http.MethodPost {
			http.Error(w, http.StatusText(http.StatusMethodNotAllowed), http.StatusMethodNotAllowed)
			return
		}

		body, err := io.ReadAll(r.Body)
		if err != nil {
			http.Error(w, http.StatusText(http.StatusInternalServerError), http.StatusInternalServerError)
			return
		}

		writeBinaryResponse(w, body)
	})

	mux.HandleFunc("/headers/response", func(w http.ResponseWriter, r *http.Request) {
		if r.Method != http.MethodGet {
			http.Error(w, http.StatusText(http.StatusMethodNotAllowed), http.StatusMethodNotAllowed)
			return
		}

		count, err := parseOptionalQueryInt(r, "count")
		if err != nil || count < 0 {
			http.Error(w, http.StatusText(http.StatusBadRequest), http.StatusBadRequest)
			return
		}

		size, err := parseOptionalQueryInt(r, "size")
		if err != nil || size < 0 {
			http.Error(w, http.StatusText(http.StatusBadRequest), http.StatusBadRequest)
			return
		}

		value := strings.Repeat("a", size)
		for index := 0; index < count; index++ {
			w.Header().Set(fmt.Sprintf("X-Protocol-Bench-Header-%03d", index), value)
		}

		writeTextResponse(w, "text/plain", "headers")
	})

	mux.HandleFunc("/inspect/headers", func(w http.ResponseWriter, r *http.Request) {
		if r.Method != http.MethodGet {
			http.Error(w, http.StatusText(http.StatusMethodNotAllowed), http.StatusMethodNotAllowed)
			return
		}

		headers := make(map[string]string, len(r.Header))
		for name, values := range r.Header {
			headers[name] = strings.Join(values, ",")
		}

		writeJSONResponse(w, headers)
	})
}

func envOrDefault(name, fallback string) string {
	value := strings.TrimSpace(os.Getenv(name))
	if value == "" {
		return fallback
	}

	return value
}

func prepareTLSMaterial() (string, string, string, *tls.Config, error) {
	certDir, err := os.MkdirTemp("", "protocol-lab-quic-go-http3-*")
	if err != nil {
		return "", "", "", nil, err
	}

	certPath := certDir + string(os.PathSeparator) + "server.crt"
	keyPath := certDir + string(os.PathSeparator) + "server.key"

	if err := writeSelfSignedCertificate(certPath, keyPath); err != nil {
		_ = os.RemoveAll(certDir)
		return "", "", "", nil, err
	}

	cert, err := tls.LoadX509KeyPair(certPath, keyPath)
	if err != nil {
		_ = os.RemoveAll(certDir)
		return "", "", "", nil, err
	}

	tlsConfig := &tls.Config{
		Certificates: []tls.Certificate{cert},
		MinVersion:   tls.VersionTLS13,
		NextProtos:   []string{"h3"},
	}

	return certDir, certPath, keyPath, tlsConfig, nil
}

func writeSelfSignedCertificate(certPath, keyPath string) error {
	privateKey, err := rsa.GenerateKey(rand.Reader, 2048)
	if err != nil {
		return err
	}

	serialNumberLimit := new(big.Int).Lsh(big.NewInt(1), 62)
	serialNumber, err := rand.Int(rand.Reader, serialNumberLimit)
	if err != nil {
		return err
	}

	template := x509.Certificate{
		SerialNumber: serialNumber,
		Subject:      pkixName("quic-go-http3"),
		NotBefore:    time.Now().Add(-time.Hour),
		NotAfter:     time.Now().Add(7 * 24 * time.Hour),
		KeyUsage:     x509.KeyUsageDigitalSignature | x509.KeyUsageKeyEncipherment,
		ExtKeyUsage:  []x509.ExtKeyUsage{x509.ExtKeyUsageServerAuth},
		DNSNames:     []string{"localhost", "host.docker.internal"},
		IPAddresses:  []net.IP{net.ParseIP("127.0.0.1"), net.ParseIP("::1")},
	}

	derBytes, err := x509.CreateCertificate(rand.Reader, &template, &template, &privateKey.PublicKey, privateKey)
	if err != nil {
		return err
	}

	certOut, err := os.Create(certPath)
	if err != nil {
		return err
	}
	defer certOut.Close()

	if err := pem.Encode(certOut, &pem.Block{Type: "CERTIFICATE", Bytes: derBytes}); err != nil {
		return err
	}

	keyOut, err := os.OpenFile(keyPath, os.O_WRONLY|os.O_CREATE|os.O_TRUNC, 0o600)
	if err != nil {
		return err
	}
	defer keyOut.Close()

	keyBytes := x509.MarshalPKCS1PrivateKey(privateKey)
	if err := pem.Encode(keyOut, &pem.Block{Type: "RSA PRIVATE KEY", Bytes: keyBytes}); err != nil {
		return err
	}

	return nil
}

func pkixName(commonName string) pkix.Name {
	return pkix.Name{
		CommonName: commonName,
		Organization: []string{
			"Incursa LLC",
		},
	}
}

func writeJSONResponse(w http.ResponseWriter, value any) {
	w.Header().Set("Content-Type", "application/json")
	encoder := json.NewEncoder(w)
	encoder.SetEscapeHTML(false)
	_ = encoder.Encode(value)
}

func writeTextResponse(w http.ResponseWriter, contentType, value string) {
	w.Header().Set("Content-Type", contentType)
	_, _ = io.WriteString(w, value)
}

func writeBinaryResponse(w http.ResponseWriter, value []byte) {
	w.Header().Set("Content-Type", "application/octet-stream")
	_, _ = w.Write(value)
}

func createDeterministicBytes(size int) []byte {
	bytes := make([]byte, size)
	for index := range bytes {
		bytes[index] = byte(index % 251)
	}

	return bytes
}

func parseTrailingInt(path, prefix string) (int, error) {
	value := strings.TrimPrefix(path, prefix)
	if value == "" {
		return 0, fmt.Errorf("missing integer path segment")
	}

	return strconv.Atoi(value)
}

func parseOptionalQueryInt(r *http.Request, key string) (int, error) {
	value := strings.TrimSpace(r.URL.Query().Get(key))
	if value == "" {
		return 0, nil
	}

	return strconv.Atoi(value)
}

type messageResponse struct {
	Message string `json:"message"`
}

type statusResponse struct {
	Server         string    `json:"server"`
	Implementation string    `json:"implementation"`
	Protocol       string    `json:"protocol"`
	UTC            time.Time `json:"utc"`
	ProcessID      int       `json:"processId"`
}

type bytesReadResponse struct {
	BytesRead int `json:"bytesRead"`
}

type hashResponse struct {
	BytesRead int    `json:"bytesRead"`
	Sha256    string `json:"sha256"`
}
