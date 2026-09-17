package database

import (
	"context"
	"database/sql"
	"database/sql/driver"
	"log"
	"strings"
	"time"

	"github.com/XSAM/otelsql"
	_ "github.com/lib/pq"
	"go.opentelemetry.io/otel/attribute"
	semconv "go.opentelemetry.io/otel/semconv/v1.26.0"
)

// sqlcOperationName extracts the operation name from sqlc-generated SQL.
// sqlc always prepends: -- name: <OpName> :one/:exec/etc.
func sqlcOperationName(query string) string {
	const prefix = "-- name: "
	if !strings.HasPrefix(query, prefix) {
		return ""
	}
	rest := query[len(prefix):]
	if i := strings.IndexByte(rest, ' '); i > 0 {
		return rest[:i]
	}
	return ""
}

func OpenPostgres(connectionString string) (*sql.DB, error) {
	conn, err := otelsql.Open("postgres", connectionString,
		otelsql.WithAttributes(semconv.DBSystemPostgreSQL),
		otelsql.WithSpanOptions(otelsql.SpanOptions{
			OmitConnResetSession: true,
		}),
		otelsql.WithAttributesGetter(func(_ context.Context, _ otelsql.Method, query string, _ []driver.NamedValue) []attribute.KeyValue {
			if name := sqlcOperationName(query); name != "" {
				return []attribute.KeyValue{attribute.String("db.sqlc.operation", name)}
			}
			return nil
		}),
	)
	if err != nil {
		return nil, err
	}

	// Connection pool limits — shared Postgres has max_connections=100 across 4 services.
	// 4 services × 25 = 100 theoretical max; in practice they never all peak simultaneously.
	//
	// SetMaxOpenConns  25   caps connections per service; prevents unbounded bursts under load
	// SetMaxIdleConns   5   keeps a small warm pool without holding all 25 open when quiet
	// ConnMaxLifetime  30m  recycles connections periodically; prevents silent stale connections
	// ConnMaxIdleTime   5m  releases connections when load drops (important for bursty bot traffic)
	conn.SetMaxOpenConns(25)
	conn.SetMaxIdleConns(5)
	conn.SetConnMaxLifetime(30 * time.Minute)
	conn.SetConnMaxIdleTime(5 * time.Minute)

	if err = conn.Ping(); err != nil {
		return nil, err
	}

	log.Println("Connected to db")
	return conn, nil
}
