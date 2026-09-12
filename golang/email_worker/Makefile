APP_NAME=email-worker
BUILD_DIR=./bin

run:
	@echo "Running email worker..."
	@CONFIG_PATH=../../credentials/appsettings.json go run ./cmd

build:
	@echo "Building $(APP_NAME)..."
	@mkdir -p $(BUILD_DIR)
	@go build -o $(BUILD_DIR)/worker ./cmd

fmt:
	@go fmt ./...

deps:
	@go mod tidy

test:
	@echo "Running worker tests..."
	@go test -v ./cmd/worker_tests/...

