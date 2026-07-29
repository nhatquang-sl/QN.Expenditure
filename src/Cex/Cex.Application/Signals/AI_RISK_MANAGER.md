# AI Risk Manager — Design Plan

> **Status**: Pending implementation.
> See [SIGNALS.md](SIGNALS.md) for the existing entity schema and signal lifecycle.

## Context

The Signal module algorithmically detects RSI divergence patterns but provides no qualitative assessment of signal quality. An AI Risk Manager evaluates each signal immediately after detection using Claude, producing a structured recommendation (Trade / Skip / Monitor), a risk score (0–100), risk level, and human-readable reasoning.

**Confirmed design decisions:**
- LLM: Anthropic Claude (`claude-haiku-4-5-20251001`) via tool use (forced structured output)
- Trigger: **Event-driven via RabbitMQ** — `FindSignalCommand` publishes the signal; a MassTransit consumer invokes `EvaluateSignalRiskCommand` for only that signal
- Behavioral impact: **Informational only** — existing Telegram alert unchanged; AI sends a separate follow-up
- `FindSignalService` loop: **unchanged** — no new step added
- AI evaluation stored in a **separate `SignalAiEvaluations` table** (not nullable columns on `Signals`) — different lifecycle, separate write path, cleaner idempotency
- Market context fetched **inside the handler** (not passed via event) — keeps the MassTransit message lean and data is live at time of evaluation

---

## Architecture Overview

```
FindSignalCommand
  → save Signal to DB
  → IEventBus.PublishAsync(new FoundSignalEvent(signal))
  → [existing Telegram alert — unchanged]

RabbitMQ (durable queue: found-signal-event)
  ↓

FoundSignalEventConsumer : IConsumer<FoundSignalEvent>
  → EvaluateSignalRiskCommand(SignalId, SignalType, Interval, EntryPrice, ...)
  → mediator.Send(command)

EvaluateSignalRiskCommand
  → idempotency guard (SignalAiEvaluations WHERE SignalId = @id → skip)
  → 48h stale guard
  → parallel fetch:
      ├── 90-day history query (DB) — win rate / entry rate context
      ├── TimeframeSummary for signal interval (volume, ATR, price position)
      ├── TimeframeSummary for 1h (trend alignment)
      ├── TimeframeSummary for 4h (trend alignment)
      ├── FundingRateInfo (crowding sentiment)
      └── OpenInterestInfo (trend conviction)
  → Anthropic Claude API (tool use)
  → INSERT SignalAiEvaluation row (no Signal entity load for write)
  → Telegram follow-up: "[5min Short] AI: SKIP 78/100 — High Risk"
  → re-throw on exception (MassTransit retries: 5s → 30s → 2min → dead-letter)
```

---

## New Files

| File | Purpose |
|---|---|
| `src/Cex/Cex.Domain/Entities/SignalAiEvaluation.cs` | New entity: one-to-one with Signal |
| `src/Cex/Cex.Infrastructure/Data/Configurations/SignalAiEvaluationConfiguration.cs` | EF config: UNIQUE(SignalId), FK, precision |
| `src/Libs/Lib.Application/Abstractions/IEventBus.cs` | Interface: `PublishAsync<T>(T message, ct)` — transport-agnostic event bus |
| `src/Libs/Lib.EventBus/RabbitMqEventBus.cs` | `IEventBus` impl wrapping MassTransit `IPublishEndpoint`; swallows publish errors so the caller is never blocked |
| `src/Cex/Cex.Application/Signals/Commands/FindSignal/FoundSignalEvent.cs` | MassTransit message contract — class with primary constructor `(Signal signal)`; carries all signal fields |
| `src/Cex/Cex.Application/Signals/Commands/FindSignal/FoundSignalEventConsumer.cs` | `IConsumer<FoundSignalEvent>` → maps to `EvaluateSignalRiskCommand` → MediatR |
| `src/Libs/Lib.ExternalServices/Anthropic/AnthropicConfig.cs` | Config: `ApiKey`, `Model`, `MaxTokens` |
| `src/Libs/Lib.ExternalServices/Anthropic/IAnthropicService.cs` | Interface: `SendMessagesWithToolAsync` |
| `src/Libs/Lib.ExternalServices/Anthropic/AnthropicService.cs` | Typed `HttpClient` — Anthropic Messages API |
| `src/Libs/Lib.ExternalServices/Anthropic/Models/AnthropicModels.cs` | Wire types: `ToolDefinition`, `ToolUseResult` |
| `src/Libs/Lib.ExternalServices/MarketData/IMarketDataService.cs` | Interface: `GetTimeframeSummaryAsync`, `GetFundingRateAsync`, `GetOpenInterestAsync` |
| `src/Libs/Lib.ExternalServices/MarketData/MarketDataService.cs` | Typed `HttpClient` — Binance REST API |
| `src/Libs/Lib.ExternalServices/MarketData/MarketDataModels.cs` | `TimeframeSummary`, `FundingRateInfo`, `OpenInterestInfo` |
| `src/Cex/Cex.Application/Signals/Commands/EvaluateSignalRisk/EvaluateSignalRiskCommand.cs` | Command + Handler + prompt builder (colocated) |
| `src/Cex/Cex.Application/Signals/Commands/EvaluateSignalRisk/EVALUATE_SIGNAL_RISK.md` | Feature doc |
| `src/WebUI.React/src/features/signal/signals/components/AiRiskChip.tsx` | MUI Chip + tooltip rendering AI assessment |

## Modified Files

| File | Change |
|---|---|
| `src/Cex/Cex.Domain/Entities/Signal.cs` | Add `SignalAiEvaluation? AiEvaluation` nav property |
| `src/Cex/Cex.Infrastructure/Data/Configurations/SignalConfiguration.cs` | Add `HasOne` nav config |
| `src/Cex/Cex.Infrastructure/Data/CexDbContext.cs` | Add `DbSet<SignalAiEvaluation>` |
| `src/Cex/Cex.Application/Signals/Commands/FindSignal/FindSignalCommand.cs` | Inject `IEventBus`; publish `FoundSignalEvent` after successful save |
| `src/Libs/Lib.ExternalServices/DependencyInjection.cs` | Register `IAnthropicService` + `IMarketDataService` typed `HttpClient`s |
| `src/Libs/Lib.EventBus/DependencyInjection.cs` | `AddLibEventBusServices` — configures MassTransit + RabbitMQ; accepts `Action<IBusRegistrationConfigurator>` delegate for consumer registration |
| `src/WebAPI/Program.cs` | Calls `AddLibEventBusServices` with delegate that registers `FoundSignalEventConsumer` |
| `docker-compose.yml` | Add `rabbitmq:4-management` service |
| `credentials/appsettings.json` | Add `Anthropic` + `RabbitMq` config sections |
| `src/Cex/Cex.Application/Signals/Queries/GetSignals/SignalDto.cs` | +6 AI fields (flattened from join) |
| `src/Cex/Cex.Application/Signals/Queries/GetSignals/GetSignalsQueryHandler.cs` | LEFT JOIN `SignalAiEvaluations`; project + map new fields |
| `src/WebUI.React/src/features/signal/signals/types.ts` | Extend `SignalDto` interface |
| `src/WebUI.React/src/features/signal/signals/index.tsx` | Add AI Risk column + `AiRiskChip` |

---

## Step 1 — Domain: SignalAiEvaluation Entity

**`src/Cex/Cex.Domain/Entities/SignalAiEvaluation.cs`** — new file:

```csharp
namespace Cex.Domain.Entities;

public class SignalAiEvaluation
{
    public int Id { get; set; }
    public int SignalId { get; set; }                // FK + UNIQUE → enforces 1:1 at DB level
    public string Recommendation { get; set; } = string.Empty;  // "Trade" | "Skip" | "Monitor"
    public int RiskScore { get; set; }               // 0–100; higher = more risk
    public string RiskLevel { get; set; } = string.Empty;       // "Low" | "Medium" | "High" | "VeryHigh"
    public string Reasoning { get; set; } = string.Empty;       // LLM reasoning text (nvarchar(max))
    public string KeyFactors { get; set; } = string.Empty;      // JSON-encoded string[] (nvarchar(max))
    public DateTime EvaluatedAt { get; set; }        // When evaluation completed

    public Signal Signal { get; set; } = null!;
}
```

**`src/Cex/Cex.Domain/Entities/Signal.cs`** — add nav property after existing fields:

```csharp
public SignalAiEvaluation? AiEvaluation { get; set; }
```

---

## Step 2 — Infrastructure: EF Core Config

**`src/Cex/Cex.Infrastructure/Data/Configurations/SignalAiEvaluationConfiguration.cs`** — new file:

```csharp
namespace Cex.Infrastructure.Data.Configurations;

public class SignalAiEvaluationConfiguration : IEntityTypeConfiguration<SignalAiEvaluation>
{
    public void Configure(EntityTypeBuilder<SignalAiEvaluation> builder)
    {
        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.SignalId).IsUnique();  // enforces 1:1 at DB level

        builder.Property(x => x.Recommendation).HasMaxLength(20).IsRequired();
        builder.Property(x => x.RiskLevel).HasMaxLength(20).IsRequired();
        builder.Property(x => x.EvaluatedAt).HasPrecision(0);
        // Reasoning, KeyFactors: nvarchar(max) by EF Core default

        builder.HasOne(x => x.Signal)
               .WithOne(x => x.AiEvaluation)
               .HasForeignKey<SignalAiEvaluation>(x => x.SignalId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
```

**`CexDbContext.cs`** — add:

```csharp
public DbSet<SignalAiEvaluation> SignalAiEvaluations => Set<SignalAiEvaluation>();
```

**Migration name:** `AddSignalAiEvaluation`

---

## Step 3 — Application: IEventBus + RabbitMqEventBus

**`src/Libs/Lib.Application/Abstractions/IEventBus.cs`** — already implemented:
```csharp
namespace Lib.Application.Abstractions;

public interface IEventBus
{
    Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class;
}
```

**`src/Libs/Lib.EventBus/RabbitMqEventBus.cs`** — already implemented; wraps MassTransit `IPublishEndpoint` and swallows publish errors so the caller (`FindSignalCommand`) is never blocked by a messaging failure.

---

## Step 4 — Application: Modify FindSignalCommand

**`FindSignalCommand.cs`** — already injects `IEventBus`; publishes `FoundSignalEvent` inside `SaveSignalIfNewAsync` after a successful `SaveChangesAsync` (EF Core sets `signal.Id` before the publish call):

```csharp
public class FindSignalCommandHandler(
    /* existing injections */
    IEventBus eventBus)
    : IRequestHandler<FindSignalCommand>
```

Inside `SaveSignalIfNewAsync`, after `SaveChangesAsync`:
```csharp
await dbContext.SaveChangesAsync(cancellationToken);
// signal.Id is set here by EF Core
await eventBus.PublishAsync(new FoundSignalEvent(signal), cancellationToken);
```

---

## Step 5 — Lib.ExternalServices: Anthropic HTTP Client

Uses typed `HttpClient` (not Refit — Anthropic's tool-use body is deeply nested, easier with `System.Text.Json` directly).

**`AnthropicConfig.cs`**:
```csharp
public class AnthropicConfig
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "claude-haiku-4-5-20251001";
    public int MaxTokens { get; set; } = 1024;
}
```

**`IAnthropicService.cs`**:
```csharp
public interface IAnthropicService
{
    Task<ToolUseResult?> SendMessagesWithToolAsync(
        string systemPrompt,
        string userMessage,
        ToolDefinition tool,
        CancellationToken cancellationToken);
}
```

**`AnthropicModels.cs`**:
```csharp
public record ToolDefinition(string Name, string Description, object InputSchema);
public record ToolUseResult(string ToolName, JsonElement Input);
```

**`AnthropicService.cs`** behaviour:
- POST `https://api.anthropic.com/v1/messages`
- Headers: `x-api-key: {ApiKey}`, `anthropic-version: 2023-06-01`
- Body includes `tool_choice: { "type": "tool", "name": toolName }` — forces Claude to call the tool (no plain-text fallback)
- Parse response: find `content[]` block where `type == "tool_use"`, return its `input` as `JsonElement`
- Returns `null` if no tool_use block; throws `HttpRequestException` on non-2xx

---

## Step 6 — Lib.ExternalServices: Market Data Service

Fetches live market context from Binance REST API for prompt enrichment. All three endpoints are called in parallel inside the handler.

**`MarketDataModels.cs`**:
```csharp
namespace Lib.ExternalServices.MarketData;

/// <summary>Summary of the last 20 candles on a given timeframe.</summary>
public record TimeframeSummary(
    string Interval,
    string Trend,           // "Uptrend" | "Downtrend" | "Ranging"
    decimal Rsi14,          // RSI(14) on closing prices
    int PositionInRangePct, // 0–100: where current price sits in the 20-candle high/low range
    decimal VolumeLast,     // Volume of the most recent closed candle (in base asset)
    decimal VolumeAvg20,    // 20-candle simple average volume
    decimal Atr14           // ATR(14) in USDT — contextualises SL/TP tightness
);

/// <summary>Current perpetual futures funding rate.</summary>
public record FundingRateInfo(
    decimal Rate,           // e.g. 0.00042 = +0.042% per 8h; negative = shorts pay longs
    string Sentiment        // "Neutral" (<0.01%) | "Elevated" (0.01–0.05%) | "Extreme" (>0.05%)
);

/// <summary>Perpetual futures open interest.</summary>
public record OpenInterestInfo(
    decimal CurrentUsdBillions,
    decimal Change4hPct     // positive = OI growing; negative = OI unwinding
);
```

**`IMarketDataService.cs`**:
```csharp
namespace Lib.ExternalServices.MarketData;

public interface IMarketDataService
{
    /// <summary>Fetch last 20 candles and compute trend, RSI, ATR, volume summary.</summary>
    Task<TimeframeSummary> GetTimeframeSummaryAsync(string symbol, string interval, CancellationToken cancellationToken);

    /// <summary>Fetch current perpetual funding rate.</summary>
    Task<FundingRateInfo> GetFundingRateAsync(string symbol, CancellationToken cancellationToken);

    /// <summary>Fetch current open interest and 4h change.</summary>
    Task<OpenInterestInfo> GetOpenInterestAsync(string symbol, CancellationToken cancellationToken);
}
```

**`MarketDataService.cs`** behaviour:
- Base URL: `https://fapi.binance.com` (USDT-M futures — funding rate + OI + klines all on same host)
- `GetTimeframeSummaryAsync`: `GET /fapi/v1/klines?symbol={symbol}&interval={interval}&limit=20` → compute locally:
  - Trend: compare 20-period EMA direction (last close vs close 10 candles ago)
  - RSI(14): Wilder smoothing on closing prices (if fewer than 14 candles, use available count)
  - PositionInRangePct: `(lastClose - rangeMin) / (rangeMax - rangeMin) * 100`
  - VolumeLast / VolumeAvg20: last candle volume vs 20-candle simple average
  - ATR(14): Wilder smoothing of true range
- `GetFundingRateAsync`: `GET /fapi/v1/premiumIndex?symbol={symbol}` → `lastFundingRate`; derive `Sentiment` from absolute value
- `GetOpenInterestAsync`: two calls in parallel — `GET /fapi/v1/openInterest?symbol={symbol}` (current) + `GET /futures/data/openInterestHist?symbol={symbol}&period=4h&limit=2` (4h change)
- Returns a safe default (zeroed/neutral record) on HTTP error rather than throwing — market data failure should not block the evaluation

**DI registration** (`Lib.ExternalServices/DependencyInjection.cs`):
```csharp
services.AddHttpClient<IAnthropicService, AnthropicService>(client =>
    client.BaseAddress = new Uri("https://api.anthropic.com"));
services.Configure<AnthropicConfig>(configuration.GetSection("Anthropic"));

services.AddHttpClient<IMarketDataService, MarketDataService>(client =>
    client.BaseAddress = new Uri("https://fapi.binance.com"));
```

---

## Step 7 — Application: EvaluateSignalRiskCommand

**`EvaluateSignalRiskCommand.cs`** — command, handler, and prompt builder colocated.

### Command
```csharp
public record EvaluateSignalRiskCommand(
    int SignalId,
    string SignalType,        // "Long" | "Short"
    string Interval,
    DateTime DetectedAt,
    decimal RsiValue,
    decimal PreviousRsiValue,
    decimal EntryPrice,
    decimal StopLoss,
    decimal TakeProfit,
    int Leverage,
    DateTime CreatedAt) : IRequest;
```

### Handler
```csharp
public class EvaluateSignalRiskCommandHandler(
    IAnthropicService anthropicService,
    IMarketDataService marketDataService,
    ICexDbContext dbContext,
    INotifier notifier,
    ILogTrace logTrace)
    : IRequestHandler<EvaluateSignalRiskCommand>
{
    public async Task Handle(EvaluateSignalRiskCommand request, CancellationToken cancellationToken)
    {
        // Idempotency guard — safe to re-deliver
        var alreadyEvaluated = await dbContext.SignalAiEvaluations
            .AnyAsync(e => e.SignalId == request.SignalId, cancellationToken);
        if (alreadyEvaluated) return;

        // Stale guard — skip if queue was drained after >48h
        if (request.CreatedAt < DateTime.UtcNow.AddHours(-48)) return;

        try
        {
            // 1. Fetch DB history + live market context in parallel
            var historyTask   = FetchHistoryAsync(request, cancellationToken);
            var signalTfTask  = marketDataService.GetTimeframeSummaryAsync("BTCUSDT", request.Interval, cancellationToken);
            var summary1hTask = marketDataService.GetTimeframeSummaryAsync("BTCUSDT", "1h", cancellationToken);
            var summary4hTask = marketDataService.GetTimeframeSummaryAsync("BTCUSDT", "4h", cancellationToken);
            var fundingTask   = marketDataService.GetFundingRateAsync("BTCUSDT", cancellationToken);
            var oiTask        = marketDataService.GetOpenInterestAsync("BTCUSDT", cancellationToken);

            await Task.WhenAll(historyTask, signalTfTask, summary1hTask, summary4hTask, fundingTask, oiTask);

            var (totalCount, entryHitCount, winCount, avgMaxProfit) = historyTask.Result;
            var signalTf  = signalTfTask.Result;
            var summary1h = summary1hTask.Result;
            var summary4h = summary4hTask.Result;
            var funding   = fundingTask.Result;
            var oi        = oiTask.Result;

            // 2. Build prompt and call Claude
            var result = await anthropicService.SendMessagesWithToolAsync(
                BuildSystemPrompt(),
                BuildUserMessage(request, signalTf, summary1h, summary4h, funding, oi,
                                 totalCount, entryHitCount, winCount, avgMaxProfit),
                BuildEvaluateSignalTool(),
                cancellationToken);

            if (result is null) return;

            // 3. Persist — INSERT only, no Signal entity load needed
            var evaluation = new SignalAiEvaluation
            {
                SignalId       = request.SignalId,
                Recommendation = result.Input.GetProperty("recommendation").GetString()!,
                RiskScore      = result.Input.GetProperty("risk_score").GetInt32(),
                RiskLevel      = result.Input.GetProperty("risk_level").GetString()!,
                Reasoning      = result.Input.GetProperty("reasoning").GetString()!,
                KeyFactors     = result.Input.GetProperty("key_factors").GetRawText(),
                EvaluatedAt    = DateTime.UtcNow,
            };

            dbContext.SignalAiEvaluations.Add(evaluation);
            await dbContext.SaveChangesAsync(cancellationToken);

            // 4. Telegram follow-up
            await notifier.Notify(BuildTelegramMessage(evaluation, request), cancellationToken);
        }
        catch (Exception ex)
        {
            logTrace.LogError("EvaluateSignalRisk failed for Signal {Id}", request.SignalId, ex);
            throw;  // re-throw so MassTransit retries the message
        }
    }

    private async Task<(int total, int entryHit, int wins, double avgProfit)> FetchHistoryAsync(
        EvaluateSignalRiskCommand request, CancellationToken cancellationToken)
    {
        var history = await dbContext.Signals
            .Where(s => s.SignalType.ToString() == request.SignalType
                     && s.Interval == request.Interval
                     && s.CreatedAt >= DateTime.UtcNow.AddDays(-90)
                     && s.Id != request.SignalId)
            .Select(s => new { s.EntryHitAt, s.StopLossHitAt, s.MaxProfit })
            .ToListAsync(cancellationToken);

        var entryHitCount = history.Count(s => s.EntryHitAt != null);
        var winCount      = history.Count(s => s.EntryHitAt != null && s.StopLossHitAt == null);
        var avgMaxProfit  = history.Where(s => s.EntryHitAt != null)
                                   .Select(s => (double)s.MaxProfit)
                                   .DefaultIfEmpty(0).Average();
        return (history.Count, entryHitCount, winCount, avgMaxProfit);
    }
}
```

### System prompt
```
You are a professional cryptocurrency trading risk analyst specialising in RSI divergence
signals on Bitcoin (BTCUSDT perpetual futures). Evaluate the provided trading signal using
the evaluate_signal tool to record your structured assessment. Base your assessment solely
on the data provided. Be concise and data-driven.
```

### User message template

```
Evaluate this BTCUSDT RSI divergence signal:

Signal:
- Direction: {SignalType} ({Long=Bullish trough | Short=Bearish peak} RSI divergence)
- Timeframe: {Interval}
- Detected: {DetectedAt:yyyy-MM-dd HH:mm} UTC ({session} session)
- RSI at divergence candle: {RsiValue:F2}
- RSI at previous {peak|trough}: {PreviousRsiValue:F2}
- RSI divergence delta: {|rsiDelta|:F2} ({improving|worsening} momentum)

Trade Levels:
- Entry: {EntryPrice:F2} USDT
- Stop Loss: {StopLoss:F2} ({slPct:+F1}% raw | {slPct * Leverage:F1}% at {Leverage}x leverage) = {slAtr:F2}x ATR(14)
- Take Profit: {TakeProfit:F2} ({tpPct:+F1}% raw | {tpPct * Leverage:F1}% at {Leverage}x leverage)
- Risk/Reward: 1:{rrRatio:F2}

Price Context — {Interval} (last 20 candles):
- Trend: {signalTf.Trend}
- RSI(14): {signalTf.Rsi14:F1}
- Price position in 20-candle range: {signalTf.PositionInRangePct}%
- Last candle volume: {signalTf.VolumeLast:F0} BTC ({signalTf.VolumeLast / signalTf.VolumeAvg20:F1}x 20-period avg)
- ATR(14): {signalTf.Atr14:F2} USDT

Higher Timeframe Alignment:
- 1h: Trend={summary1h.Trend}, RSI(14)={summary1h.Rsi14:F1}, Price position={summary1h.PositionInRangePct}%
- 4h: Trend={summary4h.Trend}, RSI(14)={summary4h.Rsi14:F1}, Price position={summary4h.PositionInRangePct}%

Market Conditions:
- Funding rate (8h): {funding.Rate:+0.000%;-0.000%} ({funding.Sentiment})
- Open interest: {oi.CurrentUsdBillions:F2}B USDT ({oi.Change4hPct:+F1}% over 4h)

Historical Performance (last 90 days, {Interval} {SignalType} signals):
- Total similar signals: {totalCount}
- Entry hit rate: {entryRate:P0} ({entryHitCount}/{totalCount})
- Win rate (of entered): {winRate:P0} ({winCount}/{entryHitCount})
- Avg max leveraged profit when entered: {avgMaxProfit:F2}%
```

**Session derivation** (computed from `DetectedAt.Hour` UTC — no API call):

| UTC Hour | Session |
|---|---|
| 00–07 | Asia |
| 08–12 | London open |
| 13–16 | New York open |
| 17–20 | New York / London close |
| 21–23 | Asia open |

**SL ATR multiple** = `Abs(EntryPrice - StopLoss) / signalTf.Atr14` — gives the LLM context on whether the SL is tight (< 1× ATR, easily stopped out) or wide (> 2× ATR).

### Tool definition (`evaluate_signal`)
```json
{
  "name": "evaluate_signal",
  "description": "Record the structured risk evaluation for this trading signal",
  "input_schema": {
    "type": "object",
    "properties": {
      "recommendation": {
        "type": "string",
        "enum": ["Trade", "Skip", "Monitor"],
        "description": "Trade=high conviction; Skip=avoid; Monitor=watch but do not enter yet"
      },
      "risk_score": {
        "type": "integer", "minimum": 0, "maximum": 100,
        "description": "0=minimal risk, 100=extremely high risk"
      },
      "risk_level": {
        "type": "string",
        "enum": ["Low", "Medium", "High", "VeryHigh"]
      },
      "reasoning": {
        "type": "string",
        "description": "2–4 sentence assessment of signal quality and key risks"
      },
      "key_factors": {
        "type": "array",
        "items": { "type": "string" },
        "minItems": 2, "maxItems": 5,
        "description": "Bullet-point factors driving the recommendation"
      }
    },
    "required": ["recommendation", "risk_score", "risk_level", "reasoning", "key_factors"]
  }
}
```

### Telegram follow-up message format
```
[{Interval} {SignalType}] AI Risk Assessment
Recommendation: {RECOMMENDATION} ({riskScore}/100 — {riskLevel} Risk)

{reasoning}

Key factors:
• {factor1}
• {factor2}
• {factor3}
```

---

## Step 8 — Lib.EventBus + Application: MassTransit Wiring

**`src/Cex/Cex.Application/Signals/Commands/FindSignal/FoundSignalEvent.cs`** — already implemented; class (not record) with primary constructor taking `Signal`:
```csharp
namespace Cex.Application.Signals.Commands.FindSignal;

public class FoundSignalEvent(Signal signal)
{
    public int SignalId { get; set; } = signal.Id;
    public string Symbol { get; set; } = signal.Symbol;
    public string Interval { get; set; } = signal.Interval;
    public SignalType SignalType { get; set; } = signal.SignalType;
    public DateTime DetectedAt { get; set; } = signal.DetectedAt;
    public DateTime PreviousCandleAt { get; set; } = signal.PreviousCandleAt;
    public decimal RsiValue { get; set; } = signal.RsiValue;
    public decimal PreviousRsiValue { get; set; } = signal.PreviousRsiValue;
    public decimal EntryPrice { get; set; } = signal.EntryPrice;
    public decimal StopLoss { get; set; } = signal.StopLoss;
    public decimal TakeProfit { get; set; } = signal.TakeProfit;
    public int Leverage { get; set; } = signal.Leverage;
}
```

**`src/Cex/Cex.Application/Signals/Commands/FindSignal/FoundSignalEventConsumer.cs`** — already scaffolded; needs `EvaluateSignalRiskCommand` dispatch added:
```csharp
namespace Cex.Application.Signals.Commands.FindSignal;

public class FoundSignalEventConsumer(IServiceScopeFactory scopeFactory)
    : IConsumer<FoundSignalEvent>
{
    public async Task Consume(ConsumeContext<FoundSignalEvent> context)
    {
        var msg = context.Message;
        using var scope = scopeFactory.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        await mediator.Send(new EvaluateSignalRiskCommand(
            SignalId:         msg.SignalId,
            SignalType:       msg.SignalType.ToString(),
            Interval:         msg.Interval,
            DetectedAt:       msg.DetectedAt,
            RsiValue:         msg.RsiValue,
            PreviousRsiValue: msg.PreviousRsiValue,
            EntryPrice:       msg.EntryPrice,
            StopLoss:         msg.StopLoss,
            TakeProfit:       msg.TakeProfit,
            Leverage:         msg.Leverage),
            context.CancellationToken);
    }
}
```

**`src/Libs/Lib.EventBus/DependencyInjection.cs`** — already implemented; `AddLibEventBusServices` owns the single `AddMassTransit` call and accepts a delegate for consumer registration:
```csharp
services.AddLibEventBusServices(configuration, busConfig =>
{
    busConfig.AddConsumer<FoundSignalEventConsumer>();
});
```

**`src/WebAPI/Program.cs`** — already wired; passes consumer delegate to `AddLibEventBusServices`:
```csharp
builder.Services.AddLibEventBusServices(builder.Configuration, busConfig =>
{
    busConfig.AddConsumer<FoundSignalEventConsumer>();
});
```

**`docker-compose.yml`** and **`credentials/qex/docker-compose.yml`** — RabbitMQ service already added with healthcheck; `qex.api` already has `depends_on: rabbitmq: condition: service_healthy`.

**`credentials/appsettings.json`** — add:
```json
"Anthropic": {
  "ApiKey": "sk-ant-api03-...",
  "Model": "claude-haiku-4-5-20251001",
  "MaxTokens": 1024
},
"RabbitMq": {
  "Host": "amqp://qex-rabbitmq:5672",
  "Username": "guest",
  "Password": "guest"
}
```

---

## Step 9 — Backend DTO

**`SignalDto.cs`** — add after `StopLossHitAfterMinutes`:
```csharp
public string? AiRecommendation { get; init; }
public int? AiRiskScore { get; init; }
public string? AiRiskLevel { get; init; }
public string? AiReasoning { get; init; }
public string? AiKeyFactors { get; init; }   // JSON string; frontend parses to string[]
public long? AiEvaluatedAt { get; init; }    // Unix ms
```

**`GetSignalsQueryHandler.cs`** — LEFT JOIN `SignalAiEvaluations` and project:
```csharp
// In query — join evaluation table
from s in dbContext.Signals
join e in dbContext.SignalAiEvaluations on s.Id equals e.SignalId into evalGroup
from e in evalGroup.DefaultIfEmpty()   // LEFT JOIN

// In Select:
AiRecommendation = e != null ? e.Recommendation : null,
AiRiskScore      = e != null ? e.RiskScore : (int?)null,
AiRiskLevel      = e != null ? e.RiskLevel : null,
AiReasoning      = e != null ? e.Reasoning : null,
AiKeyFactors     = e != null ? e.KeyFactors : null,
AiEvaluatedAt    = e != null ? e.EvaluatedAt.ToUnixTimestampMilliseconds() : (long?)null,
```

---

## Step 10 — Frontend

**`types.ts`** — extend `SignalDto`:
```typescript
aiRecommendation: 'Trade' | 'Skip' | 'Monitor' | null;
aiRiskScore: number | null;
aiRiskLevel: 'Low' | 'Medium' | 'High' | 'VeryHigh' | null;
aiReasoning: string | null;
aiKeyFactors: string | null;  // JSON string → parse to string[]
aiEvaluatedAt: number | null;
```

**`components/AiRiskChip.tsx`** (new component):
```tsx
const COLOR_MAP = { Trade: 'success', Monitor: 'warning', Skip: 'default' } as const;

export default function AiRiskChip({ recommendation, riskScore, reasoning, aiKeyFactors }) {
  if (!recommendation) {
    return <Typography variant="body2" color="text.disabled">—</Typography>;
  }
  const factors: string[] = aiKeyFactors ? JSON.parse(aiKeyFactors) : [];
  return (
    <Tooltip arrow title={
      <Box sx={{ maxWidth: 320, p: 0.5 }}>
        <Typography variant="caption" display="block" sx={{ mb: 0.5 }}>{reasoning}</Typography>
        {factors.map((f) => (
          <Typography key={f} variant="caption" display="block">• {f}</Typography>
        ))}
      </Box>
    }>
      <Chip
        label={`${recommendation} ${riskScore}`}
        color={COLOR_MAP[recommendation]}
        size="small"
        sx={{ fontWeight: 600, cursor: 'default' }}
      />
    </Tooltip>
  );
}
```

**`index.tsx`** — add to `COLUMNS` (after "SL Hit"):
```typescript
{ id: 'aiRisk', label: 'AI Risk', align: 'center' },
```

Add to row transformation in `useMemo`:
```typescript
aiRisk: (
  <AiRiskChip
    recommendation={item.aiRecommendation}
    riskScore={item.aiRiskScore}
    reasoning={item.aiReasoning}
    aiKeyFactors={item.aiKeyFactors}
  />
),
```

---

## Verification

1. **Infrastructure**: `docker compose up -d rabbitmq` — management UI at `http://localhost:15672`; `found-signal-event` queue appears after first publish
2. **Migration**: `dotnet ef migrations add AddSignalAiEvaluation && dotnet ef database update` — verify new `SignalAiEvaluations` table with UNIQUE index on `SignalId`; `Signals` table unchanged
3. **Market data**: Call `IMarketDataService` directly in a unit test with symbol `BTCUSDT` — verify `TimeframeSummary.Trend` is one of the three valid values; confirm Binance rate limits are not hit (6 parallel calls per evaluation is well within the 2,400 req/min limit)
4. **Build**: `dotnet build` — zero errors; `IAnthropicService`, `IMarketDataService`, and `ISignalEventPublisher` resolve via DI
5. **End-to-end**: Let `FindSignalService` detect a signal. Verify: message appears in RabbitMQ → consumed → new row in `SignalAiEvaluations` → Telegram follow-up includes session, trend alignment, and funding rate context
6. **Idempotency**: Re-publish `SignalDetectedEvent` for an already-evaluated signal. Confirm: `AnyAsync` returns true → handler returns early; no duplicate row or notification
7. **Market data failure**: Take `fapi.binance.com` offline (block in `/etc/hosts`). Confirm: `MarketDataService` returns safe defaults → evaluation still completes with zeroed market context → no exception thrown
8. **Graceful failure**: Set invalid Anthropic API key. Confirm: exception re-thrown → MassTransit retries → dead-lettered; no row in `SignalAiEvaluations`; `FindSignalService` unaffected
9. **Frontend**: `npm run build` — zero TS errors. Evaluated signals show colored `AiRiskChip`; unevaluated show `—`. Tooltip renders reasoning + key factors
