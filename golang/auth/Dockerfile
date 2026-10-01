# docker build . --no-cache --progress plain -t goauthapi:0.0.2 -f golang/auth/Dockerfile
# docker build . -t goauthapi:0.0.2 -f golang/auth/Dockerfile
# docker run -it --rm -p 5002:5002 goauthapi:0.0.2
# http://localhost:5002/api/weatherforecast
# Build stage
FROM golang:1.27-alpine3.24 AS build
WORKDIR /workspace
COPY golang/shared ./shared
COPY golang/auth ./auth
RUN cd /workspace/auth && go mod download && go install github.com/sqlc-dev/sqlc/cmd/sqlc@v1.31.1
WORKDIR /workspace/auth
RUN sqlc generate -f internal/database/sqlc.yaml
ARG VERSION=0.0.1
RUN CGO_ENABLED=0 GOOS=linux go build \
    -trimpath \
    -ldflags="-s -w" \
    -o /auth \
    ./cmd/main.go

# Final stage
FROM alpine:3.24
RUN apk --no-cache add ca-certificates tzdata
WORKDIR /app
COPY --from=build /auth .
ARG VERSION=0.0.1
ENV VERSION=${VERSION}

ENTRYPOINT ["/app/auth"]
