package riskshared

// Decision constants.
const (
	DecisionApprove = "APPROVE"
	DecisionReject  = "REJECT"
	DecisionReduce  = "REDUCE"
)

// Rejection reason constants.
const (
	ReasonDailyLossLimitExceeded    = "DAILY_LOSS_LIMIT_EXCEEDED"
	ReasonPortfolioExposureExceeded = "PORTFOLIO_EXPOSURE_EXCEEDED"
	ReasonSymbolExposureExceeded    = "SYMBOL_EXPOSURE_EXCEEDED"
	ReasonMaxOpenPositionsReached   = "MAX_OPEN_POSITIONS_REACHED"
	ReasonConsecutiveLossesExceeded = "CONSECUTIVE_LOSSES_EXCEEDED"
	ReasonInsufficientBalance       = "INSUFFICIENT_BALANCE"
)
