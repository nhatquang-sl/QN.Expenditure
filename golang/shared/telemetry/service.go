package telemetry

import (
	"runtime/debug"
	"strings"
)

// ServiceName returns the module name from go.mod with underscores replaced by hyphens,
// so it is safe to use as an OTLP service.name resource attribute.
func ServiceName() string {
	if info, ok := debug.ReadBuildInfo(); ok && info.Main.Path != "" {
		return strings.ReplaceAll(info.Main.Path, "_", "-")
	}
	return "unknown"
}
