-- ==============================================================================
-- Table: users
-- Service: FinSightIQ User Service
-- Database: usersdb
-- ==============================================================================

CREATE TABLE IF NOT EXISTS users (
    id             UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    email          VARCHAR(255) NOT NULL,
    password_hash  VARCHAR(60)  NOT NULL,
    role           VARCHAR(20)  NOT NULL DEFAULT 'Analyst',
    is_suspended   BOOLEAN      NOT NULL DEFAULT false,
    created_at     TIMESTAMPTZ  NOT NULL DEFAULT now(),
    updated_at     TIMESTAMPTZ  NOT NULL DEFAULT now(),
    deleted_at     TIMESTAMPTZ,
    CONSTRAINT ck_users_role CHECK (role IN ('Viewer', 'Analyst', 'Administrator'))
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_users_email_lower
    ON users (lower(email)) WHERE deleted_at IS NULL;
