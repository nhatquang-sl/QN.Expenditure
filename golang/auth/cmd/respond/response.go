package respond

import (
	"net/http"

	sharedhttpx "qn.expenditure/shared/httpx"
)

type Response struct {
	w http.ResponseWriter
}

func NewResponse(w http.ResponseWriter) Response {
	return Response{w: w}
}

func (r Response) JSON(status int, result any, err error) {
	sharedhttpx.WriteJSON(r.w, status, result, err)
}

func (r Response) OK(v any) {
	r.JSON(http.StatusOK, v, nil)
}

func (r Response) Error(err error) {
	r.JSON(http.StatusBadRequest, nil, err)
}
