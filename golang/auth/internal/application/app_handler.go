package application

import "auth/internal/application/health"

type Handler struct {
	// Add any dependencies or services needed for the handler
	*health.Handler
}

func NewHandler() *Handler {
	return &Handler{}
}
