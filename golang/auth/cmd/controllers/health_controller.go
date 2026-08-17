package controllers

import (
	"auth/internal/application/health"
	"encoding/json"
	"net/http"
)

type HealthController struct {
	handler *health.Handler
}

func NewHealthController(mux *http.ServeMux) {
	c := &HealthController{handler: &health.Handler{}}
	mux.HandleFunc("/health", c.handle)
}

func (c *HealthController) handle(w http.ResponseWriter, r *http.Request) {
	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(http.StatusOK)
	json.NewEncoder(w).Encode(c.handler.Handle())
}
