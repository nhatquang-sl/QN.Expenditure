package controllers

import (
	"net/http"

	"auth/cmd/respond"
	"auth/internal/application/health"

	. "qn.expenditure/shared/app"
)

type HealthController struct {
	handler Handler[health.Command, health.Result]
}

func NewHealthController(mux *http.ServeMux) {
	c := &HealthController{handler: health.NewHandler()}
	mux.HandleFunc("/health", c.handle)
}

func (c *HealthController) handle(w http.ResponseWriter, r *http.Request) {
	result, err := c.handler.Handle(r.Context(), health.Command{})
	respond.NewResponse(w).JSON(http.StatusOK, result, err)
}
