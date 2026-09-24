package main

import (
	"encoding/json"
	"fmt"
	"os"

	"trading_bot/internal/indicator"
)

type divergenceConfigJSON struct {
	PeakThreshold   float64 `json:"peak_threshold"`
	TroughThreshold float64 `json:"trough_threshold"`
	LookbackCandles int     `json:"lookback_candles"`
}

type indicatorConfigJSON struct {
	RSIPeriod      int                  `json:"rsi_period"`
	BBPeriod       int                  `json:"bb_period"`
	BBMultiplier   float64              `json:"bb_multiplier"`
	RSISlopePeriod int                  `json:"rsi_slope_period"`
	Divergence     divergenceConfigJSON `json:"divergence"`
}

type AppConfig struct {
	Symbols             []string            `json:"symbols"`
	Timeframes          []string            `json:"timeframes"`
	PollIntervalSeconds int                 `json:"poll_interval_seconds"`
	DBDSN               string              `json:"db_dsn"`
	KuCoinBaseURL       string              `json:"kucoin_base_url"`
	Indicator           indicatorConfigJSON `json:"indicator"`
}

func (c AppConfig) indicatorConfig() indicator.Config {
	return indicator.Config{
		RSIPeriod:      c.Indicator.RSIPeriod,
		BBPeriod:       c.Indicator.BBPeriod,
		BBMultiplier:   c.Indicator.BBMultiplier,
		RSISlopePeriod: c.Indicator.RSISlopePeriod,
		Divergence: indicator.DivergenceConfig{
			PeakThreshold:   c.Indicator.Divergence.PeakThreshold,
			TroughThreshold: c.Indicator.Divergence.TroughThreshold,
			LookbackCandles: c.Indicator.Divergence.LookbackCandles,
		},
	}
}

func loadConfig() (AppConfig, error) {
	path := os.Getenv("CONFIG_PATH")
	if path == "" {
		return AppConfig{}, fmt.Errorf("CONFIG_PATH environment variable is not set")
	}
	data, err := os.ReadFile(path)
	if err != nil {
		return AppConfig{}, fmt.Errorf("reading config %q: %w", path, err)
	}
	var cfg AppConfig
	if err := json.Unmarshal(data, &cfg); err != nil {
		return AppConfig{}, fmt.Errorf("parsing config: %w", err)
	}
	cfg.applyDefaults()
	return cfg, nil
}

func (c *AppConfig) applyDefaults() {
	if len(c.Symbols) == 0 {
		c.Symbols = []string{"BTCUSDT"}
	}
	if len(c.Timeframes) == 0 {
		c.Timeframes = []string{"1hour", "4hour"}
	}
	if c.PollIntervalSeconds == 0 {
		c.PollIntervalSeconds = 300
	}
	if c.KuCoinBaseURL == "" {
		c.KuCoinBaseURL = "https://api.kucoin.com"
	}
	if c.Indicator.RSIPeriod == 0 {
		c.Indicator.RSIPeriod = 14
	}
	if c.Indicator.BBPeriod == 0 {
		c.Indicator.BBPeriod = 20
	}
	if c.Indicator.BBMultiplier == 0 {
		c.Indicator.BBMultiplier = 2.0
	}
	if c.Indicator.RSISlopePeriod == 0 {
		c.Indicator.RSISlopePeriod = 1
	}
	if c.Indicator.Divergence.PeakThreshold == 0 {
		c.Indicator.Divergence.PeakThreshold = 68.0
	}
	if c.Indicator.Divergence.TroughThreshold == 0 {
		c.Indicator.Divergence.TroughThreshold = 32.0
	}
	if c.Indicator.Divergence.LookbackCandles == 0 {
		c.Indicator.Divergence.LookbackCandles = 20
	}
}
