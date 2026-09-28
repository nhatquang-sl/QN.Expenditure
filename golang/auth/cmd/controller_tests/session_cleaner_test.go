package controllertests

import (
	"context"
	"io"
	"log/slog"
	"testing"
	"time"

	sessioncleaner "auth/internal/application/session_cleaner"

	. "qn.expenditure/shared/app"

	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
)

func TestSessionCleaner(t *testing.T) {
	t.Run("DeletesStaleRows", cleanerDeletesStaleRows)
	t.Run("RetainsRecentRows", cleanerRetainsRecentRows)
	t.Run("BatchLimit", cleanerBatchLimit)
	t.Run("NoStaleRows", cleanerNoStaleRows)
}

func newTestCleaner() Handler[sessioncleaner.Command, sessioncleaner.Result] {
	logger := slog.New(slog.NewTextHandler(io.Discard, nil))
	return sessioncleaner.NewHandler(testQueries, logger)
}

func cleanerDeletesStaleRows(t *testing.T) {
	t.Helper()
	ctx := context.Background()

	staleTime := time.Now().UTC().Add(-8 * 24 * time.Hour)
	_, err := testDB.ExecContext(ctx, `
		INSERT INTO "UserSessions" ("UserId", "IpAddress", "UserAgent", "AccessToken", "RefreshToken", "CreatedAt", "RememberMe")
		VALUES ('cleaner-stale-user', '', '', 'at-stale-1', 'rt-stale-1', $1, false),
		       ('cleaner-stale-user', '', '', 'at-stale-2', 'rt-stale-2', $1, false)
	`, staleTime)
	require.NoError(t, err)

	t.Cleanup(func() {
		testDB.ExecContext(ctx, `DELETE FROM "UserSessions" WHERE "UserId" = 'cleaner-stale-user'`)
		testDB.ExecContext(ctx, `DELETE FROM "UserSessionHistories" WHERE "UserId" = 'cleaner-stale-user'`)
	})

	newTestCleaner().Handle(ctx, sessioncleaner.Command{})

	var sessionCount int
	err = testDB.QueryRowContext(ctx, `SELECT COUNT(*) FROM "UserSessions" WHERE "UserId" = 'cleaner-stale-user'`).Scan(&sessionCount)
	require.NoError(t, err)
	assert.Equal(t, 0, sessionCount, "stale sessions should be deleted")

	var historyCount int
	err = testDB.QueryRowContext(ctx, `SELECT COUNT(*) FROM "UserSessionHistories" WHERE "UserId" = 'cleaner-stale-user'`).Scan(&historyCount)
	require.NoError(t, err)
	assert.Equal(t, 0, historyCount, "stale session histories should be deleted")
}

func cleanerRetainsRecentRows(t *testing.T) {
	t.Helper()
	ctx := context.Background()

	recentTime := time.Now().UTC().Add(-6 * 24 * time.Hour)
	_, err := testDB.ExecContext(ctx, `
		INSERT INTO "UserSessions" ("UserId", "IpAddress", "UserAgent", "AccessToken", "RefreshToken", "CreatedAt", "RememberMe")
		VALUES ('cleaner-recent-user', '', '', 'at-recent-1', 'rt-recent-1', $1, false)
	`, recentTime)
	require.NoError(t, err)

	t.Cleanup(func() {
		testDB.ExecContext(ctx, `DELETE FROM "UserSessions" WHERE "UserId" = 'cleaner-recent-user'`)
		testDB.ExecContext(ctx, `DELETE FROM "UserSessionHistories" WHERE "UserId" = 'cleaner-recent-user'`)
	})

	newTestCleaner().Handle(ctx, sessioncleaner.Command{})

	var sessionCount int
	err = testDB.QueryRowContext(ctx, `SELECT COUNT(*) FROM "UserSessions" WHERE "UserId" = 'cleaner-recent-user'`).Scan(&sessionCount)
	require.NoError(t, err)
	assert.Equal(t, 1, sessionCount, "recent sessions should be retained")

	var historyCount int
	err = testDB.QueryRowContext(ctx, `SELECT COUNT(*) FROM "UserSessionHistories" WHERE "UserId" = 'cleaner-recent-user'`).Scan(&historyCount)
	require.NoError(t, err)
	assert.Equal(t, 1, historyCount, "recent session histories should be retained")
}

func cleanerBatchLimit(t *testing.T) {
	t.Helper()
	ctx := context.Background()

	// Insert 10,001 stale sessions via server-side generate_series (fast single statement).
	// The trigger copies each row into UserSessionHistories, so 10,001 history rows are created too.
	staleTime := time.Now().UTC().Add(-8 * 24 * time.Hour)
	_, err := testDB.ExecContext(ctx, `
		INSERT INTO "UserSessions" ("UserId", "IpAddress", "UserAgent", "AccessToken", "RefreshToken", "CreatedAt", "RememberMe")
		SELECT 'cleaner-batch-user', '', '', 'at-batch-' || i::text, 'rt-batch-' || i::text, $1, false
		FROM generate_series(1, 10001) i
	`, staleTime)
	require.NoError(t, err)

	t.Cleanup(func() {
		testDB.ExecContext(ctx, `DELETE FROM "UserSessions" WHERE "UserId" = 'cleaner-batch-user'`)
		testDB.ExecContext(ctx, `DELETE FROM "UserSessionHistories" WHERE "UserId" = 'cleaner-batch-user'`)
	})

	newTestCleaner().Handle(ctx, sessioncleaner.Command{})

	var sessionCount int
	err = testDB.QueryRowContext(ctx, `SELECT COUNT(*) FROM "UserSessions" WHERE "UserId" = 'cleaner-batch-user'`).Scan(&sessionCount)
	require.NoError(t, err)
	assert.Equal(t, 1, sessionCount, "only 10,000 stale sessions deleted per run, 1 should remain")

	var historyCount int
	err = testDB.QueryRowContext(ctx, `SELECT COUNT(*) FROM "UserSessionHistories" WHERE "UserId" = 'cleaner-batch-user'`).Scan(&historyCount)
	require.NoError(t, err)
	assert.Equal(t, 1, historyCount, "only 10,000 stale history rows deleted per run, 1 should remain")
}

func cleanerNoStaleRows(t *testing.T) {
	t.Helper()
	ctx := context.Background()
	// Should succeed silently when there are no stale rows to delete
	newTestCleaner().Handle(ctx, sessioncleaner.Command{})
}
