package controllers

import (
	"net/http"

	"auth/cmd/respond"
	"auth/internal/application"
	"auth/internal/application/health"
)

type HealthController struct {
	handler application.Handler[health.Command, health.Result]
}

func NewHealthController(mux *http.ServeMux) {
	c := &HealthController{handler: health.NewHandler()}
	mux.HandleFunc("/health", c.handle)
}

func (c *HealthController) handle(w http.ResponseWriter, r *http.Request) {
	result, err := c.handler.Handle(r.Context(), health.Command{})
	respond.NewResponse(w).JSON(http.StatusOK, result, err)
}
