package risk

import (
	"context"
	"crypto/rand"
	"encoding/json"
	"fmt"
	"time"

	"trading_bot/internal/database/generated"
)

// SnapshotInput holds the portfolio state derived from the getsnapshot handler.
// The process handler constructs this from getsnapshot.Result before calling Build.
type SnapshotInput struct {
	CandidateId   string
	Equity        float64
	AvailableCash float64
	TotalExposure float64
	UnrealizedPnl float64
	OpenPositions int
}

// RiskContextService assembles a RiskContext from portfolio and journal data,
// persists a portfolio snapshot and risk context record, and returns the assembled
// context for the HardRiskGate. Not a handler; injected into the process handler.
type RiskContextService struct {
	queries *generated.Queries
	limits  RiskLimits
}

// NewRiskContextService creates a RiskContextService.
func NewRiskContextService(queries *generated.Queries, limits RiskLimits) *RiskContextService {
	return &RiskContextService{queries: queries, limits: limits}
}

// Build assembles a RiskContext for the given candidate and portfolio snapshot,
// persists both records to the DB, and returns the assembled context and the
// persisted risk context ID (for use as a FK in the risk decision record).
func (s *RiskContextService) Build(ctx context.Context, candidate CandidateInfo, snap SnapshotInput) (RiskContext, string, error) {
	snapshotId, err := s.persistPortfolioSnapshot(ctx, snap)
	if err != nil {
		return RiskContext{}, "", fmt.Errorf("risk: persisting portfolio snapshot: %w", err)
	}

	symbolExposure, err := s.buildSymbolExposure(ctx)
	if err != nil {
		return RiskContext{}, "", fmt.Errorf("risk: reading open positions: %w", err)
	}

	history, err := s.buildTradingHistory(ctx)
	if err != nil {
		return RiskContext{}, "", fmt.Errorf("risk: reading trade history: %w", err)
	}

	riskCtx := RiskContext{
		Candidate: candidate,
		Portfolio: PortfolioInfo{
			TotalExposure:  snap.TotalExposure,
			AvailableCash:  snap.AvailableCash,
			OpenPositions:  snap.OpenPositions,
			SymbolExposure: symbolExposure,
		},
		TradingHistory: history,
		Limits:         s.limits,
	}

	rawJSON, err := json.Marshal(riskCtx)
	if err != nil {
		return RiskContext{}, "", fmt.Errorf("risk: marshaling risk context: %w", err)
	}

	riskContextId := newUUID()
	if err := s.queries.InsertRiskContext(ctx, generated.InsertRiskContextParams{
		Id:                  riskContextId,
		CandidateId:         candidate.Id,
		PortfolioSnapshotId: snapshotId,
		GeneratedAt:         time.Now().UTC(),
		RawJson:             string(rawJSON),
	}); err != nil {
		return RiskContext{}, "", fmt.Errorf("risk: saving risk context: %w", err)
	}

	return riskCtx, riskContextId, nil
}

func (s *RiskContextService) persistPortfolioSnapshot(ctx context.Context, snap SnapshotInput) (string, error) {
	id := newUUID()
	err := s.queries.InsertPortfolioSnapshot(ctx, generated.InsertPortfolioSnapshotParams{
		Id:            id,
		CandidateId:   snap.CandidateId,
		Equity:        snap.Equity,
		AvailableCash: snap.AvailableCash,
		TotalExposure: snap.TotalExposure,
		UnrealizedPnl: snap.UnrealizedPnl,
		OpenPositions: int32(snap.OpenPositions),
		CapturedAt:    time.Now().UTC(),
	})
	if err != nil {
		return "", err
	}
	return id, nil
}

func (s *RiskContextService) buildSymbolExposure(ctx context.Context) (map[string]float64, error) {
	positions, err := s.queries.GetOpenPositions(ctx)
	if err != nil {
		return nil, err
	}
	m := make(map[string]float64, len(positions))
	for _, p := range positions {
		m[p.Symbol] = p.CurrentPrice * p.Quantity
	}
	return m, nil
}

func (s *RiskContextService) buildTradingHistory(ctx context.Context) (TradingHistoryContext, error) {
	today := time.Now().UTC().Truncate(24 * time.Hour)
	dailyPnL, err := s.queries.GetDailyPnL(ctx, today)
	if err != nil {
		return TradingHistoryContext{}, err
	}

	recent, err := s.queries.GetRecentTradeOutcomes(ctx, 20)
	if err != nil {
		return TradingHistoryContext{}, err
	}

	return TradingHistoryContext{
		DailyPnL:          dailyPnL,
		ConsecutiveLosses: countConsecutiveLosses(recent),
		RecentWinRate:     calcWinRate(recent),
	}, nil
}

// countConsecutiveLosses counts trailing losses from the newest outcome backward.
// outcomes must be ordered newest-first (as returned by GetRecentTradeOutcomes).
func countConsecutiveLosses(outcomes []generated.TradeOutcome) int {
	count := 0
	for _, o := range outcomes {
		if o.NetPnl < 0 {
			count++
		} else {
			break
		}
	}
	return count
}

// calcWinRate returns the fraction of outcomes with non-negative NetPnl.
func calcWinRate(outcomes []generated.TradeOutcome) float64 {
	if len(outcomes) == 0 {
		return 0
	}
	wins := 0
	for _, o := range outcomes {
		if o.NetPnl >= 0 {
			wins++
		}
	}
	return float64(wins) / float64(len(outcomes))
}

func newUUID() string {
	b := make([]byte, 16)
	_, _ = rand.Read(b)
	b[6] = (b[6] & 0x0f) | 0x40
	b[8] = (b[8] & 0x3f) | 0x80
	return fmt.Sprintf("%08x-%04x-%04x-%04x-%012x", b[0:4], b[4:6], b[6:8], b[8:10], b[10:])
}
