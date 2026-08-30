package config

import (
	"encoding/json"
	"os"
)

// Config path is resolved at runtime. Override with CONFIG_PATH env var.
// Default: "credentials/appsettings.json" (relative to the working directory,
// which is /app in Docker — matching the mounted credentials volume).
const defaultConfigPath = "credentials/appsettings.json"
const defaultTLSCertPath = "../../credentials/qex/certs/localhost.pem"
const defaultTLSKeyPath = "../../credentials/qex/certs/localhost-key.pem"

type JwtConfig struct {
	Issuer                string
	Audience              string
	AccessTokenSecretKey  string
	RefreshTokenSecretKey string
}

type RedisConfig struct {
	Addr              string
	Password          string
	DefaultTTLSeconds int
}

type Config struct {
	ConnectionStrings struct {
		AuthConnection string
		CexConnection  string
		PGAuth         string
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
	Jwt      JwtConfig
	Redis    RedisConfig
	RabbitMq struct {
		Host     string
		Username string
		Password string
	}
	TLSCertPath  string
	TLSKeyPath   string
	GoServerPort int
}

func LoadJSONConfig() Config {
	path := os.Getenv("CONFIG_PATH")
	if path == "" {
		path = defaultConfigPath
	}

	data, err := os.ReadFile(path)
	if err != nil {
		panic("failed to read config file: " + err.Error())
	}

	var cfg Config
	if err := json.Unmarshal(data, &cfg); err != nil {
		panic("failed to parse config file: " + err.Error())
	}

	if cfg.TLSCertPath == "" {
		cfg.TLSCertPath = defaultTLSCertPath
	}
	if cfg.TLSKeyPath == "" {
		cfg.TLSKeyPath = defaultTLSKeyPath
	}

	if v := os.Getenv("PG_AUTH_CONNECTION"); v != "" {
		cfg.ConnectionStrings.PGAuth = v
	}
	if v := os.Getenv("REDIS_ADDR"); v != "" {
		cfg.Redis.Addr = v
	}
	if v := os.Getenv("REDIS_PASSWORD"); v != "" {
		cfg.Redis.Password = v
	}

	return cfg
}
