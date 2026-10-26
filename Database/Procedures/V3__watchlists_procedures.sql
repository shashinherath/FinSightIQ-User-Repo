-- ==============================================================================
-- Functions and Stored Procedures: Watchlists
-- Database: usersdb
-- Service: FinSightIQ User Service
-- Rule: Functions for GET/POST, Procedures for PUT/DELETE
-- ==============================================================================

-- GET: Retrieve user watchlist items
CREATE OR REPLACE FUNCTION fn_GetWatchlistByUserId(p_user_id UUID)
RETURNS TABLE (
    id UUID,
    user_id UUID,
    symbol VARCHAR(10),
    added_at TIMESTAMPTZ
)
LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    SELECT w.id, w.user_id, w.symbol, w.added_at
    FROM watchlists w
    WHERE w.user_id = p_user_id
    ORDER BY w.added_at DESC;
END;
$$;

-- GET: Check if symbol is in user watchlist
CREATE OR REPLACE FUNCTION fn_ExistsWatchlistItem(p_user_id UUID, p_symbol VARCHAR(10))
RETURNS BOOLEAN
LANGUAGE plpgsql
AS $$
BEGIN
    RETURN EXISTS (
        SELECT 1
        FROM watchlists w
        WHERE w.user_id = p_user_id
          AND w.symbol = p_symbol
    );
END;
$$;

-- POST: Add item to watchlist idempotently and return row
CREATE OR REPLACE FUNCTION fn_AddOrGetWatchlistItem(
    p_id UUID,
    p_user_id UUID,
    p_symbol VARCHAR(10),
    p_added_at TIMESTAMPTZ
)
RETURNS TABLE (
    id UUID,
    user_id UUID,
    symbol VARCHAR(10),
    added_at TIMESTAMPTZ
)
LANGUAGE plpgsql
AS $$
#variable_conflict use_column
BEGIN
    INSERT INTO watchlists (id, user_id, symbol, added_at)
    VALUES (p_id, p_user_id, p_symbol, p_added_at)
    ON CONFLICT (user_id, symbol) DO NOTHING;

    RETURN QUERY
    SELECT w.id, w.user_id, w.symbol, w.added_at
    FROM watchlists w
    WHERE w.user_id = p_user_id
      AND w.symbol = p_symbol
    LIMIT 1;
END;
$$;

-- DELETE: Remove item from watchlist
CREATE OR REPLACE PROCEDURE sp_DeleteWatchlistItem(
    p_user_id UUID,
    p_symbol VARCHAR(10)
)
LANGUAGE plpgsql
AS $$
BEGIN
    DELETE FROM watchlists w
    WHERE w.user_id = p_user_id
      AND w.symbol = p_symbol;
END;
$$;
