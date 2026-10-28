package riskevaluate

import riskshared "trading_bot/internal/application/risk/shared"

// RiskLimits holds all configurable risk thresholds.
type RiskLimits struct {
	MaxDailyLossUsdt         float64
	MaxPortfolioExposureUsdt float64
	MaxSymbolExposureUsdt    float64
	MaxOpenPositions         int
	MaxConsecutiveLosses     int
	MinOrderSizeUsdt         float64
}

// DefaultLimits returns the default risk thresholds.
func DefaultLimits() RiskLimits {
	return RiskLimits{
		MaxDailyLossUsdt:         300,
		MaxPortfolioExposureUsdt: 5000,
		MaxSymbolExposureUsdt:    2000,
		MaxOpenPositions:         5,
		MaxConsecutiveLosses:     3,
		MinOrderSizeUsdt:         10,
	}
}

// CandidateInfo holds the fields from a CandidateTrade required for risk evaluation.
type CandidateInfo struct {
	Id                string
	Symbol            string
	Side              string
	EntryPrice        float64
	EstimatedExposure float64 // proposed position size in USDT
}

// PortfolioInfo holds the portfolio state at evaluation time.
type PortfolioInfo struct {
	TotalExposure  float64
	AvailableCash  float64
	OpenPositions  int
	SymbolExposure map[string]float64 // current USDT exposure per symbol
}

// TradingHistoryInfo holds recent trade performance statistics.
type TradingHistoryInfo struct {
	DailyPnL          float64
	ConsecutiveLosses int
	RecentWinRate     float64
}

// RiskContext is the complete, serializable input to HardRiskGate.Evaluate().
// It aggregates all facts needed to make a risk decision without any DB or
// network access at evaluation time.
type RiskContext struct {
	Candidate      CandidateInfo
	Portfolio      PortfolioInfo
	TradingHistory TradingHistoryInfo
	Limits         RiskLimits
}

// RiskDecision is the output of HardRiskGate.Evaluate().
type RiskDecision struct {
	Decision     string   // DecisionApprove, DecisionReject, or DecisionReduce
	Reasons      []string // populated when Decision == DecisionReject
	PositionSize float64  // proposed size in USDT (pass-through from Candidate.EstimatedExposure)
}

// HardRiskGate evaluates a RiskContext against hard risk limits.
// It is a pure function: no DB calls, no network calls.
type HardRiskGate struct{}

// NewHardRiskGate returns a HardRiskGate.
func NewHardRiskGate() *HardRiskGate {
	return &HardRiskGate{}
}

// Evaluate checks all six hard risk rules and returns a RiskDecision.
// Rules are evaluated independently; all violated rules are reported.
func (g *HardRiskGate) Evaluate(ctx RiskContext) RiskDecision {
	var reasons []string

	if ctx.TradingHistory.DailyPnL <= -ctx.Limits.MaxDailyLossUsdt {
		reasons = append(reasons, riskshared.ReasonDailyLossLimitExceeded)
	}
	if ctx.Portfolio.TotalExposure+ctx.Candidate.EstimatedExposure > ctx.Limits.MaxPortfolioExposureUsdt {
		reasons = append(reasons, riskshared.ReasonPortfolioExposureExceeded)
	}
	symbolExposure := ctx.Portfolio.SymbolExposure[ctx.Candidate.Symbol]
	if symbolExposure+ctx.Candidate.EstimatedExposure > ctx.Limits.MaxSymbolExposureUsdt {
		reasons = append(reasons, riskshared.ReasonSymbolExposureExceeded)
	}
	if ctx.Portfolio.OpenPositions >= ctx.Limits.MaxOpenPositions {
		reasons = append(reasons, riskshared.ReasonMaxOpenPositionsReached)
	}
	if ctx.TradingHistory.ConsecutiveLosses >= ctx.Limits.MaxConsecutiveLosses {
		reasons = append(reasons, riskshared.ReasonConsecutiveLossesExceeded)
	}
	if ctx.Portfolio.AvailableCash < ctx.Limits.MinOrderSizeUsdt {
		reasons = append(reasons, riskshared.ReasonInsufficientBalance)
	}

	if len(reasons) > 0 {
		return RiskDecision{Decision: riskshared.DecisionReject, Reasons: reasons}
	}
	return RiskDecision{
		Decision:     riskshared.DecisionApprove,
		PositionSize: ctx.Candidate.EstimatedExposure,
	}
}
