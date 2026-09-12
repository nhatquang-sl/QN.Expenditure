package workertests

import (
	"context"
	"database/sql"
	"errors"
	"testing"
	"time"

	"email_worker/internal/worker"

	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
)

// fakeEmailSender is an injectable fake that can be configured to succeed or fail.
type fakeEmailSender struct {
	err error
}

func (f *fakeEmailSender) Send(_ context.Context, _, _, _ string) error {
	return f.err
}

func newTestWorker(sender worker.EmailSender) *worker.Worker {
	return worker.New(testDB, testQueries, sender, testLogger)
}

func TestWorker(t *testing.T) {
	t.Run("Success", workerSuccess)
	t.Run("FirstFailure", workerFirstFailure)
	t.Run("ThirdFailure", workerThirdFailure)
}

// workerSuccess: a new row is processed → status becomes 'sent'.
func workerSuccess(t *testing.T) {
	t.Helper()
	id := seedEmailQueue(t, 0, sql.NullTime{})

	newTestWorker(&fakeEmailSender{}).Tick(context.Background(), 10)

	row := getEmailQueueRow(t, id)
	assert.Equal(t, "sent", string(row.Status))
	assert.Equal(t, int32(0), row.Retry)
	assert.False(t, row.NextRetryAt.Valid)
}

// workerFirstFailure: send fails → status='fail', retry=1, NextRetryAt is set.
func workerFirstFailure(t *testing.T) {
	t.Helper()
	id := seedEmailQueue(t, 0, sql.NullTime{})

	newTestWorker(&fakeEmailSender{err: errors.New("transient error")}).Tick(context.Background(), 10)

	row := getEmailQueueRow(t, id)
	assert.Equal(t, "fail", string(row.Status))
	assert.Equal(t, int32(1), row.Retry)
	require.True(t, row.NextRetryAt.Valid, "NextRetryAt should be set after first failure")
	assert.WithinDuration(t, time.Now().Add(2*time.Minute), row.NextRetryAt.Time, 5*time.Second)
}

// workerThirdFailure: a row already at retry=2 fails again → retry=3, status='fail', no NextRetryAt.
func workerThirdFailure(t *testing.T) {
	t.Helper()
	// NextRetryAt in the past so the eligible query picks it up.
	pastTime := sql.NullTime{Time: time.Now().Add(-time.Minute), Valid: true}
	id := seedEmailQueue(t, 2, pastTime)

	newTestWorker(&fakeEmailSender{err: errors.New("transient error")}).Tick(context.Background(), 10)

	row := getEmailQueueRow(t, id)
	assert.Equal(t, "fail", string(row.Status))
	assert.Equal(t, int32(3), row.Retry)
	assert.False(t, row.NextRetryAt.Valid, "NextRetryAt should be NULL after permanent failure")
}
