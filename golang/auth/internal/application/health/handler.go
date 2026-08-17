package health

type Handler struct {
}

func (h *Handler) Handle() map[string]string {
	return map[string]string{"status": "healthy"}
}
