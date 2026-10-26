-- ==============================================================================
-- Table: watchlists
-- Service: FinSightIQ User Service
-- Database: usersdb
-- ==============================================================================

CREATE TABLE IF NOT EXISTS watchlists (
    id        UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id   UUID         NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    symbol    VARCHAR(10)  NOT NULL,
    added_at  TIMESTAMPTZ  NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_watchlists_user_symbol
    ON watchlists (user_id, symbol);
