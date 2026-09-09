package health

import (
	"context"

	. "qn.expenditure/shared/app"
)

type Command struct{}

type Result struct {
	Status string `json:"status"`
}

type handler struct{}

func NewHandler() Handler[Command, Result] {
	return &handler{}
}

func (h *handler) Handle(_ context.Context, _ Command) (Result, error) {
	return Result{Status: "healthy"}, nil
}
