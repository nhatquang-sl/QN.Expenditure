package consumertests

import (
	"context"
	"database/sql"
	"io"
	"log/slog"
	"os"
	"path/filepath"
	"runtime"
	"testing"
	"time"

	emaildb "qn.expenditure/emaildb/generated"
	shareddb "qn.expenditure/shared/database"

	migrate "github.com/golang-migrate/migrate/v4"
	migratepostgres "github.com/golang-migrate/migrate/v4/database/postgres"
	_ "github.com/golang-migrate/migrate/v4/source/file"
	"github.com/testcontainers/testcontainers-go"
	"github.com/testcontainers/testcontainers-go/modules/postgres"
	"github.com/testcontainers/testcontainers-go/wait"
)

const (
	dbName     = "email"
	dbUser     = "postgres"
	dbPassword = "postgres"
	// testUserID is a stable placeholder — email DB has no Users FK.
	testUserID = "00000000-0000-0000-0000-000000000001"
)

var (
	testDB      *sql.DB
	testQueries *emaildb.Queries
	testLogger  = slog.New(slog.NewTextHandler(io.Discard, nil))
)

func TestMain(m *testing.M) {
	ctx := context.Background()

	db, q, cleanup, err := createTestDB(ctx)
	if err != nil {
		panic(err)
	}
	testDB = db
	testQueries = q

	code := m.Run()

	if err := cleanup(ctx); err != nil {
		panic(err)
	}

	os.Exit(code)
}

type cleanupFunc func(ctx context.Context) error

func createTestDB(ctx context.Context) (*sql.DB, *emaildb.Queries, cleanupFunc, error) {
	ctr, err := postgres.Run(ctx,
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
		return nil, nil, nil, err
	}

	connStr, err := ctr.ConnectionString(ctx, "sslmode=disable")
	if err != nil {
		return nil, nil, nil, err
	}

	db, err := shareddb.OpenPostgres(connStr)
	if err != nil {
		return nil, nil, nil, err
	}

	driver, err := migratepostgres.WithInstance(db, &migratepostgres.Config{})
	if err != nil {
		return nil, nil, nil, err
	}
	_, filename, _, _ := runtime.Caller(0)
	migrationsPath := "file://" + filepath.Join(filepath.Dir(filename), "../../../emaildb/internal/database/migrations")
	mg, err := migrate.NewWithDatabaseInstance(migrationsPath, "postgres", driver)
	if err != nil {
		return nil, nil, nil, err
	}
	if err := mg.Up(); err != nil && err != migrate.ErrNoChange {
		return nil, nil, nil, err
	}

	cleanup := func(ctx context.Context) error {
		dbErr := db.Close()
		ctrErr := testcontainers.TerminateContainer(ctr)
		if dbErr != nil {
			return dbErr
		}
		return ctrErr
	}
	return db, emaildb.New(db), cleanup, nil
}

// countEmailQueue returns the total number of rows in EmailQueue.
func countEmailQueue(t *testing.T) int {
	t.Helper()
	var count int
	err := testDB.QueryRowContext(context.Background(), `SELECT COUNT(*) FROM "EmailQueue"`).Scan(&count)
	if err != nil {
		t.Fatalf("count email queue: %v", err)
	}
	return count
}
