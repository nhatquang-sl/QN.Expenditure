package risk

import (
	"testing"
)

// baseCtx returns a RiskContext that passes all hard limits.
// Individual tests override one field at a time to trigger each rejection.
func baseCtx() RiskContext {
	return RiskContext{
		Candidate: CandidateInfo{
			Id:                "cand-1",
			Symbol:            "BTCUSDT",
			Side:              "BUY",
			EntryPrice:        50000,
			EstimatedExposure: 100,
		},
		Portfolio: PortfolioInfo{
			TotalExposure: 1000,
			AvailableCash: 500,
			OpenPositions: 2,
			SymbolExposure: map[string]float64{
				"BTCUSDT": 500,
			},
		},
		TradingHistory: TradingHistoryContext{
			DailyPnL:          50,
			ConsecutiveLosses: 1,
		},
		Limits: DefaultLimits(),
	}
}

func TestHardRiskGate_Approve(t *testing.T) {
	gate := NewHardRiskGate()
	decision := gate.Evaluate(baseCtx())

	if decision.Decision != DecisionApprove {
		t.Errorf("expected APPROVE, got %s (reasons: %v)", decision.Decision, decision.Reasons)
	}
	if len(decision.Reasons) != 0 {
		t.Errorf("expected no reasons on approve, got %v", decision.Reasons)
	}
	if decision.PositionSize != baseCtx().Candidate.EstimatedExposure {
		t.Errorf("expected PositionSize %v, got %v", baseCtx().Candidate.EstimatedExposure, decision.PositionSize)
	}
}

func TestHardRiskGate_Reject_DailyLossLimitExceeded(t *testing.T) {
	ctx := baseCtx()
	ctx.TradingHistory.DailyPnL = -300 // equals -MaxDailyLossUsdt (300)

	gate := NewHardRiskGate()
	decision := gate.Evaluate(ctx)

	if decision.Decision != DecisionReject {
		t.Errorf("expected REJECT, got %s", decision.Decision)
	}
	if !contains(decision.Reasons, ReasonDailyLossLimitExceeded) {
		t.Errorf("expected reason %s, got %v", ReasonDailyLossLimitExceeded, decision.Reasons)
	}
}

func TestHardRiskGate_Reject_PortfolioExposureExceeded(t *testing.T) {
	ctx := baseCtx()
	// TotalExposure(4950) + EstimatedExposure(100) = 5050 > MaxPortfolioExposureUsdt(5000)
	ctx.Portfolio.TotalExposure = 4950

	gate := NewHardRiskGate()
	decision := gate.Evaluate(ctx)

	if decision.Decision != DecisionReject {
		t.Errorf("expected REJECT, got %s", decision.Decision)
	}
	if !contains(decision.Reasons, ReasonPortfolioExposureExceeded) {
		t.Errorf("expected reason %s, got %v", ReasonPortfolioExposureExceeded, decision.Reasons)
	}
}

func TestHardRiskGate_Reject_SymbolExposureExceeded(t *testing.T) {
	ctx := baseCtx()
	// SymbolExposure(1950) + EstimatedExposure(100) = 2050 > MaxSymbolExposureUsdt(2000)
	ctx.Portfolio.SymbolExposure["BTCUSDT"] = 1950

	gate := NewHardRiskGate()
	decision := gate.Evaluate(ctx)

	if decision.Decision != DecisionReject {
		t.Errorf("expected REJECT, got %s", decision.Decision)
	}
	if !contains(decision.Reasons, ReasonSymbolExposureExceeded) {
		t.Errorf("expected reason %s, got %v", ReasonSymbolExposureExceeded, decision.Reasons)
	}
}

func TestHardRiskGate_Reject_MaxOpenPositionsReached(t *testing.T) {
	ctx := baseCtx()
	ctx.Portfolio.OpenPositions = 5 // equals MaxOpenPositions(5)

	gate := NewHardRiskGate()
	decision := gate.Evaluate(ctx)

	if decision.Decision != DecisionReject {
		t.Errorf("expected REJECT, got %s", decision.Decision)
	}
	if !contains(decision.Reasons, ReasonMaxOpenPositionsReached) {
		t.Errorf("expected reason %s, got %v", ReasonMaxOpenPositionsReached, decision.Reasons)
	}
}

func TestHardRiskGate_Reject_ConsecutiveLossesExceeded(t *testing.T) {
	ctx := baseCtx()
	ctx.TradingHistory.ConsecutiveLosses = 3 // equals MaxConsecutiveLosses(3)

	gate := NewHardRiskGate()
	decision := gate.Evaluate(ctx)

	if decision.Decision != DecisionReject {
		t.Errorf("expected REJECT, got %s", decision.Decision)
	}
	if !contains(decision.Reasons, ReasonConsecutiveLossesExceeded) {
		t.Errorf("expected reason %s, got %v", ReasonConsecutiveLossesExceeded, decision.Reasons)
	}
}

func TestHardRiskGate_Reject_InsufficientBalance(t *testing.T) {
	ctx := baseCtx()
	ctx.Portfolio.AvailableCash = 5 // less than MinOrderSizeUsdt(10)

	gate := NewHardRiskGate()
	decision := gate.Evaluate(ctx)

	if decision.Decision != DecisionReject {
		t.Errorf("expected REJECT, got %s", decision.Decision)
	}
	if !contains(decision.Reasons, ReasonInsufficientBalance) {
		t.Errorf("expected reason %s, got %v", ReasonInsufficientBalance, decision.Reasons)
	}
}

func contains(slice []string, s string) bool {
	for _, v := range slice {
		if v == s {
			return true
		}
	}
	return false
}
