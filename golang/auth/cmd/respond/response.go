package respond

import (
	"encoding/json"
	"net/http"

	"auth/internal/application/apperror"
)

type Response struct {
	w http.ResponseWriter
}

func NewResponse(w http.ResponseWriter) Response {
	return Response{w: w}
}

func (r Response) JSON(status int, result any, err error) {
	r.w.Header().Set("Content-Type", "application/json")

	if err != nil {
		switch e := err.(type) {
		case *apperror.AppError:
			r.w.WriteHeader(e.Code)
			json.NewEncoder(r.w).Encode(map[string]string{"message": e.Message})
		case *apperror.ValidationError:
			r.w.WriteHeader(http.StatusUnprocessableEntity)
			json.NewEncoder(r.w).Encode(e.Fields)
		default:
			r.w.WriteHeader(http.StatusInternalServerError)
			json.NewEncoder(r.w).Encode(map[string]string{"message": "Internal Server Error"})
		}
	} else {
		r.w.WriteHeader(status)
		json.NewEncoder(r.w).Encode(result)
	}
}

func (r Response) OK(v any) {
	r.JSON(http.StatusOK, v, nil)
}

func (r Response) Error(err error) {
	r.JSON(http.StatusBadRequest, nil, err)
}
