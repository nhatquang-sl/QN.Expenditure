package database

import (
	"database/sql"
	"log"

	dbsqlc "auth/internal/database/generated"
	_ "github.com/lib/pq"
)

func ConnectDB(connectionString string) (*sql.DB, *dbsqlc.Queries, error) {
	conn, err := sql.Open("postgres", connectionString)
	if err != nil {
		return nil, nil, err
	}

	if err = conn.Ping(); err != nil {
		return nil, nil, err
	}

	log.Println("Connected to db")
	return conn, dbsqlc.New(conn), nil
}
