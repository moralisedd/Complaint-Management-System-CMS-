-- =============================================================================
-- CMS Initial Schema — runs once per tenant during onboarding (ADR-02A)
-- Execute as: psql -U cms_app -d cms_db -v schema=tenant_natwest -f InitialSchema.sql
-- Replace :schema with the target tenant schema name.
-- =============================================================================

-- Create the tenant schema
CREATE SCHEMA IF NOT EXISTS :schema;

-- Set the search path for this script
SET search_path = :schema, public;

-- ---------------------------------------------------------------------------
-- COMPLAINT table
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS complaints (
    id              UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       VARCHAR(100)    NOT NULL,
    subject         VARCHAR(120)    NOT NULL,
    description     VARCHAR(2000)   NOT NULL,
    channel         VARCHAR(20)     NOT NULL CHECK (channel IN ('Web','Mobile','Phone','Email')),
    status          VARCHAR(20)     NOT NULL DEFAULT 'Open'
                                    CHECK (status IN ('Open','InProgress','Resolved','Closed')),
    logged_by_user_id VARCHAR(200)  NOT NULL,
    assigned_to_id  VARCHAR(200),
    logged_at       TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    assigned_at     TIMESTAMPTZ,
    resolved_at     TIMESTAMPTZ,
    resolution_notes VARCHAR(4000)
);

CREATE INDEX IF NOT EXISTS idx_complaints_tenant_status
    ON complaints (tenant_id, status);

CREATE INDEX IF NOT EXISTS idx_complaints_logged_at
    ON complaints (logged_at DESC);

-- ---------------------------------------------------------------------------
-- SUPPORT_PERSONS table
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS support_persons (
    id              UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       VARCHAR(100)    NOT NULL,
    display_name    VARCHAR(200)    NOT NULL,
    email           VARCHAR(320)    NOT NULL,
    is_active       BOOLEAN         NOT NULL DEFAULT TRUE
);

CREATE INDEX IF NOT EXISTS idx_support_persons_tenant_active
    ON support_persons (tenant_id, is_active);

-- ---------------------------------------------------------------------------
-- OUTBOX_EVENTS table (transactional outbox pattern)
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS outbox_events (
    id              UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       VARCHAR(100)    NOT NULL,
    event_type      VARCHAR(100)    NOT NULL,
    payload         JSONB           NOT NULL,
    created_at      TIMESTAMPTZ     NOT NULL DEFAULT NOW(),
    processed_at    TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS idx_outbox_unprocessed
    ON outbox_events (created_at)
    WHERE processed_at IS NULL;

-- ---------------------------------------------------------------------------
-- AUDIT_EVENTS table — insert-only (RNF10)
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS audit_events (
    id              UUID            PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       VARCHAR(100)    NOT NULL,
    actor_user_id   VARCHAR(200)    NOT NULL,
    action          VARCHAR(100)    NOT NULL,
    resource_type   VARCHAR(100)    NOT NULL,
    resource_id     VARCHAR(100)    NOT NULL,
    details         JSONB,
    occurred_at     TIMESTAMPTZ     NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_audit_tenant_occurred
    ON audit_events (tenant_id, occurred_at DESC);

-- ---------------------------------------------------------------------------
-- Seed data: support persons for demo tenants
-- (Replace tenant_id values as appropriate)
-- ---------------------------------------------------------------------------
INSERT INTO support_persons (id, tenant_id, display_name, email, is_active)
VALUES
    (gen_random_uuid(), current_schema(), 'Sarah Lee',       'sarah.lee@example.com',    TRUE),
    (gen_random_uuid(), current_schema(), 'Marcus Webb',     'marcus.webb@example.com',  TRUE),
    (gen_random_uuid(), current_schema(), 'Priya Nair',      'priya.nair@example.com',   TRUE)
ON CONFLICT DO NOTHING;
