package config

import "os"

// Config holds runtime configuration for the auth service.
// All values are loaded exclusively from environment variables.
type Config struct {
	Port        string
	AppEnv      string
	Version     string
	TLSCertPath string
	TLSKeyPath  string
}

// Load reads all config from environment variables.
// Set APP_ENV=Development or APP_ENV=Production (default: Production).
func Load() Config {
	env := os.Getenv("APP_ENV")
	if env == "" {
		env = "Development"
	}

	port := os.Getenv("PORT")
	if port == "" {
		port = "5002"
	}

	version := os.Getenv("VERSION")
	if version == "" {
		version = "0.0.1"
	}

	return Config{
		AppEnv:      env,
		Port:        port,
		Version:     version,
		TLSCertPath: os.Getenv("TLS_CERT_PATH"),
		TLSKeyPath:  os.Getenv("TLS_KEY_PATH"),
	}
}
