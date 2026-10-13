package kucoin

import (
	"context"
	"crypto/hmac"
	"crypto/sha256"
	"encoding/base64"
	"encoding/json"
	"fmt"
	"net/http"
	"strconv"
	"strings"
	"time"

	"trading_bot/internal/marketdata"
)

const baseURL = "https://api.kucoin.com"

// Service calls the KuCoin REST API for market data and (when credentials are
// provided) authenticated account operations.
// It normalizes domain symbols (BTCUSDT) to KuCoin format (BTC-USDT) at the boundary,
// reverses the newest-first response to oldest-first, and marks all returned candles
// as closed (REST candles are always fully closed).
type Service struct {
	httpClient *http.Client
	cfg        Config
}

// NewService returns a Service configured with the given credentials.
// Pass an empty Config{} for unauthenticated (public endpoint) use.
func NewService(cfg Config) *Service {
	return &Service{
		httpClient: &http.Client{Timeout: 30 * time.Second},
		cfg:        cfg,
	}
}

// GetClosedCandles fetches up to limit closed candles for symbol+timeframe from KuCoin,
// returned oldest-first. symbol must be in canonical form (e.g. "BTCUSDT").
func (s *Service) GetClosedCandles(ctx context.Context, symbol, timeframe string, limit int) ([]marketdata.Candle, error) {
	kuCoinSymbol := toKuCoinSymbol(symbol)
	url := fmt.Sprintf("%s/api/v1/market/candles?type=%s&symbol=%s", baseURL, timeframe, kuCoinSymbol)

	req, err := http.NewRequestWithContext(ctx, http.MethodGet, url, nil)
	if err != nil {
		return nil, fmt.Errorf("kucoin: building request: %w", err)
	}

	resp, err := s.httpClient.Do(req)
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

// GetBalances fetches spot (trade) account balances from KuCoin.
// Requires the Service to have been created with NewAuthenticatedService.
func (s *Service) GetBalances(ctx context.Context) ([]Balance, error) {
	path := "/api/v1/accounts?type=trade"
	req, err := s.newAuthRequest(ctx, http.MethodGet, path, "")
	if err != nil {
		return nil, fmt.Errorf("kucoin: building balances request: %w", err)
	}

	resp, err := s.httpClient.Do(req)
	if err != nil {
		return nil, fmt.Errorf("kucoin: fetching balances: %w", err)
	}
	defer resp.Body.Close()

	var result struct {
		Code string `json:"code"`
		Data []struct {
			Currency  string `json:"currency"`
			Balance   string `json:"balance"`
			Available string `json:"available"`
			Holds     string `json:"holds"`
		} `json:"data"`
	}
	if err := json.NewDecoder(resp.Body).Decode(&result); err != nil {
		return nil, fmt.Errorf("kucoin: decoding balances response: %w", err)
	}
	if result.Code != "200000" {
		return nil, fmt.Errorf("kucoin: accounts API returned code %s", result.Code)
	}

	balances := make([]Balance, 0, len(result.Data))
	for _, d := range result.Data {
		total, _ := strconv.ParseFloat(d.Balance, 64)
		available, _ := strconv.ParseFloat(d.Available, 64)
		locked, _ := strconv.ParseFloat(d.Holds, 64)
		if total == 0 && available == 0 && locked == 0 {
			continue // skip zero-balance assets
		}
		balances = append(balances, Balance{
			Asset:     d.Currency,
			Total:     total,
			Available: available,
			Locked:    locked,
		})
	}
	return balances, nil
}

// GetOpenOrders fetches active spot orders from KuCoin.
// Requires the Service to have been created with NewAuthenticatedService.
func (s *Service) GetOpenOrders(ctx context.Context) ([]OpenOrder, error) {
	path := "/api/v1/orders?status=active&tradeType=TRADE"
	req, err := s.newAuthRequest(ctx, http.MethodGet, path, "")
	if err != nil {
		return nil, fmt.Errorf("kucoin: building orders request: %w", err)
	}

	resp, err := s.httpClient.Do(req)
	if err != nil {
		return nil, fmt.Errorf("kucoin: fetching open orders: %w", err)
	}
	defer resp.Body.Close()

	var result struct {
		Code string `json:"code"`
		Data struct {
			Items []struct {
				Symbol   string `json:"symbol"`
				Side     string `json:"side"`
				Size     string `json:"size"`
				Price    string `json:"price"`
				DealSize string `json:"dealSize"`
			} `json:"items"`
		} `json:"data"`
	}
	if err := json.NewDecoder(resp.Body).Decode(&result); err != nil {
		return nil, fmt.Errorf("kucoin: decoding orders response: %w", err)
	}
	if result.Code != "200000" {
		return nil, fmt.Errorf("kucoin: orders API returned code %s", result.Code)
	}

	orders := make([]OpenOrder, 0, len(result.Data.Items))
	for _, item := range result.Data.Items {
		size, _ := strconv.ParseFloat(item.Size, 64)
		price, _ := strconv.ParseFloat(item.Price, 64)
		filled, _ := strconv.ParseFloat(item.DealSize, 64)
		orders = append(orders, OpenOrder{
			Symbol:     item.Symbol,
			Side:       item.Side,
			Size:       size,
			Price:      price,
			FilledSize: filled,
		})
	}
	return orders, nil
}

// newAuthRequest builds an HTTP request with KuCoin V2 signature headers.
// sign   = base64(HMAC-SHA256(timestamp+method+path+body, apiSecret))
// passph = base64(HMAC-SHA256(passphrase, apiSecret))
func (s *Service) newAuthRequest(ctx context.Context, method, path, body string) (*http.Request, error) {
	timestamp := strconv.FormatInt(time.Now().UnixMilli(), 10)
	preHash := timestamp + method + path + body

	sign := base64.StdEncoding.EncodeToString(hmacSHA256([]byte(s.cfg.APISecret), []byte(preHash)))
	passphrase := base64.StdEncoding.EncodeToString(hmacSHA256([]byte(s.cfg.APISecret), []byte(s.cfg.Passphrase)))

	req, err := http.NewRequestWithContext(ctx, method, baseURL+path, strings.NewReader(body))
	if err != nil {
		return nil, err
	}
	req.Header.Set("Content-Type", "application/json")
	req.Header.Set("KC-API-KEY", s.cfg.APIKey)
	req.Header.Set("KC-API-SIGN", sign)
	req.Header.Set("KC-API-TIMESTAMP", timestamp)
	req.Header.Set("KC-API-PASSPHRASE", passphrase)
	req.Header.Set("KC-API-KEY-VERSION", "2")
	return req, nil
}

func hmacSHA256(secret, data []byte) []byte {
	h := hmac.New(sha256.New, secret)
	h.Write(data)
	return h.Sum(nil)
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
