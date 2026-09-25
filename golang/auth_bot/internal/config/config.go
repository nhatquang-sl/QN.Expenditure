package config

import (
	"os"

	sharedconfig "qn.expenditure/shared/config"
)

const defaultConfigPath = "credentials/appsettings.json"

type AuthBotConfig struct {
	AuthBaseUrl             string
	BotPassword             string
	PGAuthConnection        string
	RegisterIntervalSeconds int
	LoginIntervalSeconds    int
}

type Config struct {
	Application struct {
		Version string
	}
	AuthBot AuthBotConfig
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
		if v := os.Getenv("AUTH_BASE_URL"); v != "" {
			cfg.AuthBot.AuthBaseUrl = v
		}
		if v := os.Getenv("PG_AUTH_CONNECTION"); v != "" {
			cfg.AuthBot.PGAuthConnection = v
		}
	})
}
