-- =============================================================================
-- PostgreSQL initialisation — runs automatically on first container start
-- Creates both tenant schemas with tables and seed data
-- =============================================================================

-- ---------------------------------------------------------------------------
-- NatWest tenant schema
-- ---------------------------------------------------------------------------
CREATE SCHEMA IF NOT EXISTS tenant_natwest;
SET search_path = tenant_natwest, public;

CREATE TABLE IF NOT EXISTS complaints (
    id                UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id         VARCHAR(100) NOT NULL,
    subject           VARCHAR(120) NOT NULL,
    description       VARCHAR(2000) NOT NULL,
    channel           VARCHAR(20)  NOT NULL,
    status            VARCHAR(20)  NOT NULL DEFAULT 'Open',
    logged_by_user_id VARCHAR(200) NOT NULL,
    assigned_to_id    VARCHAR(200),
    logged_at         TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    assigned_at       TIMESTAMPTZ,
    resolved_at       TIMESTAMPTZ,
    resolution_notes  VARCHAR(4000)
);

CREATE TABLE IF NOT EXISTS support_persons (
    id           UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id    VARCHAR(100) NOT NULL,
    display_name VARCHAR(200) NOT NULL,
    email        VARCHAR(320) NOT NULL,
    is_active    BOOLEAN      NOT NULL DEFAULT TRUE
);

CREATE TABLE IF NOT EXISTS outbox_events (
    id           UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id    VARCHAR(100) NOT NULL,
    event_type   VARCHAR(100) NOT NULL,
    payload      JSONB        NOT NULL,
    created_at   TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    processed_at TIMESTAMPTZ
);

CREATE TABLE IF NOT EXISTS audit_events (
    id            UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id     VARCHAR(100) NOT NULL,
    actor_user_id VARCHAR(200) NOT NULL,
    action        VARCHAR(100) NOT NULL,
    resource_type VARCHAR(100) NOT NULL,
    resource_id   VARCHAR(100) NOT NULL,
    details       JSONB,
    occurred_at   TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

INSERT INTO support_persons (id, tenant_id, display_name, email, is_active) VALUES
    (gen_random_uuid(), 'natwest', 'Sarah Lee',   'sarah.lee@natwest-example.com',   TRUE),
    (gen_random_uuid(), 'natwest', 'Marcus Webb',  'marcus.webb@natwest-example.com',  TRUE),
    (gen_random_uuid(), 'natwest', 'Priya Nair',   'priya.nair@natwest-example.com',   TRUE);

-- ---------------------------------------------------------------------------
-- O2 tenant schema
-- ---------------------------------------------------------------------------
CREATE SCHEMA IF NOT EXISTS tenant_o2;
SET search_path = tenant_o2, public;

CREATE TABLE IF NOT EXISTS complaints (
    id                UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id         VARCHAR(100) NOT NULL,
    subject           VARCHAR(120) NOT NULL,
    description       VARCHAR(2000) NOT NULL,
    channel           VARCHAR(20)  NOT NULL,
    status            VARCHAR(20)  NOT NULL DEFAULT 'Open',
    logged_by_user_id VARCHAR(200) NOT NULL,
    assigned_to_id    VARCHAR(200),
    logged_at         TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    assigned_at       TIMESTAMPTZ,
    resolved_at       TIMESTAMPTZ,
    resolution_notes  VARCHAR(4000)
);

CREATE TABLE IF NOT EXISTS support_persons (
    id           UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id    VARCHAR(100) NOT NULL,
    display_name VARCHAR(200) NOT NULL,
    email        VARCHAR(320) NOT NULL,
    is_active    BOOLEAN      NOT NULL DEFAULT TRUE
);

CREATE TABLE IF NOT EXISTS outbox_events (
    id           UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id    VARCHAR(100) NOT NULL,
    event_type   VARCHAR(100) NOT NULL,
    payload      JSONB        NOT NULL,
    created_at   TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    processed_at TIMESTAMPTZ
);

CREATE TABLE IF NOT EXISTS audit_events (
    id            UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id     VARCHAR(100) NOT NULL,
    actor_user_id VARCHAR(200) NOT NULL,
    action        VARCHAR(100) NOT NULL,
    resource_type VARCHAR(100) NOT NULL,
    resource_id   VARCHAR(100) NOT NULL,
    details       JSONB,
    occurred_at   TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

INSERT INTO support_persons (id, tenant_id, display_name, email, is_active) VALUES
    (gen_random_uuid(), 'o2', 'James Okafor',  'james.okafor@o2-example.com',  TRUE),
    (gen_random_uuid(), 'o2', 'Aisha Malik',   'aisha.malik@o2-example.com',   TRUE),
    (gen_random_uuid(), 'o2', 'Tom Briggs',    'tom.briggs@o2-example.com',    TRUE);

-- Reset search_path to default
RESET search_path;
