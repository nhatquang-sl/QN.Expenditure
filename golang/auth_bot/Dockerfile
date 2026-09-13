# docker build . --no-cache --progress plain -t qex.auth-bot:local -f golang/auth_bot/Dockerfile
# docker run --rm --env-file credentials/qex/.env qex.auth-bot:local

FROM golang:1.27-alpine3.24 AS build
WORKDIR /workspace

COPY golang/shared ./shared
COPY golang/auth_bot ./auth_bot

WORKDIR /workspace/auth_bot
RUN go mod download
ARG VERSION=0.0.1
RUN CGO_ENABLED=0 GOOS=linux go build \
    -trimpath \
    -ldflags="-s -w" \
    -o /auth-bot \
    ./cmd/main.go

FROM alpine:3.24
RUN apk --no-cache add ca-certificates tzdata
WORKDIR /app
COPY --from=build /auth-bot .
ARG VERSION=0.0.1
ENV VERSION=${VERSION}
ENTRYPOINT ["/app/auth-bot"]
