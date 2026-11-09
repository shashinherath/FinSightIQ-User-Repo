-- ==============================================================================
-- Functions and Stored Procedures: Refresh Tokens
-- Database: usersdb
-- Service: FinSightIQ User Service
-- Rule: Functions for GET/POST, Procedures for PUT/DELETE
-- ==============================================================================

-- GET: Retrieve refresh token by SHA-256 hash
CREATE OR REPLACE FUNCTION fn_GetRefreshTokenByHash(p_token_hash VARCHAR(64))
RETURNS TABLE (
    id UUID,
    user_id UUID,
    token_hash VARCHAR(64),
    family_id UUID,
    expires_at TIMESTAMPTZ,
    used_at TIMESTAMPTZ,
    revoked_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ
)
LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    SELECT r.id, r.user_id, r.token_hash, r.family_id, r.expires_at, r.used_at, r.revoked_at, r.created_at
    FROM refresh_tokens r
    WHERE r.token_hash = p_token_hash
    LIMIT 1;
END;
$$;

-- POST: Create new refresh token
CREATE OR REPLACE FUNCTION fn_CreateRefreshToken(
    p_id UUID,
    p_user_id UUID,
    p_token_hash VARCHAR(64),
    p_family_id UUID,
    p_expires_at TIMESTAMPTZ,
    p_used_at TIMESTAMPTZ,
    p_revoked_at TIMESTAMPTZ,
    p_created_at TIMESTAMPTZ
)
RETURNS VOID
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO refresh_tokens (
        id, user_id, token_hash, family_id, expires_at, used_at, revoked_at, created_at
    ) VALUES (
        p_id, p_user_id, p_token_hash, p_family_id, p_expires_at, p_used_at, p_revoked_at, p_created_at
    );
END;
$$;

-- POST: Atomically rotate refresh token (mark old as used if unused, insert new token)
-- Returns 1 if old token was rotated, 0 if old token was already used (replay/race)
CREATE OR REPLACE FUNCTION fn_RotateRefreshToken(
    p_old_token_id UUID,
    p_new_id UUID,
    p_user_id UUID,
    p_token_hash VARCHAR(64),
    p_family_id UUID,
    p_expires_at TIMESTAMPTZ,
    p_created_at TIMESTAMPTZ
)
RETURNS INT
LANGUAGE plpgsql
AS $$
DECLARE
    v_rows_updated INT;
BEGIN
    UPDATE refresh_tokens
    SET used_at = now()
    WHERE id = p_old_token_id
      AND used_at IS NULL;

    GET DIAGNOSTICS v_rows_updated = ROW_COUNT;

    IF v_rows_updated > 0 THEN
        INSERT INTO refresh_tokens (
            id, user_id, token_hash, family_id, expires_at, created_at
        ) VALUES (
            p_new_id, p_user_id, p_token_hash, p_family_id, p_expires_at, p_created_at
        );
    END IF;

    RETURN v_rows_updated;
END;
$$;

-- PUT: Mark refresh token as used
CREATE OR REPLACE PROCEDURE sp_MarkRefreshTokenUsed(p_id UUID)
LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE refresh_tokens
    SET used_at = now()
    WHERE id = p_id;
END;
$$;

-- PUT: Revoke single refresh token
CREATE OR REPLACE PROCEDURE sp_RevokeRefreshToken(p_id UUID)
LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE refresh_tokens
    SET revoked_at = now()
    WHERE id = p_id
      AND revoked_at IS NULL;
END;
$$;

-- PUT: Revoke all refresh tokens in a family
CREATE OR REPLACE PROCEDURE sp_RevokeRefreshTokenFamily(p_family_id UUID)
LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE refresh_tokens
    SET revoked_at = now()
    WHERE family_id = p_family_id
      AND revoked_at IS NULL;
END;
$$;

-- PUT: Revoke all refresh tokens for a user
CREATE OR REPLACE PROCEDURE sp_RevokeAllRefreshTokensForUser(p_user_id UUID)
LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE refresh_tokens
    SET revoked_at = now()
    WHERE user_id = p_user_id
      AND revoked_at IS NULL;
END;
$$;
