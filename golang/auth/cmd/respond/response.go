package respond

import (
	"log/slog"
	"net/http"

	sharedhttpx "qn.expenditure/shared/httpx"
	. "qn.expenditure/shared/apperror"
)

type Response struct {
	w      http.ResponseWriter
	logger *slog.Logger
}

func NewResponse(w http.ResponseWriter, logger *slog.Logger) Response {
	return Response{w: w, logger: logger}
}

func (r Response) JSON(status int, result any, err error) {
	if err != nil {
		switch err.(type) {
		case *AppError, *ValidationError:
			// expected errors — no logging needed
		default:
			r.logger.Error("unhandled error", slog.Any("error", err))
		}
	}
	sharedhttpx.WriteJSON(r.w, status, result, err)
}

func (r Response) OK(v any) {
	r.JSON(http.StatusOK, v, nil)
}

func (r Response) Error(err error) {
	r.JSON(http.StatusBadRequest, nil, err)
}
