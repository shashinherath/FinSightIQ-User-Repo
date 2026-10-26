-- ==============================================================================
-- Table: refresh_tokens
-- Service: FinSightIQ User Service
-- Database: usersdb
-- ==============================================================================

CREATE TABLE IF NOT EXISTS refresh_tokens (
    id          UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id     UUID         NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    token_hash  VARCHAR(64)  NOT NULL UNIQUE,
    family_id   UUID         NOT NULL,
    expires_at  TIMESTAMPTZ  NOT NULL,
    used_at     TIMESTAMPTZ,
    revoked_at  TIMESTAMPTZ,
    created_at  TIMESTAMPTZ  NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_refresh_tokens_user   ON refresh_tokens (user_id);
CREATE INDEX IF NOT EXISTS ix_refresh_tokens_family ON refresh_tokens (family_id);
CREATE INDEX IF NOT EXISTS ix_refresh_tokens_expiry ON refresh_tokens (expires_at)
    WHERE revoked_at IS NULL;
