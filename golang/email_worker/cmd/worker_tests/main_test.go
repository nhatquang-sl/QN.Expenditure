package workertests

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

// seedEmailQueue inserts an EmailQueue row with the given retry count and nextRetryAt.
func seedEmailQueue(t *testing.T, retry int32, nextRetryAt sql.NullTime) int64 {
	t.Helper()
	var id int64
	err := testDB.QueryRowContext(context.Background(), `
		INSERT INTO "EmailQueue" ("EmailTypeId", "HtmlData", "UserId", "ToEmail", "Status", "Retry", "NextRetryAt")
		VALUES ('activate_account', '{"FirstName":"Jane","ConfirmURL":"http://example.com/confirm"}', $1, 'jane@example.com',
		        CASE WHEN $2 THEN 'fail'::email_queue_status ELSE 'new'::email_queue_status END,
		        $3, $4)
		RETURNING "Id"`,
		testUserID,
		retry > 0,
		retry,
		nextRetryAt,
	).Scan(&id)
	if err != nil {
		t.Fatalf("seed email queue: %v", err)
	}
	return id
}

// getEmailQueueRow fetches a single EmailQueue row by Id for assertions.
func getEmailQueueRow(t *testing.T, id int64) emaildb.EmailQueue {
	t.Helper()
	var row emaildb.EmailQueue
	err := testDB.QueryRowContext(context.Background(), `
		SELECT "Id", "EmailTypeId", "HtmlData", "Status", "Retry", "NextRetryAt", "UserId", "ToEmail", "CreatedAt", "UpdatedAt"
		FROM "EmailQueue" WHERE "Id" = $1`, id,
	).Scan(
		&row.Id, &row.EmailTypeId, &row.HtmlData, &row.Status,
		&row.Retry, &row.NextRetryAt, &row.UserId, &row.ToEmail, &row.CreatedAt, &row.UpdatedAt,
	)
	if err != nil {
		t.Fatalf("get email queue row: %v", err)
	}
	return row
}
