# Complaint Management System — PoC

A multi-tenant complaint management prototype built with ASP.NET Core 8, PostgreSQL, and Keycloak.

---

## Prerequisites

Make sure these are installed before starting:

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)

---

## 1. Start the infrastructure

From the `Prototype` folder, start PostgreSQL, Redis, and Keycloak:

```bash
docker compose up -d
```

Wait about 60–90 seconds for Keycloak to finish starting. You can check it's ready by visiting:

```
http://localhost:8080
```

> **First run only:** Docker automatically runs the SQL init scripts to create both tenant schemas (`tenant_natwest`, `tenant_o2`) with their tables and seed support staff.

---

## 2. Run the application

```bash
cd CMS.Web
dotnet run
```

The app starts at **https://localhost:5001**

> Your browser may show a certificate warning on the first run. Click through to accept the localhost dev certificate. If the warning persists, run `dotnet dev-certs https --trust` and restart.

---

## 3. Sign in

Navigate to **https://localhost:5001** and you'll be taken to the login page.

Choose your organisation and sign in with one of the test accounts below.

### NatWest users

| Username    | Password    | Role           | Can do                          |
|-------------|-------------|----------------|---------------------------------|
| `consumer1` | `Password1!` | Consumer       | Log complaints                  |
| `agent1`    | `Password1!` | HelpDeskAgent  | View all complaints, assign support |
| `support1`  | `Password1!` | SupportPerson  | Record resolutions              |
| `admin1`    | `Password1!` | TenantAdmin    | View KPI dashboard              |

### O2 users

| Username      | Password    | Role          | Can do                          |
|---------------|-------------|---------------|---------------------------------|
| `consumer-o2` | `Password1!` | Consumer      | Log complaints                  |
| `agent-o2`    | `Password1!` | HelpDeskAgent | View all complaints, assign support |
| `support-o2`  | `Password1!` | SupportPerson | Record resolutions              |

---

## 4. Using the application

**Log a complaint (Consumer)**
1. Sign in as `consumer1` or `consumer-o2`
2. Click **Log Complaint** in the nav
3. Fill in the subject, description, and channel, then submit
4. You'll get a reference number on the confirmation page

**Assign a support person (HelpDeskAgent)**
1. Sign in as `agent1` or `agent-o2`
2. Click **All Complaints** to see the list
3. Click a complaint and select a support person from the dropdown

---

## 5. Run the tests

Open a second terminal in the `Prototype` folder. Make sure Docker is running (the integration tests spin up their own PostgreSQL container).

```bash
# Unit tests only
dotnet test CMS.Domain.Tests

# Integration tests (requires Docker)
dotnet test CMS.Infrastructure.Tests
```

### Generate a coverage report

```powershell
# Collect coverage from both test projects
dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage

# Generate HTML report (open a new terminal after installing if the command isn't found)
dotnet tool install -g dotnet-reportgenerator-globaltool

reportgenerator "-reports:coverage/**/*.xml" "-targetdir:coverage/report" "-reporttypes:Html"

# Open the report
Start-Process coverage/report/index.html
```

---

## 6. Explore the API (Swagger)

The REST API is documented at:

```
https://localhost:5001/swagger
```

To test a protected endpoint:
1. Go to **http://localhost:8080** → sign into the Keycloak admin console (`admin` / `admin`)
2. Select the `natwest-dev` or `o2-dev` realm → **Clients** → `cms-web` → **Client scopes** → get a token via the token endpoint
3. Paste the token into the Swagger **Authorize** button

---

## Resetting the database

If you want a clean slate:

```bash
docker compose down -v   # removes containers AND the postgres data volume
docker compose up -d     # recreates everything from scratch
```

---

## Project structure

```
Prototype/
├── CMS.Domain/               # Entities, interfaces, strategy pattern
├── CMS.Application/          # Use case services (ComplaintService, SupportAssignmentService)
├── CMS.Infrastructure/       # EF Core, repositories, middleware, Keycloak interceptor
├── CMS.Web/                  # Razor Pages UI + REST controllers
├── CMS.Domain.Tests/         # Unit tests
├── CMS.Infrastructure.Tests/ # Integration tests (Testcontainers)
└── docker/
    ├── keycloak/             # Realm configs (natwest-dev, o2-dev)
    └── postgres-init/        # SQL scripts that run on first container start
```
