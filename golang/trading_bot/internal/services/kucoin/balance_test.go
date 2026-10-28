package kucoin_test

import (
	"context"
	"path/filepath"
	"runtime"
	"testing"

	"trading_bot/internal/config"
	"trading_bot/internal/services/kucoin"

	"github.com/stretchr/testify/require"
)

func TestGetBalances(t *testing.T) {
	_, file, _, _ := runtime.Caller(0)
	// Resolve credentials relative to this file: up 6 dirs to QN.Expenditure/, then credentials/
	credPath := filepath.Join(filepath.Dir(file), "../../../../../credentials/appsettings.json")

	t.Setenv("CONFIG_PATH", credPath)

	cfg := config.LoadJSONConfig()
	if cfg.KuCoin.APIKey == "" {
		t.Skip("KuCoin credentials not configured in appsettings.json")
	}

	svc := kucoin.NewService(cfg.KuCoin)
	balances, err := svc.GetBalances(context.Background())
	require.NoError(t, err)

	for _, b := range balances {
		t.Logf("asset=%-8s total=%f available=%f locked=%f", b.Asset, b.Total, b.Available, b.Locked)
	}
}
