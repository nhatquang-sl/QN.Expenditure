package kucoin

import (
	"context"
	"encoding/json"
	"fmt"
	"net/http"
	"strconv"
	"strings"
	"time"

	"trading_bot/internal/marketdata"
)

// Adapter implements marketdata.CandleRepository backed by the KuCoin REST API.
// It normalizes domain symbols (BTCUSDT) to KuCoin format (BTC-USDT) at the boundary,
// reverses the newest-first response to oldest-first, and marks all returned candles
// as closed (REST candles are always fully closed).
type Adapter struct {
	baseURL    string
	httpClient *http.Client
}

func NewAdapter(baseURL string) *Adapter {
	return &Adapter{
		baseURL:    strings.TrimRight(baseURL, "/"),
		httpClient: &http.Client{Timeout: 30 * time.Second},
	}
}

// GetClosedCandles fetches up to limit closed candles for symbol+timeframe from KuCoin,
// returned oldest-first. symbol must be in canonical form (e.g. "BTCUSDT").
func (a *Adapter) GetClosedCandles(ctx context.Context, symbol, timeframe string, limit int) ([]marketdata.Candle, error) {
	kuCoinSymbol := toKuCoinSymbol(symbol)
	url := fmt.Sprintf("%s/api/v1/market/candles?type=%s&symbol=%s", a.baseURL, timeframe, kuCoinSymbol)

	req, err := http.NewRequestWithContext(ctx, http.MethodGet, url, nil)
	if err != nil {
		return nil, fmt.Errorf("kucoin: building request: %w", err)
	}

	resp, err := a.httpClient.Do(req)
	if err != nil {
		return nil, fmt.Errorf("kucoin: fetching candles for %s %s: %w", symbol, timeframe, err)
	}
	defer resp.Body.Close()

	var result struct {
		Code string     `json:"code"`
		Data [][]string `json:"data"`
	}
	if err := json.NewDecoder(resp.Body).Decode(&result); err != nil {
		return nil, fmt.Errorf("kucoin: decoding response: %w", err)
	}
	if result.Code != "200000" {
		return nil, fmt.Errorf("kucoin: API returned code %s", result.Code)
	}

	// KuCoin returns newest-first; reverse to oldest-first.
	rows := result.Data
	for i, j := 0, len(rows)-1; i < j; i, j = i+1, j-1 {
		rows[i], rows[j] = rows[j], rows[i]
	}

	candles := make([]marketdata.Candle, 0, len(rows))
	dur := timeframeDuration(timeframe)
	for _, row := range rows {
		c, err := parseRow(row, symbol, timeframe, dur)
		if err != nil {
			return nil, err
		}
		candles = append(candles, c)
	}

	if len(candles) > limit {
		candles = candles[len(candles)-limit:]
	}
	return candles, nil
}

// toKuCoinSymbol converts canonical symbol (BTCUSDT) to KuCoin format (BTC-USDT).
var knownQuotes = []string{"USDT", "USDC", "BTC", "ETH", "BNB"}

func toKuCoinSymbol(symbol string) string {
	for _, q := range knownQuotes {
		if strings.HasSuffix(symbol, q) {
			return symbol[:len(symbol)-len(q)] + "-" + q
		}
	}
	return symbol
}

// parseRow converts a KuCoin candle row to a Candle.
// KuCoin row format: [timestamp, open, close, high, low, volume, turnover]
func parseRow(row []string, symbol, timeframe string, dur time.Duration) (marketdata.Candle, error) {
	if len(row) < 7 {
		return marketdata.Candle{}, fmt.Errorf("kucoin: unexpected row length %d", len(row))
	}

	ts, err := strconv.ParseInt(row[0], 10, 64)
	if err != nil {
		return marketdata.Candle{}, fmt.Errorf("kucoin: parsing timestamp %q: %w", row[0], err)
	}
	open, err := strconv.ParseFloat(row[1], 64)
	if err != nil {
		return marketdata.Candle{}, fmt.Errorf("kucoin: parsing open: %w", err)
	}
	close, err := strconv.ParseFloat(row[2], 64)
	if err != nil {
		return marketdata.Candle{}, fmt.Errorf("kucoin: parsing close: %w", err)
	}
	high, err := strconv.ParseFloat(row[3], 64)
	if err != nil {
		return marketdata.Candle{}, fmt.Errorf("kucoin: parsing high: %w", err)
	}
	low, err := strconv.ParseFloat(row[4], 64)
	if err != nil {
		return marketdata.Candle{}, fmt.Errorf("kucoin: parsing low: %w", err)
	}
	volume, err := strconv.ParseFloat(row[5], 64)
	if err != nil {
		return marketdata.Candle{}, fmt.Errorf("kucoin: parsing volume: %w", err)
	}

	openTime := time.Unix(ts, 0).UTC()
	return marketdata.Candle{
		Symbol:    symbol,
		Timeframe: timeframe,
		OpenTime:  openTime,
		CloseTime: openTime.Add(dur),
		Open:      open,
		High:      high,
		Low:       low,
		Close:     close,
		Volume:    volume,
		IsClosed:  true, // REST API only returns fully closed candles
	}, nil
}

func timeframeDuration(timeframe string) time.Duration {
	switch timeframe {
	case "1min":
		return time.Minute
	case "3min":
		return 3 * time.Minute
	case "5min":
		return 5 * time.Minute
	case "15min":
		return 15 * time.Minute
	case "30min":
		return 30 * time.Minute
	case "1hour":
		return time.Hour
	case "2hour":
		return 2 * time.Hour
	case "4hour":
		return 4 * time.Hour
	case "6hour":
		return 6 * time.Hour
	case "8hour":
		return 8 * time.Hour
	case "12hour":
		return 12 * time.Hour
	case "1day":
		return 24 * time.Hour
	case "1week":
		return 7 * 24 * time.Hour
	default:
		return time.Hour
	}
}
