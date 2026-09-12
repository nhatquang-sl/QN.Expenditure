# docker build . --no-cache --progress plain -t qex.email-worker:local -f golang/email_worker/Dockerfile
# docker run --rm --env-file credentials/qex/.env qex.email-worker:local

FROM golang:1.27-alpine3.24 AS build
WORKDIR /workspace

COPY golang/shared ./shared
COPY golang/emaildb ./emaildb
COPY golang/email_worker ./email_worker

WORKDIR /workspace/emaildb
RUN go mod download && go install github.com/sqlc-dev/sqlc/cmd/sqlc@v1.31.1
RUN sqlc generate -f internal/database/sqlc.yaml

WORKDIR /workspace/email_worker
RUN go mod download
ARG VERSION=0.0.1
RUN CGO_ENABLED=0 GOOS=linux go build \
    -trimpath \
    -ldflags="-s -w" \
    -o /email-worker \
    ./cmd/main.go

FROM alpine:3.24
RUN apk --no-cache add ca-certificates tzdata
WORKDIR /app
COPY --from=build /email-worker .
ARG VERSION=0.0.1
ENV VERSION=${VERSION}
ENTRYPOINT ["/app/email-worker"]
