# docker build . --no-cache --progress plain -t go-trading-bot:local -f golang/trading_bot/Dockerfile
# docker run --rm go-trading-bot:local

FROM golang:1.27-alpine3.24 AS build
WORKDIR /workspace

COPY golang/shared ./shared
COPY golang/trading_bot ./trading_bot

WORKDIR /workspace/trading_bot
RUN go mod download
ARG VERSION=0.0.1
RUN CGO_ENABLED=0 GOOS=linux go build \
    -trimpath \
    -ldflags="-s -w" \
    -o /trading-bot \
    ./cmd/main.go

FROM alpine:3.24
RUN apk --no-cache add ca-certificates tzdata
WORKDIR /app
COPY --from=build /trading-bot .
ARG VERSION=0.0.1
ENV VERSION=${VERSION}
ENTRYPOINT ["/app/trading-bot"]
