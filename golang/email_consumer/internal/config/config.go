package config

import (
	"os"

	sharedconfig "qn.expenditure/shared/config"
	. "qn.expenditure/shared/messaging"
)

const defaultConfigPath = "credentials/appsettings.json"

type Config struct {
	ConnectionStrings struct {
		PGEmail string
	}
	Application struct {
		Version string
	}
	RabbitMq RabbitMqConfig
}

func LoadJSONConfig() Config {
	path := os.Getenv("CONFIG_PATH")
	if path == "" {
		path = defaultConfigPath
	}

	return sharedconfig.LoadJSON(path, func(cfg *Config) {
		if v := os.Getenv("VERSION"); v != "" {
			cfg.Application.Version = v
		}
		if v := os.Getenv("PG_EMAIL_CONNECTION"); v != "" {
			cfg.ConnectionStrings.PGEmail = v
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
}
