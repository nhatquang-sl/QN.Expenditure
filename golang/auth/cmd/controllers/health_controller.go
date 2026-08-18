package controllers

import (
	"auth/cmd/respond"
	"auth/internal/application/health"
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
	respond.NewResponse(w).OK(c.handler.Handle())
}
