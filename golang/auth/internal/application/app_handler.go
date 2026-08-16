package application

type Handler struct {
	// Add any dependencies or services needed for the handler
}

func NewHandler() *Handler {
	return &Handler{}
}

func (h *Handler) Health() map[string]string {
	return map[string]string{"status": "healthy"}
}
