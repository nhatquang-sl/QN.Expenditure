package health

import (
	"context"

	"auth/internal/application"
)

type Command struct{}

type Result struct {
	Status string `json:"status"`
}

type handler struct{}

func NewHandler() application.Handler[Command, Result] {
	return &handler{}
}

func (h *handler) Handle(_ context.Context, _ Command) (Result, error) {
	return Result{Status: "healthy"}, nil
}
