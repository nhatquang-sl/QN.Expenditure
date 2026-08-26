APP_NAME=go-auth-api
BIN_NAME=gobin # name of the ouptut binary when we run our build command
BUILD_DIR=./bin # where the binary will be outputted to
GO_FILES=$(shell find . -type f -name '*.go' -not -path "./vendor/*")

dev:
	@echo "Running $(APP_NAME)..."
	@CONFIG_PATH=../../credentials/appsettings.json air

run:
	@echo "Running $(APP_NAME)..."
	@CONFIG_PATH=../../credentials/appsettings.json go run ./cmd/main.go

deps:
	@echo "Installing dependencies..."
	@go mod tidy

fmt:
	@echo "Formatting code..."
	@go fmt ./...

build:
	@echo "Building $(APP_NAME)..."
	@mkdir -p $(BUILD_DIR)
	@go build -o $(BUILD_DIR)/$(BIN_NAME) cmd/main.go

clean:
	@echo "Cleaning build artifacts..."
	@rm -rf $(BUILD_DIR)

stop:
	@echo "Stopping $(APP_NAME)..."
	@pkill -f $(BIN_NAME) || echo "$(APP_NAME) is not running."

docker:
	@echo "Building Docker image for $(APP_NAME)..."
	@docker build -t $(APP_NAME):latest .
	@docker run -p 5002:5002 --name $(APP_NAME) $(APP_NAME):latest

test:
	@echo "Running tests..."
	@go test -v ./cmd/controller_tests/...

migrate-create:
	@migrate create -ext sql -dir internal/database/migrations -seq $(n)

migrate-up:
	@echo "Running database migrations..."
	@migrate -database '$(POSTGRESQL_URL)' -path internal/database/migrations up

migrate-down:
	@echo "Reverting database migrations..."
	@migrate -database '$(POSTGRESQL_URL)' -path internal/database/migrations down