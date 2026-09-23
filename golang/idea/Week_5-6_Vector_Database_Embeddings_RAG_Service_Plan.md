# Week 5–6: Vector Database, Embeddings, and RAG Service Implementation Plan

## 1. Objective

The goal of Week 5–6 is to build the retrieval foundation for the AI Risk Manager.

By the end of this phase, the system should be able to:

1. Collect completed trade and risk-history data from the Journal Service.
2. Convert useful historical trading experiences into structured documents.
3. Generate embeddings for those documents.
4. Store embeddings and metadata in a vector database.
5. Search historical experiences using semantic similarity.
6. Apply metadata filters such as symbol, timeframe, strategy, side, outcome, and time range.
7. Expose a RAG Service that retrieves relevant historical context for a future LLM Risk Manager.
8. Keep retrieval deterministic, traceable, versioned, and reproducible.
9. Evaluate retrieval quality before introducing the LLM.

The LLM is **not part of the core Week 5–6 implementation**. The primary objective is to make the retrieval layer reliable first.

---

## 2. Scope

### In Scope

- Historical candidate trades
- Risk decisions
- Orders and fills
- Completed trades
- Trade outcomes
- Relevant market/indicator snapshots
- Relevant portfolio/risk context
- RAG document construction
- Document chunking strategy
- Embedding generation
- Embedding model configuration
- Vector database schema
- Metadata filtering
- Similarity search
- Top-K retrieval
- Retrieval scoring
- RAG Service API
- Retrieval logging
- Embedding/version management
- Re-indexing support
- Retrieval evaluation

### Out of Scope

Do not implement:

- LLM Risk Manager
- LLM-generated BUY/SELL signals
- Autonomous prompt optimization
- Fine-tuning
- Reinforcement learning
- AI-based position sizing
- AI-based hard-risk-limit decisions
- Real-money AI trading
- Complex multi-agent architecture

The output of this phase should be a reliable retrieval service that can later be consumed by the LLM Risk Manager.

---

## 3. Target Architecture

```text
                    Journal Service
                           |
                           v
                Historical Trade Data
                           |
                           v
                RAG Document Builder
                           |
                           v
                  Embedding Service
                           |
                           v
                  Vector Database
                           |
                    +------+------+
                    |             |
                    v             v
             Metadata Filter   Similarity Search
                    |             |
                    +------+------+
                           |
                           v
                     RAG Service
                           |
                           v
                 Retrieved Context
                           |
                           v
             Future LLM Risk Manager
```

Key principle:

> The RAG Service retrieves evidence. It does not make trading decisions.

---

## 4. Recommended Technology Direction

### Vector Database

Recommended initial option:

- PostgreSQL
- `pgvector`

Reasons:

- PostgreSQL is already suitable for the project.
- Trading data and vector data can coexist.
- Metadata filtering is straightforward.
- Operational complexity is lower than introducing a separate vector database.
- It is sufficient for an initial trading-history corpus.

A dedicated vector database can be introduced later if scale or retrieval requirements justify it.

### Embedding Provider

Create an abstraction rather than coupling the RAG Service directly to one provider.

```go
type EmbeddingProvider interface {
    Embed(ctx context.Context, texts []string) ([][]float32, error)
}
```

This allows the implementation to switch providers/models without changing the RAG domain.

---

# 5. Week 5–6 Work Breakdown

## Week 5

### Day 1–2: Define RAG Data Model

Define the historical information that should become retrievable knowledge.

Create a canonical RAG document model.

```go
type RAGDocument struct {
    ID               string
    DocumentType     string
    Symbol           string
    Timeframe        string
    StrategyName     string
    StrategyVersion  string
    Side             string

    TradeID          string
    CandidateTradeID string

    Content          string
    Metadata         map[string]any

    Outcome          string
    PnL              float64

    EventTime        time.Time
    CreatedAt        time.Time

    EmbeddingModel   string
    EmbeddingVersion string
}
```

The document should contain both:

- Human-readable content for semantic search.
- Structured metadata for deterministic filtering.

---

## Day 2: Define What Becomes a RAG Document

Do not blindly embed every database row.

Create meaningful retrieval units.

### 1. Completed Trade

Include:

- Symbol
- Timeframe
- Strategy
- Strategy version
- Side
- Entry price
- Exit price
- Holding duration
- Indicators at entry
- Risk context
- Position size
- Risk decision
- Outcome
- PnL
- Maximum favorable excursion if available
- Maximum adverse excursion if available

Example:

```text
BTC/USDT 5m long trade.

Strategy: RSI_BB
Strategy version: v1

Entry conditions:
- RSI: 27.4
- Price below lower Bollinger Band
- BB Width: 0.021
- RSI slope: +1.8

Risk context:
- Current exposure: 8.2%
- Daily loss: -0.7%
- Open positions: 2

Risk decision:
- Approved
- Position size reduced to 60%

Outcome:
- Closed after 35 minutes
- PnL: +0.84%
```

### 2. Risk Decision

Store a separate document when the risk decision contains useful information.

```text
Candidate trade was rejected because portfolio exposure exceeded
the configured maximum exposure for the strategy.

Symbol: BTC/USDT
Timeframe: 5m
Strategy: RSI_BB
Exposure before decision: 19.2%
Maximum exposure: 20%
Existing correlated positions: ETH/USDT
```

### 3. Trade Pattern / Aggregate Summary

Do not build this initially unless enough historical data exists.

Later, aggregate documents can describe:

- Similar RSI/BB setups
- Performance by market regime
- Strategy performance by timeframe
- Performance after large volatility expansion

---

## Day 2–3: Metadata Design

Metadata is critical.

Recommended metadata:

```text
symbol
timeframe
strategy_name
strategy_version
side
document_type
outcome
event_time
trade_id
candidate_trade_id
pnl
holding_duration
market_regime
risk_decision
```

Example:

```json
{
  "symbol": "BTC/USDT",
  "timeframe": "5m",
  "strategy_name": "RSI_BB",
  "strategy_version": "v1",
  "side": "BUY",
  "document_type": "completed_trade",
  "outcome": "WIN",
  "market_regime": "HIGH_VOLATILITY"
}
```

Important rule:

> Metadata should be used for hard filtering; embeddings should be used for semantic similarity.

---

## Day 3: Document Content Rules

Embedding content should be stable and deterministic.

Do not include unnecessary dynamic values such as:

- Random IDs
- Request IDs
- Trace IDs
- Database timestamps that do not describe the trade
- Internal infrastructure details

The same historical trade should generate the same canonical content.

Example:

```text
Trade Pattern:
BTC/USDT
5m
LONG

Strategy:
RSI_BB v1

Indicators:
RSI=27.4
BBWidth=0.021
BBPercentB=-0.04
RSISlope=1.8

Risk:
Exposure=8.2%
DailyPnL=-0.7%
RiskDecision=APPROVED
PositionSizeMultiplier=0.60

Outcome:
PnL=0.84%
Duration=35m
Result=WIN
```

---

## Day 3–4: Document Versioning

Document construction must be versioned.

Example:

```text
document_schema_version = 1
embedding_model = <model-name>
embedding_model_version = <version>
```

Recommended fields:

```go
type EmbeddingMetadata struct {
    DocumentSchemaVersion string
    EmbeddingModel        string
    EmbeddingModelVersion string
}
```

If the document format or embedding model changes, old vectors should not silently be mixed with new vectors.

---

## Day 4: Vector Database Schema

Example PostgreSQL design:

```sql
CREATE EXTENSION IF NOT EXISTS vector;

CREATE TABLE rag_documents (
    id UUID PRIMARY KEY,

    document_type VARCHAR(50) NOT NULL,

    symbol VARCHAR(50) NOT NULL,
    timeframe VARCHAR(20) NOT NULL,

    strategy_name VARCHAR(100),
    strategy_version VARCHAR(50),
    side VARCHAR(20),

    trade_id UUID,
    candidate_trade_id UUID,

    content TEXT NOT NULL,

    outcome VARCHAR(30),
    pnl NUMERIC,

    event_time TIMESTAMPTZ NOT NULL,
    created_at TIMESTAMPTZ NOT NULL,

    document_schema_version VARCHAR(20) NOT NULL,
    embedding_model VARCHAR(100) NOT NULL,
    embedding_model_version VARCHAR(50) NOT NULL,

    metadata JSONB NOT NULL,

    embedding VECTOR(<DIMENSION>)
);
```

The actual vector dimension must match the selected embedding model.

---

## Day 4–5: Vector and Metadata Indexes

Example:

```sql
CREATE INDEX idx_rag_documents_embedding
ON rag_documents
USING hnsw (embedding vector_cosine_ops);
```

Metadata indexes:

```sql
CREATE INDEX idx_rag_documents_symbol
ON rag_documents(symbol);

CREATE INDEX idx_rag_documents_strategy
ON rag_documents(strategy_name);

CREATE INDEX idx_rag_documents_event_time
ON rag_documents(event_time);

CREATE INDEX idx_rag_documents_metadata
ON rag_documents
USING gin(metadata);
```

Do not optimize prematurely. Measure retrieval performance with realistic data.

---

## Day 5: Embedding Service

Create:

```go
type EmbeddingProvider interface {
    Embed(ctx context.Context, texts []string) ([][]float32, error)
}
```

Responsibilities:

1. Accept canonical document text.
2. Call the embedding provider.
3. Validate vector dimensions.
4. Return vectors.
5. Handle provider errors.
6. Support batching.
7. Respect rate limits.
8. Support retries where appropriate.

Do not let the rest of the application depend directly on the external embedding SDK.

---

## Day 5: Batch Embedding

Prefer batching instead of one API call per trade when supported.

```text
1000 historical trades
       |
       v
Split into batches
       |
       v
EmbeddingProvider
       |
       v
1000 vectors
       |
       v
Bulk insert
```

Add configurable parameters:

```text
EmbeddingBatchSize
EmbeddingConcurrency
EmbeddingRetryCount
EmbeddingTimeout
```

Avoid unlimited concurrency because the provider may enforce rate limits.

---

# 6. Week 6 Work Breakdown

## Day 6–7: Historical Backfill

Create a backfill process:

```text
Journal DB
   |
   v
Load completed trades
   |
   v
Build RAG documents
   |
   v
Generate embeddings
   |
   v
Insert into vector DB
```

The process should be:

- Resumable
- Idempotent
- Observable
- Rate-limit aware

Use a deterministic document key such as:

```text
trade_id + document_type + document_schema_version + embedding_model_version
```

This prevents duplicate vectors when a backfill is restarted.

---

## Day 7: Incremental Indexing

After historical backfill works, implement incremental indexing.

```text
TradeCompleted
      |
      v
RAG Document Builder
      |
      v
Embedding Provider
      |
      v
Vector DB
```

A completed trade should eventually become searchable.

If the same `TradeCompleted` event is processed twice, it must not create duplicate documents.

---

## Day 8: RAG Retrieval Interface

Create:

```go
type RetrievalRequest struct {
    Query string

    Symbol          string
    Timeframe       string
    StrategyName    string
    StrategyVersion string
    Side            string
    DocumentType    string

    TopK int
}
```

Result:

```go
type RetrievedDocument struct {
    DocumentID string
    Content    string
    Similarity float64
    Metadata   map[string]any
}
```

Interface:

```go
type Retriever interface {
    Retrieve(
        ctx context.Context,
        request RetrievalRequest,
    ) ([]RetrievedDocument, error)
}
```

---

## Day 8: Similarity Search

Conceptual SQL:

```sql
SELECT
    id,
    content,
    metadata,
    1 - (embedding <=> $1) AS similarity
FROM rag_documents
WHERE symbol = $2
  AND timeframe = $3
ORDER BY embedding <=> $1
LIMIT $4;
```

The exact SQL depends on the selected distance metric and pgvector configuration.

Important:

> Similarity is not a trading confidence score. It only indicates semantic closeness.

---

## Day 8–9: Metadata Filtering

Example request:

```text
Query:
"Find historical trades similar to this oversold BTC setup."

Filters:
Symbol = BTC/USDT
Timeframe = 5m
Strategy = RSI_BB
Side = BUY
DocumentType = completed_trade
TopK = 10
```

This is safer than searching the entire trading history.

---

## Day 9: Query Construction

The RAG query should be generated from the current CandidateTrade + RiskContext.

Example:

```text
Current market setup:

Symbol: BTC/USDT
Timeframe: 5m
Side: BUY

Strategy:
RSI_BB v1

Indicators:
RSI=28.1
BBWidth=0.019
BBPercentB=-0.03
RSISlope=1.2

Risk:
Current exposure=7.4%
Daily PnL=-0.4%
Open positions=1

Find historical completed trades with similar market conditions
and risk context.
```

The query should contain meaningful trading context, not simply:

```text
BTC BUY
```

---

## Day 9: Top-K Strategy

Start simple:

```text
TopK = 5–10
```

Do not automatically send 50–100 historical trades to the future LLM.

Too many results can:

- Increase prompt size
- Add irrelevant context
- Increase token cost
- Make the context harder to interpret

Later, evaluate whether re-ranking is necessary.

---

## Day 10: RAG Service API

Example:

```http
POST /api/v1/retrieval/search
```

Request:

```json
{
  "query": "Historical trades similar to this oversold BTC setup",
  "symbol": "BTC/USDT",
  "timeframe": "5m",
  "strategyName": "RSI_BB",
  "strategyVersion": "v1",
  "side": "BUY",
  "documentType": "completed_trade",
  "topK": 5
}
```

Response:

```json
{
  "results": [
    {
      "documentId": "trade-123",
      "similarity": 0.91,
      "content": "...",
      "metadata": {
        "symbol": "BTC/USDT",
        "timeframe": "5m",
        "outcome": "WIN",
        "pnl": 0.84
      }
    }
  ]
}
```

---

# 7. Suggested Go Package Structure

```text
internal/
├── rag/
│   ├── document.go
│   ├── document_builder.go
│   ├── embedding.go
│   ├── embedding_provider.go
│   ├── retriever.go
│   ├── retrieval_request.go
│   ├── retrieval_result.go
│   ├── repository.go
│   ├── backfill.go
│   └── service.go
│
├── vectorstore/
│   ├── pgvector.go
│   ├── repository.go
│   └── mapper.go
│
└── journal/
    └── ...
```

Keep vector database implementation details outside the RAG domain logic.

---

# 8. Testing Strategy

Testing should focus on contracts and observable behavior.

## Test 1 — Document Builder

Input:

```text
Completed Trade
+ Risk Context
+ Candidate Trade
+ Trade Outcome
```

Expected:

```text
Deterministic RAGDocument
```

Verify:

- Required fields
- Stable content
- Correct metadata
- Correct document type
- Correct version

---

## Test 2 — Embedding Provider

Use a fake embedding provider.

```go
type FakeEmbeddingProvider struct {
    Embedding []float32
}
```

Verify:

- Input is passed correctly
- Returned vector dimension is validated
- Provider errors are propagated
- Batching works

Do not call the real embedding API in unit tests.

---

## Test 3 — Vector Repository

Test:

- Insert
- Upsert/idempotency
- Metadata filtering
- Similarity ordering
- Top-K behavior

A PostgreSQL integration test is appropriate here.

---

## Test 4 — Retrieval Service

Input:

```text
RetrievalRequest
```

Expected:

```text
Relevant documents ordered by similarity
```

Verify:

- Metadata filters
- Top-K
- Similarity values
- Empty results
- Invalid requests
- Repository failures

---

## Test 5 — Backfill Idempotency

Run:

```text
Backfill()
Backfill()
```

Expected:

```text
Same number of documents
No duplicates
```

---

## Test 6 — Deterministic Document Generation

Given the same historical trade:

```text
Trade A
   ↓
BuildDocument()
   ↓
Document A

Trade A
   ↓
BuildDocument()
   ↓
Document B
```

Assert:

```text
Document A.Content == Document B.Content
Document A.Metadata == Document B.Metadata
```

---

# 9. Retrieval Quality Evaluation

A vector database being operational does not mean RAG is useful.

Create a small evaluation dataset.

Example:

```text
Query 1:
BTC 5m RSI oversold + price below lower BB

Expected relevant:
historical BTC 5m RSI_BB trades
```

For each query, manually define relevant historical documents.

Measure:

- Precision@K
- Recall@K
- Hit Rate@K
- Average similarity
- Filter correctness

Start with:

```text
K = 3
K = 5
K = 10
```

Compare retrieval quality.

---

# 10. Important Retrieval Quality Cases

### Case 1 — Same setup, same symbol

```text
BTC 5m RSI_BB BUY
```

Similar BTC 5m RSI_BB trades should rank highly.

### Case 2 — Same setup, different symbol

```text
ETH 5m RSI_BB BUY
```

The system should not return BTC-only results when symbol filtering is required.

### Case 3 — Same symbol, different timeframe

```text
BTC 1h RSI_BB BUY
```

Should not contaminate a strict BTC 5m query when timeframe filtering is enabled.

### Case 4 — Same market setup, different outcome

The retriever should be capable of returning both:

- winning historical trades
- losing historical trades

Do not design the retrieval system to retrieve only profitable examples.

This is important for avoiding confirmation bias.

---

# 11. Avoid Outcome Bias

Do not build:

```text
Current BUY setup
        ↓
Search only winning trades
        ↓
LLM sees mostly successful trades
        ↓
LLM becomes overly optimistic
```

Instead, retrieve representative historical outcomes.

Example:

```text
Similar trades:
- WIN +1.2%
- LOSS -0.8%
- WIN +0.6%
- LOSS -1.1%
- BREAKEVEN +0.1%
```

The future LLM should see the actual historical distribution rather than a curated success-only dataset.

---

# 12. Market Regime

If enough data is available, add market regime metadata.

Initial examples:

```text
LOW_VOLATILITY
NORMAL_VOLATILITY
HIGH_VOLATILITY
TRENDING_UP
TRENDING_DOWN
RANGE
```

Keep regime classification deterministic and simple during Week 5–6.

---

# 13. Observability

Every retrieval request should be traceable.

Record:

```text
request_id
candidate_trade_id
symbol
timeframe
strategy
query
filters
top_k
returned_document_ids
similarity_scores
retrieval_latency
embedding_model
document_schema_version
```

This will be useful when evaluating future LLM decisions.

The desired trace is:

```text
CandidateTrade
      |
      v
RAG Request
      |
      v
Retrieved Documents
      |
      v
Future LLM Risk Decision
```

---

# 14. Error Handling

The RAG system must fail safely.

### Embedding provider unavailable

Do not create incomplete vectors.

### Vector database unavailable

Return an explicit retrieval failure.

### No historical matches

Return:

```text
results = []
```

Do not fabricate historical evidence.

### Embedding dimension mismatch

Reject the document.

### Duplicate document

Treat as idempotent rather than creating another vector.

---

# 15. RAG Failure Policy

Important architectural rule:

> RAG failure must never bypass deterministic risk controls.

If the future LLM depends on RAG and RAG is unavailable:

```text
CandidateTrade
      ↓
Hard Risk Engine
      ↓
RAG unavailable
      ↓
LLM Risk Manager cannot make a contextual decision
```

Follow an explicitly configured fail-safe policy, such as:

```text
REJECT
```

or:

```text
REQUIRE MANUAL REVIEW
```

Do not allow:

```text
RAG unavailable → automatically approve trade
```

---

# 16. Security and Data Hygiene

Do not embed:

- API keys
- Access tokens
- Secrets
- Private credentials
- Personal information
- Internal authentication tokens

Only trading information required for retrieval should be included.

---

# 17. Performance Targets

Initial targets should be practical rather than aggressive.

### Indexing

Process historical trades in batches.

### Retrieval

A reasonable initial development target is approximately:

```text
< 100–200 ms
```

for vector retrieval under a normal development dataset, excluding external embedding generation.

Do not treat this as a production SLA yet. Measure first.

---

# 18. Suggested Implementation Order

```text
1. RAGDocument model
        ↓
2. DocumentBuilder
        ↓
3. EmbeddingProvider interface
        ↓
4. FakeEmbeddingProvider
        ↓
5. PostgreSQL + pgvector
        ↓
6. VectorRepository
        ↓
7. Historical backfill
        ↓
8. Similarity search
        ↓
9. Metadata filtering
        ↓
10. RetrievalService
        ↓
11. Incremental indexing
        ↓
12. Retrieval evaluation
        ↓
13. Observability
        ↓
14. Performance testing
```

Do not start with the LLM.

---

# 19. Definition of Done

## Data

- [ ] Completed trades can be converted into RAG documents.
- [ ] Risk decisions can be converted into RAG documents where useful.
- [ ] Documents contain structured metadata.
- [ ] Document generation is deterministic.
- [ ] Document schema is versioned.

## Embeddings

- [ ] Embedding provider abstraction exists.
- [ ] Embeddings can be generated in batches.
- [ ] Vector dimensions are validated.
- [ ] Provider errors are handled.
- [ ] Embedding model/version is stored.

## Vector Database

- [ ] PostgreSQL + pgvector is configured.
- [ ] Vector schema exists.
- [ ] Vector index exists.
- [ ] Metadata indexes exist.
- [ ] Upsert/idempotency works.

## Backfill

- [ ] Historical trades can be backfilled.
- [ ] Backfill is resumable.
- [ ] Backfill is idempotent.
- [ ] Duplicate documents are prevented.

## Retrieval

- [ ] Similarity search works.
- [ ] Metadata filtering works.
- [ ] Top-K works.
- [ ] Empty results are handled.
- [ ] Retrieval results contain similarity scores.
- [ ] Retrieval is traceable to CandidateTrade.

## Quality

- [ ] Retrieval evaluation dataset exists.
- [ ] Precision@K / Recall@K or Hit Rate@K is measured.
- [ ] Same-symbol and same-timeframe filtering is verified.
- [ ] Winning and losing historical trades can both be retrieved.
- [ ] No fabricated historical results are possible.

## Safety

- [ ] RAG cannot bypass hard risk controls.
- [ ] RAG failure has an explicit fail-safe behavior.
- [ ] Secrets are excluded from embeddings.

---

# 20. Recommended Deliverables

At the end of Week 5–6, the project should contain:

```text
1. RAG document model
2. Document builder
3. Embedding provider interface
4. Embedding provider implementation
5. pgvector schema/migrations
6. Vector repository
7. Historical backfill job
8. Incremental indexing flow
9. Retrieval service
10. Retrieval API
11. Retrieval test suite
12. Retrieval evaluation dataset
13. Retrieval metrics
14. Observability/logging
15. Documentation
```

---

# 21. Final Architecture After Week 6

```text
Market Data
    ↓
Candle Engine
    ↓
Indicator Engine
    ↓
Strategy Engine
    ↓
Candidate Trade
    ↓
Portfolio / Risk Context
    ↓
Deterministic Hard Risk
    ↓
        ┌─────────────────────┐
        │                     │
        │    RAG Service      │
        │                     │
        │  Vector Database    │
        │       ↑             │
        │   Embeddings        │
        │       ↑             │
        │  Trading Journal    │
        │                     │
        └─────────┬───────────┘
                  ↓
          Retrieved Context
                  ↓
       Future LLM Risk Manager
```

The key outcome is not simply having a vector database.

The real outcome is:

> Given a CandidateTrade and its RiskContext, the system can reliably retrieve relevant historical trading experiences with traceable metadata and measurable retrieval quality.

This creates the foundation for Week 7–8, where the LLM Risk Manager can consume the retrieved historical context and make a structured risk decision in shadow mode.
