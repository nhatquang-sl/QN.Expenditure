# Observability

## Overview

All services in this project — Go (auth, auth-bot, email-consumer, email-worker) and .NET (WebAPI) — are instrumented using the **OpenTelemetry** standard across three pillars: **traces**, **metrics**, and **logs**. Telemetry data is exported via the **OTLP** protocol over gRPC to an **Elastic Fleet Server** (acting as the APM intake), which stores everything in **Elasticsearch** and surfaces it through **Kibana**.

When `OTEL_EXPORTER_OTLP_ENDPOINT` is not set (local dev without the observability stack), all services fall back to stdout-only structured logging — no code changes required.

---

## Glossary of Terms

### OpenTelemetry (OTel)

An open-source observability framework and set of APIs, SDKs, and tools for generating, collecting, and exporting telemetry data (traces, metrics, logs). It is vendor-neutral — the same instrumentation code works with Elastic, Grafana, Jaeger, Datadog, etc.

In this project, every Go service delegates to a single shared setup function:

```go
// golang/shared/telemetry/otel.go
func Setup(ctx context.Context, version string) (slog.Handler, func(context.Context) error, error)
```

Each service's own `internal/telemetry/otel.go` is a thin wrapper that calls this shared function. The .NET equivalent lives in `src/Aspire/ServiceDefaults/Extensions.cs`.

---

### OTLP (OpenTelemetry Protocol)

The wire protocol used to send telemetry data from a service to a backend collector. Supports both gRPC and HTTP transports. This project uses **gRPC exclusively**.

Three OTLP gRPC exporters are configured in the shared Go setup:

| Signal | Package | Exporter |
|--------|---------|---------|
| Traces | `go.opentelemetry.io/otel/exporters/otlp/otlptrace/otlptracegrpc` | `otlptracegrpc.New(ctx)` |
| Metrics | `go.opentelemetry.io/otel/exporters/otlp/otlpmetric/otlpmetricgrpc` | `otlpmetricgrpc.New(ctx)` |
| Logs | `go.opentelemetry.io/otel/exporters/otlp/otlplog/otlploggrpc` | `otlploggrpc.New(ctx)` |

The destination endpoint is read from the `OTEL_EXPORTER_OTLP_ENDPOINT` environment variable. In production this is `http://fleet-server:8200`.

---

### APM (Application Performance Monitoring)

A category of tooling that tracks the runtime behaviour of services — request latency, error rates, database query times, etc. — by analysing traces and metrics. In this project, **Elastic APM** is the APM solution, delivered through **Fleet Server** which includes a built-in APM Server intake.

The APM Server listens on port 8200 and accepts data in OTLP format, so no Elastic-specific SDK is required on the service side.

---

### Traces & Spans

A **trace** represents the end-to-end journey of a single request across services. It is made up of **spans** — individual units of work (e.g. "handle HTTP POST /login", "query UserSessions", "publish RabbitMQ message").

**Go — automatic HTTP spans** (`golang/auth/cmd/main.go`):

```go
Handler: otelhttp.NewHandler(middleware.Recover(logger, mux), os.Getenv("OTEL_SERVICE_NAME"))
```

`otelhttp` wraps the entire HTTP mux and creates one root span per request, propagating trace context from incoming headers.

**Go — automatic DB spans** (`golang/shared/database/postgres.go`):

The shared `OpenPostgres` function wraps `*sql.DB` with `otelsql`, which emits a child span for every SQL statement. The span name is taken from the sqlc `-- name:` comment, stored as `db.sqlc.operation`.

**.NET — manual spans** (`src/WebAPI/HostedServices/TracedBackgroundService.cs`):

```csharp
using var activity = _activitySource.StartActivity("OperationName");
activity?.SetTag("messaging.system", "rabbitmq");
```

Background services create spans manually using `ActivitySource`.

The **Tracer Provider** (`sdktrace.NewTracerProvider`) batches completed spans and sends them to the OTLP exporter.

---

### Metrics

Numeric measurements recorded over time — counters, gauges, histograms. Examples: HTTP request count, request duration, GC heap size.

**Go**: The Meter Provider (`sdkmetric.NewMeterProvider`) uses a periodic reader that flushes metrics to the OTLP exporter on a regular interval.

**.NET** (`src/Aspire/ServiceDefaults/Extensions.cs`): Instruments are registered automatically:

| Instrument | What it measures |
|-----------|-----------------|
| `AspNetCore` | HTTP request count, latency |
| `HttpClient` | Outbound HTTP calls |
| `Runtime` | GC, heap, JIT, thread pool |
| `Process` | CPU, memory usage |

---

### Logs

Structured log records emitted by the application. OTel treats logs as a first-class signal alongside traces and metrics, allowing log records to be correlated with the trace they were emitted within (via `trace_id` / `span_id`).

In Go, logs flow through two channels simultaneously via `multiHandler` (see below). In .NET, the OpenTelemetry logging bridge is registered in `ServiceDefaults`, forwarding all `ILogger` output to the OTLP log exporter.

---

### slog

The Go standard library structured logging package (`log/slog`), introduced in Go 1.21. Every service in this project uses `slog` as its logging API:

```go
logger := slog.New(slogHandler)
slog.SetDefault(logger)
logger.Info("config loaded", slog.String("version", cfg.Application.Version))
```

`slog` is backend-agnostic — it writes to a `slog.Handler`. When OTel is enabled, the handler is `multiHandler`; when not, it is a plain `slog.NewTextHandler(os.Stdout, nil)`.

---

### multiHandler

A custom `slog.Handler` defined in `golang/shared/telemetry/otel.go` that fans out every log record to multiple handlers in sequence:

```go
handler := &multiHandler{handlers: []slog.Handler{
    textHandler,                                           // stdout (always)
    otelslog.NewHandler(serviceName, otelslog.WithSource(true)), // OTel log pipeline
}}
```

This means logs always appear in stdout (visible in `docker compose logs`) **and** are shipped to Elasticsearch via OTLP when the endpoint is configured.

---

### Resource

Metadata that describes the entity producing telemetry — the service identity. Attached to every span, metric, and log record.

```go
res, err := resource.New(ctx,
    resource.WithFromEnv(),   // reads OTEL_RESOURCE_ATTRIBUTES, OTEL_SERVICE_NAME
    resource.WithProcess(),   // PID, executable path, Go runtime version
    resource.WithOS(),        // OS type and version
    resource.WithAttributes(semconv.ServiceVersion(version)),
)
```

In production `OTEL_RESOURCE_ATTRIBUTES` is set to `service.version=<tag>,deployment.environment=production`.

---

### Propagator

The mechanism by which trace context (trace ID, span ID, sampling flag) is passed between services — typically via HTTP headers or message headers.

This project installs two propagators:

```go
otel.SetTextMapPropagator(propagation.NewCompositeTextMapPropagator(
    propagation.TraceContext{}, // W3C Trace Context — "traceparent" header
    propagation.Baggage{},      // W3C Baggage — arbitrary key-value pairs
))
```

`otelhttp` in the auth service uses these propagators automatically to extract incoming trace context from request headers and inject it into outbound calls.

---

### Instrumentation Libraries

Thin wrappers that add OTel spans/metrics to existing libraries without requiring manual span creation.

#### otelsql (`github.com/XSAM/otelsql`)

Wraps `database/sql` to emit a span for every SQL query. Used in `golang/shared/database/postgres.go`:

```go
db, err := otelsql.Open("postgres", connStr)
otelsql.RegisterDBStatsMetrics(db, otelsql.WithAttributes(...))
```

The span name is set to the sqlc operation name (e.g. `CreateUserSession`, `DeleteStaleSessions`).

#### otelhttp (`go.opentelemetry.io/contrib/instrumentation/net/http/otelhttp`)

Wraps `http.Handler` to create a span for every incoming HTTP request. Used only in the auth service (`golang/auth/cmd/main.go`).

#### otelslog (`go.opentelemetry.io/contrib/bridges/otelslog`)

Bridges `slog` records into the OTel log pipeline. Attached inside `multiHandler` so that every `slog` call is automatically forwarded to the OTel Log Provider, preserving trace correlation (trace ID and span ID are attached automatically when a span is active on the context).

---

### Elastic Fleet Server / APM Server

**Fleet Server** is the Elastic Agent management server. In this project it also runs the **APM Server** component, which is the OTLP intake endpoint.

- Defined in `credentials/otel/docker-compose.yml` as the `fleet-server` service
- Listens on port 8200
- Accepts OTLP gRPC from all application services
- Forwards data to Elasticsearch
- Requires a bearer token (`ELASTIC_APM_SECRET_TOKEN`) for authentication, sent by services via `OTEL_EXPORTER_OTLP_HEADERS`

---

### Elasticsearch

The data store for all telemetry signals. Runs as `es01` in the otel Docker Compose stack. Traces are stored as APM documents; metrics and logs are stored in dedicated data streams. Elasticsearch provides the query engine that Kibana uses to build dashboards and the APM UI.

---

### Kibana

The web UI for exploring telemetry data. Connects to Elasticsearch and provides:

- **APM UI** — service map, transaction traces, error tracking, latency percentiles
- **Discover** — ad-hoc log search
- **Dashboards** — custom metric visualisations

Runs as `kibana` in the otel Docker Compose stack.

---

### Metricbeat

An Elastic Beat that collects host and Docker infrastructure metrics (CPU, memory, disk, network, container stats) and ships them directly to Elasticsearch. It is separate from the OTel pipeline — it is not an OTel component. Defined as `metricbeat01` in `credentials/otel/docker-compose.yml`.

---

## Environment Variables

| Variable | Purpose | Production value |
|----------|---------|-----------------|
| `OTEL_SERVICE_NAME` | Identifies the service in all telemetry signals | `go-auth-api`, `go-auth-bot`, `go-email-consumer`, `go-email-worker`, `qexapi` |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | gRPC endpoint of the OTLP receiver | `http://fleet-server:8200` |
| `OTEL_EXPORTER_OTLP_HEADERS` | Auth header sent with every OTLP request | `Authorization=Bearer <token>` |
| `OTEL_RESOURCE_ATTRIBUTES` | Extra resource metadata | `service.version=<tag>,deployment.environment=production` |
| `OTEL_TRACES_EXPORTER` | Trace exporter to use | `otlp` |
| `OTEL_METRICS_EXPORTER` | Metrics exporter to use | `otlp` |
| `OTEL_LOGS_EXPORTER` | Logs exporter to use | `otlp` |
| `ELASTIC_APM_SECRET_TOKEN` | APM Server bearer token (same value as the token in `OTEL_EXPORTER_OTLP_HEADERS`) | secret |

When `OTEL_EXPORTER_OTLP_ENDPOINT` is **not set**, the shared Go setup skips all OTel initialisation and returns a plain stdout text handler. No data is exported.

---

## How It All Connects

```
┌─────────────────────────────────────────────────────┐
│  Application Service (Go or .NET)                   │
│                                                     │
│  slog.Info(...)  ──► otelslog ──► OTel Log SDK      │
│  HTTP request    ──► otelhttp ──► OTel Trace SDK    │
│  SQL query       ──► otelsql  ──► OTel Trace SDK    │
│  metrics         ──────────────► OTel Metric SDK    │
│                                       │             │
│                            OTLP gRPC (port 8200)    │
└───────────────────────────────────────┼─────────────┘
                                        │
                            ┌───────────▼──────────┐
                            │  Elastic Fleet Server │
                            │  (APM Server intake)  │
                            └───────────┬──────────┘
                                        │
                            ┌───────────▼──────────┐
                            │    Elasticsearch      │
                            │  (traces/metrics/logs)│
                            └───────────┬──────────┘
                                        │
                            ┌───────────▼──────────┐
                            │       Kibana          │
                            │  (APM UI, dashboards) │
                            └──────────────────────┘
```

---

## Per-Service Summary

| Service | `OTEL_SERVICE_NAME` | Extra instrumentation |
|---------|--------------------|-----------------------|
| `golang/auth` | `go-auth-api` | `otelhttp` (HTTP mux), `otelsql` (Postgres) |
| `golang/auth_bot` | `go-auth-bot` | `otelsql` (Postgres) |
| `golang/email_consumer` | `go-email-consumer` | `otelsql` (Postgres) |
| `golang/email_worker` | `go-email-worker` | `otelsql` (Postgres) |
| `src/WebAPI` | `qexapi` | AspNetCore, HttpClient, Runtime, Process metrics; manual spans in background services |
