APP_NAME=auth-bot
BUILD_DIR=./bin

run:
	@echo "Running auth bot..."
	@CONFIG_PATH=../../credentials/appsettings.json go run ./cmd

build:
	@echo "Building $(APP_NAME)..."
	@mkdir -p $(BUILD_DIR)
	@go build -o $(BUILD_DIR)/bot ./cmd

fmt:
	@go fmt ./...

deps:
	@go mod tidy

test:
	@echo "Running bot tests..."
	@go test -v ./cmd/bot_tests/...

