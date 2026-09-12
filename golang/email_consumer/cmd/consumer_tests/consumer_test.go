package consumertests

import (
	"context"
	"encoding/json"
	"testing"

	insertemailqueue "email_consumer/internal/application/email_queue/insert_email_queue"

	emaildb "qn.expenditure/emaildb/generated"

	. "qn.expenditure/shared/app"

	"github.com/stretchr/testify/assert"
	"github.com/stretchr/testify/require"
)

func newTestHandler() Handler[insertemailqueue.Command, insertemailqueue.Result] {
	return insertemailqueue.NewHandler(testQueries)
}

func TestConsumer(t *testing.T) {
	t.Run("ValidMessage", consumerValidMessage)
	t.Run("UnknownEmailType", consumerUnknownEmailType)
}

func consumerValidMessage(t *testing.T) {
	t.Helper()

	data, _ := json.Marshal(map[string]string{"FirstName": "John", "ConfirmURL": "http://example.com/confirm"})
	_, err := newTestHandler().Handle(context.Background(), insertemailqueue.Command{
		UserId:      testUserID,
		ToEmail:     "john@example.com",
		EmailTypeId: "activate_account",
		HtmlData:    string(data),
	})
	require.NoError(t, err)
	require.Equal(t, 1, countEmailQueue(t))

	var row emaildb.EmailQueue
	err = testDB.QueryRowContext(context.Background(),
		`SELECT "Id", "EmailTypeId", "HtmlData", "Status", "Retry", "UserId", "ToEmail" FROM "EmailQueue" LIMIT 1`,
	).Scan(&row.Id, &row.EmailTypeId, &row.HtmlData, &row.Status, &row.Retry, &row.UserId, &row.ToEmail)
	require.NoError(t, err)

	assert.Equal(t, "activate_account", row.EmailTypeId)
	assert.Equal(t, testUserID, row.UserId)
	assert.Equal(t, "john@example.com", row.ToEmail)
	assert.Equal(t, emaildb.EmailQueueStatusNew, row.Status)
	assert.Equal(t, int32(0), row.Retry)

	var parsed map[string]string
	require.NoError(t, json.Unmarshal([]byte(row.HtmlData), &parsed))
	assert.Equal(t, "John", parsed["FirstName"])
	assert.Equal(t, "http://example.com/confirm", parsed["ConfirmURL"])
}

func consumerUnknownEmailType(t *testing.T) {
	t.Helper()
	countBefore := countEmailQueue(t)

	data, _ := json.Marshal(map[string]string{})
	_, err := newTestHandler().Handle(context.Background(), insertemailqueue.Command{
		UserId:      testUserID,
		ToEmail:     "john@example.com",
		EmailTypeId: "nonexistent_type",
		HtmlData:    string(data),
	})
	require.Error(t, err)
	assert.Equal(t, countBefore, countEmailQueue(t), "no row should be inserted for unknown email type")
}
