package health

type Handler struct {
}

func (h *Handler) Health() map[string]string {
	return map[string]string{"status": "healthy"}
}
