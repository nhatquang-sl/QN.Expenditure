POSTGRESQL_URL := postgres://admin:123456x%40X@localhost:5432/email-test?sslmode=disable

migrate-create:
	@migrate create -ext sql -dir internal/database/migrations -seq $(n)

migrate-up:
	@echo "Running email database migrations..."
	@migrate -database '$(POSTGRESQL_URL)' -path internal/database/migrations up

migrate-down:
	@echo "Reverting email database migrations..."
	@migrate -database '$(POSTGRESQL_URL)' -path internal/database/migrations down

sqlc-generate:
	@echo "Regenerating sqlc types..."
	@sqlc generate -f internal/database/sqlc.yaml
