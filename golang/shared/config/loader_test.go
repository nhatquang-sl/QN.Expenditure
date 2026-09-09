package config

import (
	"os"
	"path/filepath"
	"testing"
)

type sampleConfig struct {
	Name string `json:"name"`
	Port int    `json:"port"`
}

func TestLoadJSON(t *testing.T) {
	tempDir := t.TempDir()
	path := filepath.Join(tempDir, "appsettings.json")
	if err := os.WriteFile(path, []byte(`{"name":"demo","port":8080}`), 0o600); err != nil {
		t.Fatal(err)
	}

	cfg := LoadJSON[sampleConfig](path)
	if cfg.Name != "demo" {
		t.Fatalf("expected demo, got %q", cfg.Name)
	}
	if cfg.Port != 8080 {
		t.Fatalf("expected port 8080, got %d", cfg.Port)
	}
}
