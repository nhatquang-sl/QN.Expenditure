package telemetry

import (
	"context"
	"log/slog"

	sharedtelemetry "qn.expenditure/shared/telemetry"
)

func Setup(ctx context.Context, serviceName, version string) (slog.Handler, func(context.Context) error, error) {
	return sharedtelemetry.Setup(ctx, serviceName, version)
}
