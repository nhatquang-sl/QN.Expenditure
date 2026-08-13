package config

import (
	_ "embed"
	"encoding/json"
)

// Embeds appsettings.json into the binary at compile time, resolved relative to this file's directory
//
//go:embed appsettings.json
var configData []byte

type Config struct {
	ConnectionStrings struct {
		AuthConnection string
		CexConnection  string
	}
	CorsOrigins string
	Application struct {
		Version  string
		Endpoint string
	}
	Email struct {
		ApiKeyPublic  string
		ApiKeyPrivate string
		FromEmail     string
	}
	Jwt struct {
		Issuer                string
		Audience              string
		AccessTokenSecretKey  string
		RefreshTokenSecretKey string
	}
	RabbitMq struct {
		Host     string
		Username string
		Password string
	}
}

func LoadJSONConfig() (Config, error) {
	var cfg Config
	// Unmarshal parses the JSON bytes and maps values into the Config struct fields
	if err := json.Unmarshal(configData, &cfg); err != nil {
		return Config{}, err
	}
	return cfg, nil
}
