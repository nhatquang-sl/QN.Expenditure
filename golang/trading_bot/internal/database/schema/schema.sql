CREATE TABLE candidate_trades (
    id               TEXT        NOT NULL PRIMARY KEY,
    symbol           TEXT        NOT NULL,
    timeframe        TEXT        NOT NULL,
    side             TEXT        NOT NULL,
    entry_price      DOUBLE PRECISION NOT NULL,
    strategy_name    TEXT        NOT NULL,
    strategy_version TEXT        NOT NULL,
    rsi              DOUBLE PRECISION NOT NULL,
    bb_upper         DOUBLE PRECISION NOT NULL,
    bb_middle        DOUBLE PRECISION NOT NULL,
    bb_lower         DOUBLE PRECISION NOT NULL,
    bb_width         DOUBLE PRECISION NOT NULL,
    bb_percent_b     DOUBLE PRECISION NOT NULL,
    rsi_slope        DOUBLE PRECISION NOT NULL,
    divergence_type  INTEGER     NOT NULL,
    score            INTEGER     NOT NULL,
    reasons          TEXT[]      NOT NULL,
    idempotency_key  TEXT        NOT NULL,
    created_at       TIMESTAMPTZ NOT NULL,
    UNIQUE (idempotency_key)
);

CREATE TABLE account_balances (
    id         TEXT             NOT NULL PRIMARY KEY,
    asset      TEXT             NOT NULL,
    total      DOUBLE PRECISION NOT NULL,
    available  DOUBLE PRECISION NOT NULL,
    locked     DOUBLE PRECISION NOT NULL,
    updated_at TIMESTAMPTZ      NOT NULL,
    UNIQUE (asset)
);

CREATE TABLE portfolio_snapshots (
    id             TEXT             NOT NULL PRIMARY KEY,
    candidate_id   TEXT             NOT NULL REFERENCES candidate_trades (id),
    equity         DOUBLE PRECISION NOT NULL,
    available_cash DOUBLE PRECISION NOT NULL,
    total_exposure DOUBLE PRECISION NOT NULL,
    unrealized_pnl DOUBLE PRECISION NOT NULL,
    open_positions INTEGER          NOT NULL,
    captured_at    TIMESTAMPTZ      NOT NULL,
    UNIQUE (candidate_id)
);

CREATE TABLE positions (
    id              TEXT             NOT NULL PRIMARY KEY,
    candidate_id    TEXT             NOT NULL REFERENCES candidate_trades (id),
    symbol          TEXT             NOT NULL,
    asset           TEXT             NOT NULL,
    quantity        DOUBLE PRECISION NOT NULL,
    avg_entry_price DOUBLE PRECISION NOT NULL,
    current_price   DOUBLE PRECISION NOT NULL,
    unrealized_pnl  DOUBLE PRECISION NOT NULL,
    created_at      TIMESTAMPTZ      NOT NULL,
    updated_at      TIMESTAMPTZ      NOT NULL,
    UNIQUE (symbol)
);

CREATE TABLE risk_contexts (
    id                    TEXT        NOT NULL PRIMARY KEY,
    candidate_id          TEXT        NOT NULL REFERENCES candidate_trades (id),
    portfolio_snapshot_id TEXT        NOT NULL REFERENCES portfolio_snapshots (id),
    generated_at          TIMESTAMPTZ NOT NULL,
    raw_json              TEXT        NOT NULL,
    UNIQUE (candidate_id)
);

CREATE TABLE risk_decisions (
    id              TEXT             NOT NULL PRIMARY KEY,
    candidate_id    TEXT             NOT NULL REFERENCES candidate_trades (id),
    risk_context_id TEXT             NOT NULL REFERENCES risk_contexts (id),
    decision        TEXT             NOT NULL,
    position_size   DOUBLE PRECISION NOT NULL,
    reasons         TEXT[]           NOT NULL,
    created_at      TIMESTAMPTZ      NOT NULL,
    UNIQUE (candidate_id)
);

CREATE TABLE paper_orders (
    id               TEXT             NOT NULL PRIMARY KEY,
    candidate_id     TEXT             NOT NULL REFERENCES candidate_trades (id),
    risk_decision_id TEXT             NOT NULL REFERENCES risk_decisions (id),
    symbol           TEXT             NOT NULL,
    side             TEXT             NOT NULL,
    quantity         DOUBLE PRECISION NOT NULL,
    fill_price       DOUBLE PRECISION NOT NULL,
    status           TEXT             NOT NULL,
    created_at       TIMESTAMPTZ      NOT NULL,
    updated_at       TIMESTAMPTZ      NOT NULL,
    UNIQUE (candidate_id)
);

CREATE TABLE paper_fills (
    id             TEXT             NOT NULL PRIMARY KEY,
    candidate_id   TEXT             NOT NULL REFERENCES candidate_trades (id),
    paper_order_id TEXT             NOT NULL REFERENCES paper_orders (id),
    quantity       DOUBLE PRECISION NOT NULL,
    price          DOUBLE PRECISION NOT NULL,
    executed_at    TIMESTAMPTZ      NOT NULL,
    UNIQUE (paper_order_id)
);

CREATE TABLE trade_outcomes (
    id            TEXT             NOT NULL PRIMARY KEY,
    candidate_id  TEXT             NOT NULL REFERENCES candidate_trades (id),
    paper_fill_id TEXT             NOT NULL REFERENCES paper_fills (id),
    entry_price   DOUBLE PRECISION NOT NULL,
    exit_price    DOUBLE PRECISION NOT NULL,
    quantity      DOUBLE PRECISION NOT NULL,
    gross_pnl     DOUBLE PRECISION NOT NULL,
    net_pnl       DOUBLE PRECISION NOT NULL,
    closed_at     TIMESTAMPTZ      NOT NULL,
    UNIQUE (candidate_id)
);
