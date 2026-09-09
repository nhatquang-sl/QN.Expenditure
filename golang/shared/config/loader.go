package config

import (
	"encoding/json"
	"os"
)

func LoadJSON[T any](path string, apply ...func(*T)) T {
	var zero T
	data, err := os.ReadFile(path)
	if err != nil {
		panic("failed to read config file: " + err.Error())
	}

	if err := json.Unmarshal(data, &zero); err != nil {
		panic("failed to parse config file: " + err.Error())
	}

	for _, fn := range apply {
		if fn != nil {
			fn(&zero)
		}
	}

	return zero
}
