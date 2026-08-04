package handler

import (
	"encoding/json"
	"net/http"

	"auth/internal/infrastructure/config"
)

func RegisterHealthRoutes(mux *http.ServeMux, cfg config.Config) {
	mux.HandleFunc("GET /health", func(w http.ResponseWriter, r *http.Request) {
		w.Header().Set("Content-Type", "application/json")
		_ = json.NewEncoder(w).Encode(map[string]string{
			"app_env": cfg.AppEnv,
			"version": cfg.Version,
		})
	})
}
