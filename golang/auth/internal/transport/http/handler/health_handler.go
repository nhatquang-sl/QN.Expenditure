package handler

import (
	"auth/internal/config"
	"encoding/json"
	"net/http"
)

func RegisterHealthRoutes(mux *http.ServeMux, cfg config.Config) {
	mux.HandleFunc("GET /health", func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Content-Type", "application/json")
		_ = json.NewEncoder(w).Encode(map[string]string{
			"app_env": cfg.Application.Endpoint,
			"version": cfg.Application.Version,
		})
	})
}
