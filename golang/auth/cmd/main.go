package main

import (
	"auth/internal/application"
	"auth/internal/config"
	"auth/internal/database"
	"encoding/json"
	"fmt"
	"log/slog"
	"net/http"
	"os"
)

func writeJSON(w http.ResponseWriter, status int, data any) {
	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(status)
	json.NewEncoder(w).Encode(data)
}

func main() {
	logger := slog.New(slog.NewTextHandler(os.Stdout, nil))
	cfg := config.LoadJSONConfig()
	logger.Info("config loaded", slog.String("endpoint", cfg.Application.Endpoint), slog.String("version", cfg.Application.Version))

	// connect to database
	db := database.ConnectDB("host=localhost user=sunlight password=@dmin191092 dbname=auth-test sslmode=disable")
	defer db.Close()

	// 1. set up HTTP server
	mux := http.NewServeMux()

	// 2. setup routes
	handler := application.NewHandler()
	mux.HandleFunc("/health", func(w http.ResponseWriter, r *http.Request) {
		writeJSON(w, http.StatusOK, handler.Health())
	})

	// 3. server instance
	serverAddr := fmt.Sprintf(":%d", cfg.GoServerPort)
	srv := &http.Server{
		Addr:    serverAddr,
		Handler: mux,
	}

	if cfg.TLSCertPath != "" && cfg.TLSKeyPath != "" {
		logger.Info("starting server (TLS)", slog.String("addr", "https://localhost"+srv.Addr))
		if err := srv.ListenAndServeTLS(cfg.TLSCertPath, cfg.TLSKeyPath); err != nil {
			logger.Error("server stopped", slog.Any("error", err))
			os.Exit(1)
		}
	} else {
		logger.Info("starting server", slog.String("addr", srv.Addr))
		if err := srv.ListenAndServe(); err != nil {
			logger.Error("server stopped", slog.Any("error", err))
			os.Exit(1)
		}
	}
}
