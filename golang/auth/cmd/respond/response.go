package respond

import (
	"encoding/json"
	"net/http"
)

type Response struct {
	w http.ResponseWriter
}

func NewResponse(w http.ResponseWriter) Response {
	return Response{w: w}
}

func (r Response) JSON(status int, v any) {
	r.w.Header().Set("Content-Type", "application/json")
	r.w.WriteHeader(status)
	json.NewEncoder(r.w).Encode(v)
}

func (r Response) OK(v any) {
	r.JSON(http.StatusOK, v)
}
