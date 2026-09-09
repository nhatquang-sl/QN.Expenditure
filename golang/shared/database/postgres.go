package database

import (
	"database/sql"
	"log"

	_ "github.com/lib/pq"
)

func OpenPostgres(connectionString string) (*sql.DB, error) {
	conn, err := sql.Open("postgres", connectionString)
	if err != nil {
		return nil, err
	}

	if err = conn.Ping(); err != nil {
		return nil, err
	}

	log.Println("Connected to db")
	return conn, nil
}
