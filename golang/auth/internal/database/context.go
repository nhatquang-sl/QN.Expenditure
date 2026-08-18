package database

import (
	"database/sql"
	"log"

	dbsqlc "auth/internal/database/generated"
	_ "github.com/lib/pq"
)

func ConnectDB(connectionString string) *dbsqlc.Queries {
	conn, err := sql.Open("postgres", connectionString)
	if err != nil {
		log.Fatalf("Failed to open database connection: %v", err)
	}

	if err = conn.Ping(); err != nil {
		log.Fatalf("Failed to ping database: %v", err)
	}

	log.Println("Connected to db")
	return dbsqlc.New(conn)
}
