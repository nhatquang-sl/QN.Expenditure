package controllers

import (
	"log/slog"
	"net/http"

	"auth/cmd/respond"
	"auth/internal/application/health"

	. "qn.expenditure/shared/app"
)

type HealthController struct {
	handler Handler[health.Command, health.Result]
	logger  *slog.Logger
}

func NewHealthController(mux *http.ServeMux, logger *slog.Logger) {
	c := &HealthController{handler: health.NewHandler(), logger: logger}
	mux.HandleFunc("/health", c.handle)
}

func (c *HealthController) handle(w http.ResponseWriter, r *http.Request) {
	result, err := c.handler.Handle(r.Context(), health.Command{})
	respond.NewResponse(w, c.logger).JSON(http.StatusOK, result, err)
}
