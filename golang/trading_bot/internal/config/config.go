package config

import (
	"os"

	"trading_bot/internal/indicator"

	sharedconfig "qn.expenditure/shared/config"
)

type AppConfig struct {
	Application struct {
		Version string
	}
	ConnectionStrings struct {
		PGTrading string
	}
	TradingBot struct {
		Symbols             []string
		Timeframes          []string
		PollIntervalSeconds int
		Indicator           struct {
			RSIPeriod      int
			BBPeriod       int
			BBMultiplier   float64
			RSISlopePeriod int
			Divergence     struct {
				PeakThreshold   float64
				TroughThreshold float64
				LookbackCandles int
			}
		}
	}
}

func LoadJSONConfig() AppConfig {
	path := os.Getenv("CONFIG_PATH")
	if path == "" {
		path = "credentials/appsettings.json"
	}

	return sharedconfig.LoadJSON(path, func(cfg *AppConfig) {
		if v := os.Getenv("VERSION"); v != "" {
			cfg.Application.Version = v
		}
		if v := os.Getenv("PG_TRADING_CONNECTION"); v != "" {
			cfg.ConnectionStrings.PGTrading = v
		}
		if len(cfg.TradingBot.Symbols) == 0 {
			cfg.TradingBot.Symbols = []string{"BTCUSDT"}
		}
		if len(cfg.TradingBot.Timeframes) == 0 {
			cfg.TradingBot.Timeframes = []string{"1hour", "4hour"}
		}
		if cfg.TradingBot.PollIntervalSeconds == 0 {
			cfg.TradingBot.PollIntervalSeconds = 300
		}
		if cfg.TradingBot.Indicator.RSIPeriod == 0 {
			cfg.TradingBot.Indicator.RSIPeriod = 14
		}
		if cfg.TradingBot.Indicator.BBPeriod == 0 {
			cfg.TradingBot.Indicator.BBPeriod = 20
		}
		if cfg.TradingBot.Indicator.BBMultiplier == 0 {
			cfg.TradingBot.Indicator.BBMultiplier = 2.0
		}
		if cfg.TradingBot.Indicator.RSISlopePeriod == 0 {
			cfg.TradingBot.Indicator.RSISlopePeriod = 1
		}
		if cfg.TradingBot.Indicator.Divergence.PeakThreshold == 0 {
			cfg.TradingBot.Indicator.Divergence.PeakThreshold = 68.0
		}
		if cfg.TradingBot.Indicator.Divergence.TroughThreshold == 0 {
			cfg.TradingBot.Indicator.Divergence.TroughThreshold = 32.0
		}
		if cfg.TradingBot.Indicator.Divergence.LookbackCandles == 0 {
			cfg.TradingBot.Indicator.Divergence.LookbackCandles = 20
		}
	})
}

func (c AppConfig) IndicatorConfig() indicator.Config {
	ind := c.TradingBot.Indicator
	return indicator.Config{
		RSIPeriod:      ind.RSIPeriod,
		BBPeriod:       ind.BBPeriod,
		BBMultiplier:   ind.BBMultiplier,
		RSISlopePeriod: ind.RSISlopePeriod,
		Divergence: indicator.DivergenceConfig{
			PeakThreshold:   ind.Divergence.PeakThreshold,
			TroughThreshold: ind.Divergence.TroughThreshold,
			LookbackCandles: ind.Divergence.LookbackCandles,
		},
	}
}
