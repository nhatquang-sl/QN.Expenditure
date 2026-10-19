package processcandidatetrade

import (
	"testing"
	"time"

	"trading_bot/internal/database/generated"
)

func TestComputeSnapshotInput_ExposureAndEquity(t *testing.T) {
	balances := []generated.AccountBalance{
		{Id: "1", Asset: "USDT", Total: 5000, Available: 5000, Locked: 0, UpdatedAt: time.Now()},
		{Id: "2", Asset: "BTC", Total: 0.5, Available: 0.5, Locked: 0, UpdatedAt: time.Now()},
	}
	positions := []generated.Position{
		{
			Id: "p1", CandidateId: "c1", Symbol: "BTCUSDT", Asset: "BTC",
			Quantity: 0.5, AvgEntryPrice: 40000, CurrentPrice: 45000,
			UnrealizedPnl: 2500,
			CreatedAt:     time.Now(), UpdatedAt: time.Now(),
		},
	}

	snap := computeSnapshotInput(balances, positions)

	// TotalExposure = 0.5 × 45000 = 22500
	if snap.TotalExposure != 22500 {
		t.Errorf("TotalExposure: got %.2f, want 22500", snap.TotalExposure)
	}
	// Equity = AvailableCash + TotalExposure = 5000 + 22500 = 27500
	if snap.Equity != 27500 {
		t.Errorf("Equity: got %.2f, want 27500", snap.Equity)
	}
	if snap.AvailableCash != 5000 {
		t.Errorf("AvailableCash: got %.2f, want 5000", snap.AvailableCash)
	}
	if snap.UnrealizedPnl != 2500 {
		t.Errorf("UnrealizedPnl: got %.2f, want 2500", snap.UnrealizedPnl)
	}
	if snap.OpenPositions != 1 {
		t.Errorf("OpenPositions: got %d, want 1", snap.OpenPositions)
	}
}

func TestComputeSnapshotInput_MultiplePositions(t *testing.T) {
	balances := []generated.AccountBalance{
		{Id: "1", Asset: "USDT", Total: 1000, Available: 1000, Locked: 0, UpdatedAt: time.Now()},
	}
	positions := []generated.Position{
		{
			Id: "p1", CandidateId: "c1", Symbol: "BTCUSDT", Asset: "BTC",
			Quantity: 0.1, CurrentPrice: 50000, UnrealizedPnl: 500,
			CreatedAt: time.Now(), UpdatedAt: time.Now(),
		},
		{
			Id: "p2", CandidateId: "c2", Symbol: "ETHUSDT", Asset: "ETH",
			Quantity: 2.0, CurrentPrice: 3000, UnrealizedPnl: -200,
			CreatedAt: time.Now(), UpdatedAt: time.Now(),
		},
	}

	snap := computeSnapshotInput(balances, positions)

	// TotalExposure = (0.1 × 50000) + (2.0 × 3000) = 5000 + 6000 = 11000
	if snap.TotalExposure != 11000 {
		t.Errorf("TotalExposure: got %.2f, want 11000", snap.TotalExposure)
	}
	// Equity = 1000 + 11000 = 12000
	if snap.Equity != 12000 {
		t.Errorf("Equity: got %.2f, want 12000", snap.Equity)
	}
	// UnrealizedPnl = 500 + (-200) = 300
	if snap.UnrealizedPnl != 300 {
		t.Errorf("UnrealizedPnl: got %.2f, want 300", snap.UnrealizedPnl)
	}
	if snap.OpenPositions != 2 {
		t.Errorf("OpenPositions: got %d, want 2", snap.OpenPositions)
	}
}

func TestComputeSnapshotInput_EmptyPortfolio(t *testing.T) {
	snap := computeSnapshotInput(nil, nil)

	if snap.Equity != 0 {
		t.Errorf("Equity: got %.2f, want 0", snap.Equity)
	}
	if snap.AvailableCash != 0 {
		t.Errorf("AvailableCash: got %.2f, want 0", snap.AvailableCash)
	}
	if snap.TotalExposure != 0 {
		t.Errorf("TotalExposure: got %.2f, want 0", snap.TotalExposure)
	}
	if snap.OpenPositions != 0 {
		t.Errorf("OpenPositions: got %d, want 0", snap.OpenPositions)
	}
}

func TestComputeSnapshotInput_NoUSDTBalance(t *testing.T) {
	balances := []generated.AccountBalance{
		{Id: "1", Asset: "BTC", Total: 1.0, Available: 1.0, Locked: 0, UpdatedAt: time.Now()},
	}
	positions := []generated.Position{
		{
			Id: "p1", CandidateId: "c1", Symbol: "BTCUSDT", Asset: "BTC",
			Quantity: 1.0, CurrentPrice: 60000, UnrealizedPnl: 0,
			CreatedAt: time.Now(), UpdatedAt: time.Now(),
		},
	}

	snap := computeSnapshotInput(balances, positions)

	if snap.AvailableCash != 0 {
		t.Errorf("AvailableCash: got %.2f, want 0", snap.AvailableCash)
	}
	if snap.TotalExposure != 60000 {
		t.Errorf("TotalExposure: got %.2f, want 60000", snap.TotalExposure)
	}
	if snap.Equity != 60000 {
		t.Errorf("Equity: got %.2f, want 60000", snap.Equity)
	}
}
