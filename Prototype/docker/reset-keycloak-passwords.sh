#!/usr/bin/env bash
# =============================================================================
# reset-keycloak-passwords.sh
#
# Keycloak 24 does not honour the plaintext "value" field in realm-import
# credentials — it stores them but cannot verify them at login time.
# Run this once after every fresh "docker compose up -d" to set working
# passwords for all dev users via the Admin REST API.
#
# Usage (from repo root):
#   bash docker/reset-keycloak-passwords.sh
# =============================================================================

set -e

KC_URL="http://localhost:8080"
ADMIN_USER="admin"
ADMIN_PASS="admin"
PASSWORD="Password1!"

echo "Waiting for Keycloak to be ready..."
until curl -sf "$KC_URL/realms/master" > /dev/null 2>&1; do
  sleep 3
done
echo "Keycloak is up."

# ── Obtain admin token ───────────────────────────────────────────────────────
ADMIN_TOKEN=$(curl -s -X POST "$KC_URL/realms/master/protocol/openid-connect/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=password&client_id=admin-cli&username=$ADMIN_USER&password=$ADMIN_PASS" \
  | grep -o '"access_token":"[^"]*"' | cut -d'"' -f4)

if [ -z "$ADMIN_TOKEN" ]; then
  echo "ERROR: Could not obtain admin token. Is Keycloak running?" >&2
  exit 1
fi

# ── Helper ───────────────────────────────────────────────────────────────────
reset_password() {
  local REALM=$1
  local USERNAME=$2

  USER_ID=$(curl -s \
    -H "Authorization: Bearer $ADMIN_TOKEN" \
    "$KC_URL/admin/realms/$REALM/users?username=$USERNAME" \
    | grep -o '"id":"[^"]*"' | head -1 | cut -d'"' -f4)

  if [ -z "$USER_ID" ]; then
    echo "  SKIP  $REALM/$USERNAME (user not found)"
    return
  fi

  HTTP=$(curl -s -o /dev/null -w "%{http_code}" \
    -X PUT \
    -H "Authorization: Bearer $ADMIN_TOKEN" \
    -H "Content-Type: application/json" \
    "$KC_URL/admin/realms/$REALM/users/$USER_ID/reset-password" \
    -d "{\"type\":\"password\",\"value\":\"$PASSWORD\",\"temporary\":false}")

  if [ "$HTTP" = "204" ]; then
    echo "  OK    $REALM/$USERNAME"
  else
    echo "  FAIL  $REALM/$USERNAME (HTTP $HTTP)"
  fi
}

# ── natwest-dev realm ────────────────────────────────────────────────────────
echo ""
echo "natwest-dev:"
reset_password "natwest-dev" "consumer1"
reset_password "natwest-dev" "agent1"
reset_password "natwest-dev" "support1"
reset_password "natwest-dev" "admin1"

# ── o2-dev realm ─────────────────────────────────────────────────────────────
echo ""
echo "o2-dev:"
reset_password "o2-dev" "consumer-o2"
reset_password "o2-dev" "agent-o2"
reset_password "o2-dev" "support-o2"

echo ""
echo "Done. All passwords set to: $PASSWORD"
