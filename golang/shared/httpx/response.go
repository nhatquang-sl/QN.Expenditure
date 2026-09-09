package httpx

import (
	"encoding/json"
	"net/http"

	. "qn.expenditure/shared/apperror"
)

func WriteJSON(w http.ResponseWriter, status int, result any, err error) {
	w.Header().Set("Content-Type", "application/json")

	if err != nil {
		switch e := err.(type) {
		case *AppError:
			w.WriteHeader(e.Code)
			_ = json.NewEncoder(w).Encode(map[string]string{"message": e.Message})
			return
		case *ValidationError:
			w.WriteHeader(http.StatusUnprocessableEntity)
			_ = json.NewEncoder(w).Encode(e.Fields)
			return
		default:
			w.WriteHeader(http.StatusInternalServerError)
			_ = json.NewEncoder(w).Encode(map[string]string{"message": "Internal Server Error"})
			return
		}
	}

	w.WriteHeader(status)
	_ = json.NewEncoder(w).Encode(result)
}
