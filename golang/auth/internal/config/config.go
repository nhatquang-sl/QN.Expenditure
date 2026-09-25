package config

import (
	"os"

	sharedconfig "qn.expenditure/shared/config"
	. "qn.expenditure/shared/messaging"
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
	Jwt          JwtConfig
	Redis        RedisConfig
	RabbitMq     RabbitMqConfig
	TLSCertPath  string
	TLSKeyPath   string
	GoServerPort int
}

func LoadJSONConfig() Config {
	path := os.Getenv("CONFIG_PATH")
	if path == "" {
		path = defaultConfigPath
	}

	cfg := sharedconfig.LoadJSON(path, func(cfg *Config) {
		if cfg.TLSCertPath == "" {
			cfg.TLSCertPath = defaultTLSCertPath
		}
		if cfg.TLSKeyPath == "" {
			cfg.TLSKeyPath = defaultTLSKeyPath
		}
		if v := os.Getenv("PG_AUTH_CONNECTION"); v != "" {
			cfg.ConnectionStrings.PGAuth = v
		}
		if v := os.Getenv("VERSION"); v != "" {
			cfg.Application.Version = v
		}
		if v := os.Getenv("APPLICATION_ENDPOINT"); v != "" {
			cfg.Application.Endpoint = v
		}
		if v := os.Getenv("REDIS_ADDR"); v != "" {
			cfg.Redis.Addr = v
		}
		if v := os.Getenv("REDIS_PASSWORD"); v != "" {
			cfg.Redis.Password = v
		}
		if v := os.Getenv("RABBITMQ_HOST"); v != "" {
			cfg.RabbitMq.Host = v
		}
		if v := os.Getenv("RABBITMQ_USERNAME"); v != "" {
			cfg.RabbitMq.Username = v
		}
		if v := os.Getenv("RABBITMQ_PASSWORD"); v != "" {
			cfg.RabbitMq.Password = v
		}
	})

	return cfg
}
