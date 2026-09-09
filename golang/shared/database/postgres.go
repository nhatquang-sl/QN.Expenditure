package database

import (
	"database/sql"
	"log"

	"github.com/XSAM/otelsql"
	_ "github.com/lib/pq"
	semconv "go.opentelemetry.io/otel/semconv/v1.26.0"
)

func OpenPostgres(connectionString string) (*sql.DB, error) {
	conn, err := otelsql.Open("postgres", connectionString,
		otelsql.WithAttributes(semconv.DBSystemPostgreSQL),
		otelsql.WithSpanOptions(otelsql.SpanOptions{
			OmitConnResetSession: true,
		}),
	)
	if err != nil {
		return nil, err
	}

	if err = conn.Ping(); err != nil {
		return nil, err
	}

	log.Println("Connected to db")
	return conn, nil
}
