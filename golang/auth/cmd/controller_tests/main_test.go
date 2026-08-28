package controllertests

import (
	"auth/cmd/controllers"
	"auth/cmd/middleware"
	. "auth/internal/config"
	"auth/internal/database"
	"auth/internal/database/generated"
	jwtservice "auth/internal/services/jwt"
	"context"
	"io"
	"log/slog"
	"net/http"
	"os"
	"path/filepath"
	"runtime"
	"testing"
	"time"

	migrate "github.com/golang-migrate/migrate/v4"
	migratepostgres "github.com/golang-migrate/migrate/v4/database/postgres"
	_ "github.com/golang-migrate/migrate/v4/source/file"
	"github.com/testcontainers/testcontainers-go"
	"github.com/testcontainers/testcontainers-go/modules/postgres"
	"github.com/testcontainers/testcontainers-go/wait"
)

var testJwtService = jwtservice.NewService(JwtConfig{
	Issuer:                "test",
	Audience:              "test",
	AccessTokenSecretKey:  "test-access-secret",
	RefreshTokenSecretKey: "test-refresh-secret",
})

const (
	dbName     = "auth"
	dbUser     = "postgres"
	dbPassword = "postgres"
)

// testQueries is shared across all tests in this package.
var testQueries *generated.Queries

func TestMain(m *testing.M) {
	ctx := context.Background()
	q, cf, err := createTestDB(ctx)
	if err != nil {
		panic(err)
	}
	testQueries = q

	code := m.Run()

	if err := cf(ctx); err != nil {
		panic(err)
	}

	os.Exit(code)
}

// newTestHandler builds an AuthController mux wrapped with the Recover middleware,
// matching the same setup used in production.
func newTestHandler() http.Handler {
	mux := http.NewServeMux()
	logger := slog.New(slog.NewTextHandler(io.Discard, nil))
	controllers.NewAuthController(mux, testQueries, testJwtService, nil, logger, "test-secret", "http://localhost", true)
	return middleware.Recover(logger, mux)
}

type DBCleanupFunc func(ctx context.Context) error

func createPostgresContainer(ctx context.Context) (*postgres.PostgresContainer, error) {
	postgresContainer, err := postgres.Run(ctx,
		"postgres:16-alpine",
		postgres.WithDatabase(dbName),
		postgres.WithUsername(dbUser),
		postgres.WithPassword(dbPassword),
		testcontainers.WithWaitStrategy(
			wait.ForLog("database system is ready to accept connections").
				WithOccurrence(2).
				WithStartupTimeout(30*time.Second),
		),
	)
	if err != nil {
		return nil, err
	}
	return postgresContainer, nil
}

func createTestDB(ctx context.Context) (*generated.Queries, DBCleanupFunc, error) {
	ctr, err := createPostgresContainer(ctx)
	if err != nil {
		return nil, nil, err
	}

	// Use ConnectionString instead of building it manually: testcontainers maps
	// the container's port 5432 to a random ephemeral host port, so we let the
	// container tell us the correct address rather than hardcoding port 5432.
	// The username, password, and dbname are embedded automatically from the
	// values passed to postgres.WithUsername/WithPassword/WithDatabase above.
	connStr, err := ctr.ConnectionString(ctx, "sslmode=disable")
	if err != nil {
		return nil, nil, err
	}

	db, queries, err := database.ConnectDB(connStr)
	if err != nil {
		return nil, nil, err
	}

	// Run migrations using the open *sql.DB so we don't need to re-parse the
	// connection string into a URL format.
	driver, err := migratepostgres.WithInstance(db, &migratepostgres.Config{})
	if err != nil {
		return nil, nil, err
	}
	_, filename, _, _ := runtime.Caller(0)
	migrationsPath := "file://" + filepath.Join(filepath.Dir(filename), "../../internal/database/migrations")
	m, err := migrate.NewWithDatabaseInstance(migrationsPath, "postgres", driver)
	if err != nil {
		return nil, nil, err
	}
	if err := m.Up(); err != nil && err != migrate.ErrNoChange {
		return nil, nil, err
	}

	cleanup := func(ctx context.Context) error {
		dbErr := db.Close()
		ctrErr := testcontainers.TerminateContainer(ctr)
		if dbErr != nil {
			return dbErr
		}
		return ctrErr
	}
	return queries, cleanup, nil
}
