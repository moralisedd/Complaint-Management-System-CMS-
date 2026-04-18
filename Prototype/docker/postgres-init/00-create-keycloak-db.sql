-- Create a dedicated database for Keycloak (KC_DB=postgres in docker-compose.yml).
-- This runs before 01-init-tenants.sql on first container start.
-- cms_app is the POSTGRES_USER (superuser), so no explicit GRANT needed.
CREATE DATABASE keycloak_db;
