// Copyright (c) 2026 Incursa LLC.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

package main

import (
	"errors"
	"fmt"
	"log"
	"net/http"
	"os"

	"github.com/quic-go/quic-go/http3"
)

const (
	defaultListenAddress  = ":8443"
	defaultImplementation = "quic-go-http3"
)

func main() {
	implementation := envOrDefault("PROTOCOL_LAB_IMPLEMENTATION", defaultImplementation)
	listenAddress := envOrDefault("PROTOCOL_LAB_QUIC_GO_ADDR", defaultListenAddress)

	certDir, certFile, keyFile, tlsConfig, err := prepareTLSMaterial()
	if err != nil {
		log.Fatalf("failed to prepare TLS material: %v", err)
	}
	defer func() {
		if removeErr := os.RemoveAll(certDir); removeErr != nil {
			log.Printf("warning: failed to remove temporary certificate directory %q: %v", certDir, removeErr)
		}
	}()

	mux := http.NewServeMux()
	registerHandlers(mux, implementation)

	fmt.Printf("QUIC_GO_IMPLEMENTATION=%s\n", implementation)
	fmt.Printf("QUIC_GO_LISTEN=%s\n", listenAddress)
	fmt.Printf("QUIC_GO_CERT_DIR=%s\n", certDir)
	log.Printf("quic-go HTTP/3 server starting on %s", listenAddress)

	server := &http3.Server{
		Addr:      listenAddress,
		Handler:   mux,
		TLSConfig: tlsConfig,
	}

	err = server.ListenAndServeTLS(certFile, keyFile)
	if err != nil && !errors.Is(err, http.ErrServerClosed) {
		log.Fatalf("quic-go HTTP/3 server stopped with error: %v", err)
	}
}
