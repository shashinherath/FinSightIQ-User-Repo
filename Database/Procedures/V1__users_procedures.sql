-- ==============================================================================
-- Functions and Stored Procedures: Users
-- Database: usersdb
-- Service: FinSightIQ User Service
-- Rule: Functions for GET/POST, Procedures for PUT/DELETE
-- ==============================================================================

-- GET: Retrieve user by ID
CREATE OR REPLACE FUNCTION fn_GetUserById(p_id UUID)
RETURNS TABLE (
    id UUID,
    email VARCHAR(255),
    password_hash VARCHAR(60),
    role VARCHAR(20),
    is_suspended BOOLEAN,
    created_at TIMESTAMPTZ,
    updated_at TIMESTAMPTZ,
    deleted_at TIMESTAMPTZ
)
LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    SELECT u.id, u.email, u.password_hash, u.role, u.is_suspended, u.created_at, u.updated_at, u.deleted_at
    FROM users u
    WHERE u.id = p_id
    LIMIT 1;
END;
$$;

-- GET: Retrieve active user by email (case-insensitive)
CREATE OR REPLACE FUNCTION fn_GetUserByEmail(p_email VARCHAR(255))
RETURNS TABLE (
    id UUID,
    email VARCHAR(255),
    password_hash VARCHAR(60),
    role VARCHAR(20),
    is_suspended BOOLEAN,
    created_at TIMESTAMPTZ,
    updated_at TIMESTAMPTZ,
    deleted_at TIMESTAMPTZ
)
LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    SELECT u.id, u.email, u.password_hash, u.role, u.is_suspended, u.created_at, u.updated_at, u.deleted_at
    FROM users u
    WHERE lower(u.email) = lower(p_email)
      AND u.deleted_at IS NULL
    LIMIT 1;
END;
$$;

-- GET: Check if email exists
CREATE OR REPLACE FUNCTION fn_ExistsByEmail(p_email VARCHAR(255))
RETURNS BOOLEAN
LANGUAGE plpgsql
AS $$
BEGIN
    RETURN EXISTS (
        SELECT 1
        FROM users u
        WHERE lower(u.email) = lower(p_email)
          AND u.deleted_at IS NULL
    );
END;
$$;

-- POST: Create new user
CREATE OR REPLACE FUNCTION fn_CreateUser(
    p_id UUID,
    p_email VARCHAR(255),
    p_password_hash VARCHAR(60),
    p_role VARCHAR(20),
    p_is_suspended BOOLEAN,
    p_created_at TIMESTAMPTZ,
    p_updated_at TIMESTAMPTZ,
    p_deleted_at TIMESTAMPTZ
)
RETURNS TABLE (
    id UUID,
    email VARCHAR(255),
    password_hash VARCHAR(60),
    role VARCHAR(20),
    is_suspended BOOLEAN,
    created_at TIMESTAMPTZ,
    updated_at TIMESTAMPTZ,
    deleted_at TIMESTAMPTZ
)
LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    INSERT INTO users (
        id, email, password_hash, role, is_suspended, created_at, updated_at, deleted_at
    ) VALUES (
        p_id, p_email, p_password_hash, p_role, p_is_suspended, p_created_at, p_updated_at, p_deleted_at
    )
    RETURNING users.id, users.email, users.password_hash, users.role, users.is_suspended, users.created_at, users.updated_at, users.deleted_at;
END;
$$;

-- PUT: Update user attributes
CREATE OR REPLACE PROCEDURE sp_UpdateUser(
    p_id UUID,
    p_email VARCHAR(255),
    p_password_hash VARCHAR(60),
    p_role VARCHAR(20),
    p_is_suspended BOOLEAN,
    p_updated_at TIMESTAMPTZ,
    p_deleted_at TIMESTAMPTZ
)
LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE users
    SET email = p_email,
        password_hash = p_password_hash,
        role = p_role,
        is_suspended = p_is_suspended,
        updated_at = p_updated_at,
        deleted_at = p_deleted_at
    WHERE id = p_id;
END;
$$;

-- GET: Retrieve paged users
CREATE OR REPLACE FUNCTION fn_GetPagedUsers(
    p_limit INT,
    p_offset INT
)
RETURNS TABLE (
    id UUID,
    email VARCHAR(255),
    password_hash VARCHAR(60),
    role VARCHAR(20),
    is_suspended BOOLEAN,
    created_at TIMESTAMPTZ,
    updated_at TIMESTAMPTZ,
    deleted_at TIMESTAMPTZ
)
LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    SELECT u.id, u.email, u.password_hash, u.role, u.is_suspended, u.created_at, u.updated_at, u.deleted_at
    FROM users u
    WHERE u.deleted_at IS NULL
    ORDER BY u.created_at DESC
    LIMIT p_limit OFFSET p_offset;
END;
$$;

-- GET: Retrieve total active users count
CREATE OR REPLACE FUNCTION fn_GetUsersCount()
RETURNS INT
LANGUAGE plpgsql
AS $$
DECLARE
    v_count INT;
BEGIN
    SELECT COUNT(*)::INT INTO v_count
    FROM users u
    WHERE u.deleted_at IS NULL;
    RETURN v_count;
END;
$$;
