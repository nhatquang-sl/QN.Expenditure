package middleware

import (
	"log/slog"
	"net/http"

	sharedhttpx "qn.expenditure/shared/httpx"
)

func Recover(logger *slog.Logger, next http.Handler) http.Handler {
	return sharedhttpx.Recover(logger, next)
}
