APP_NAME=email-consumer
BUILD_DIR=./bin

run:
	@echo "Running email consumer..."
	@CONFIG_PATH=../../credentials/appsettings.json go run ./cmd

build:
	@echo "Building $(APP_NAME)..."
	@mkdir -p $(BUILD_DIR)
	@go build -o $(BUILD_DIR)/consumer ./cmd

fmt:
	@go fmt ./...

deps:
	@go mod tidy

test:
	@echo "Running consumer tests..."
	@go test -v ./cmd/consumer_tests/...

migrate-create:
	@migrate create -ext sql -dir internal/database/migrations -seq $(n)

migrate-up:
	@echo "Running email database migrations..."
	@migrate -database '$(PG_EMAIL_URL)' -path internal/database/migrations up

migrate-down:
	@echo "Reverting email database migrations..."
	@migrate -database '$(PG_EMAIL_URL)' -path internal/database/migrations down

sqlc-generate:
	@echo "Regenerating sqlc types..."
	@sqlc generate -f internal/database/sqlc.yaml
