# Newman API Contract Tests

Six tests covering the CMS REST API (FR1 + FR2 + RBAC rejection).

## Prerequisites

```bash
npm install -g newman
```

The app must be running: `cd CMS.Web && dotnet run`

---

## Getting Keycloak tokens

You need two Bearer tokens — one for `consumer1` (NatWest) and one for `agent1` (NatWest).

Open a terminal and run these `curl` commands (Docker must be running):

```bash
# Consumer token
curl -s -X POST http://localhost:8080/realms/natwest-dev/protocol/openid-connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=password&client_id=cms-web&client_secret=change-me-in-production&username=consumer1&password=Password1!" \
  | python3 -m json.tool

# Agent token
curl -s -X POST http://localhost:8080/realms/natwest-dev/protocol/openid-connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=password&client_id=cms-web&client_secret=change-me-in-production&username=agent1&password=Password1!" \
  | python3 -m json.tool
```

Copy the `access_token` value from each response.

---

## Getting a SupportPersonId

Connect to the running PostgreSQL container and query the table:

```bash
docker exec -it $(docker ps -q --filter name=postgres) psql -U cms_app -d cms_app \
  -c "SET search_path = tenant_natwest; SELECT id, display_name FROM support_persons LIMIT 3;"
```

Copy any UUID from the `id` column.

---

## Running the tests

```powershell
.\newman\run-tests.ps1 `
  -ConsumerToken "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9..." `
  -AgentToken    "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9..." `
  -SupportPersonId "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
```

Expected output: **6 passing, 0 failing**.

---

## Test summary

| # | Test | Expected |
|---|------|----------|
| 01 | FR1 — Consumer logs complaint | `201 Created` + `complaintId` UUID |
| 02 | FR2 — Agent assigns support person | `204 No Content` |
| 03 | No auth header | `401 Unauthorized` |
| 04 | Agent tries to log complaint (wrong role) | `403 Forbidden` |
| 05 | Subject > 120 chars | `400 Bad Request` |
| 06 | Consumer tries to assign support (wrong role) | `403 Forbidden` |
