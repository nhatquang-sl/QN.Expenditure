package config

import (
	"os"

	sharedconfig "qn.expenditure/shared/config"
)

const defaultConfigPath = "credentials/appsettings.json"

type EmailWorkerConfig struct {
	BatchSize       int
	IntervalSeconds int
}

type Config struct {
	ConnectionStrings struct {
		PGEmail string
	}
	Email struct {
		ApiKeyPublic  string
		ApiKeyPrivate string
		FromEmail     string
	}
	EmailWorker EmailWorkerConfig
}

func LoadJSONConfig() Config {
	path := os.Getenv("CONFIG_PATH")
	if path == "" {
		path = defaultConfigPath
	}

	return sharedconfig.LoadJSON(path, func(cfg *Config) {
		if v := os.Getenv("PG_EMAIL_CONNECTION"); v != "" {
			cfg.ConnectionStrings.PGEmail = v
		}
	})
}
