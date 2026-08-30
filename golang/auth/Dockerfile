# docker build . --no-cache --progress plain -t qex.goapi:0.0.2 -f golang/auth/Dockerfile
# docker build . -t qex.goapi:0.0.2 -f golang/auth/Dockerfile
# docker run -it --rm -p 5002:5002 qex.goapi:0.0.2
# http://localhost:5002/api/weatherforecast
# Build stage
FROM golang:1.27-alpine3.24 AS build
WORKDIR /app
COPY golang/auth/go.mod golang/auth/go.sum ./
RUN go mod download
COPY golang/auth/ .
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
EXPOSE 5002

ARG VERSION=0.0.1
ENV VERSION=${VERSION}
ENV PORT=5002

ENTRYPOINT ["/app/auth"]
