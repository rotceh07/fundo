# Fundo Full-Stack Engineer Take-Home

[Watch the demo video](https://youtu.be/4dbdDqwRyKQ)

A small loan application flow built with .NET, Next.js, SQLite and a transactional outbox.

An applicant fills in a form and the backend rule engine approves or denies the application. Approved applications are stored transactionally, and submitting again with the same SSN updates the same customer and application instead of creating new ones. Each approval also stages an event in a transactional outbox, and a background worker later sends it to a mock external HTTP service as a create or an update.

For design decisions and trade-offs, see [ARCHITECTURE.md](./ARCHITECTURE.md).

## Tech stack

| Area | Technology |
| --- | --- |
| Backend | .NET 10 / ASP.NET Core |
| ORM | EF Core 10 |
| Database | SQLite |
| Frontend | Next.js 16.3.6 / React / TypeScript |
| External mock | .NET 10 Minimal API |
| Backend tests | xUnit + WebApplicationFactory + real SQLite |

## Repository structure

```text
fundo/
├── backend/            .NET solution: Domain, Application, Infrastructure, Api and their tests
├── frontend/           Next.js application form and result pages
├── external-service/   Standalone mock of the external HTTP service
├── README.md
└── ARCHITECTURE.md
```

## Prerequisites

- .NET SDK 10
- Node.js 22 LTS or 24 LTS
- npm

## Install

```bash
git clone https://github.com/rotceh07/fundo.git
cd fundo
dotnet restore backend/Fundo.sln
cd frontend
npm ci
cd ..
```

## Run

Start each process in its own terminal, from the repository root.

Terminal 1, external mock:

```bash
dotnet run --project external-service/Fundo.ExternalApi/Fundo.ExternalApi.csproj --urls http://localhost:5180
```

Terminal 2, backend API and background worker:

```bash
dotnet run --project backend/src/Fundo.Api/Fundo.Api.csproj --urls http://localhost:5176
```

Terminal 3, frontend:

```bash
cd frontend
npm run dev
```

| Service | URL |
| --- | --- |
| Frontend | http://localhost:3000 |
| Backend API | http://localhost:5176 |
| External mock | http://localhost:5180 |
| OpenAPI document (Development) | http://localhost:5176/openapi/v1.json |

No manual database setup is required. The API applies the EF Core migrations at startup and creates a local SQLite file named `fundo.db`. `dotnet run --project` starts the API with the project folder as its working directory, so with the command above the file is created at `backend/src/Fundo.Api/fundo.db`. It is ignored by git; stop the API and delete it to start from an empty database.

An approved application does not wait for the external service. If the mock is not running, the local transaction still succeeds and the outbox worker keeps retrying in the background until the mock is available.

## Configuration

Backend settings live in `backend/src/Fundo.Api/appsettings.json`:

| Key | Default | Purpose |
| --- | --- | --- |
| `ConnectionStrings:Fundo` | `Data Source=fundo.db` | SQLite database file |
| `ExternalService:BaseUrl` | `http://localhost:5180` | External mock used by the worker |
| `Frontend:Origin` | `http://localhost:3000` | Only origin allowed by CORS |
| `Blacklist:Ssns` | `999999999`, `111111111` | Blacklisted SSNs |

The API does not start if the connection string, the external service URL or the frontend origin is missing, or if a configured blacklist SSN is invalid. If `Blacklist:Ssns` is absent, the API starts with an empty blacklist. If the frontend runs on a different port, update `Frontend:Origin`.

The frontend calls `http://localhost:5176` by default. To point it at another API, set `NEXT_PUBLIC_API_BASE_URL` before starting it. No `.env` file is needed for the default setup.

## Test data

All data below is fictitious. Use otherwise valid values for any field not listed.

**Approved, new customer.** Use this SSN first:

| First name | Last name | Address | State | Company | Amount | SSN |
| --- | --- | --- | --- | --- | --- | --- |
| Jane | Doe | 100 Main St | FL | Acme | 10000 | 123-45-6789 |

**Returning customer.** Submit again with the same SSN, written without dashes, and new data:

| First name | Last name | Address | State | Company | Amount | SSN |
| --- | --- | --- | --- | --- | --- | --- |
| Jane | Smith | 200 Oak Ave | TX | Globex | 25000.75 | 123456789 |

Both submissions show the same application reference. The database keeps one customer and one loan application with the latest data, and the external mock is updated instead of receiving a second customer.

**Denied by state.** State `NY` with SSN `222-22-2222` shows *Applications from NY are not eligible.*

**Denied by blacklist.** State `FL` with SSN `999-99-9999` shows *SSN is not eligible.* The blacklisted SSNs are `999999999` and `111111111`, in any accepted format.

To see what the external service received:

```bash
curl http://localhost:5180/api/customers
```

This diagnostic endpoint returns names, state, company and amount, but never the SSN or the address.

The API can also be called directly:

```bash
curl -X POST http://localhost:5176/api/applications \
  -H "Content-Type: application/json" \
  -d '{
    "firstName": "Jane",
    "lastName": "Doe",
    "address": "100 Main St",
    "state": "FL",
    "companyName": "Acme",
    "requestedAmount": 10000,
    "ssn": "123-45-6789"
  }'
```

## Tests

Backend unit and integration tests:

```bash
dotnet test backend/Fundo.sln
```

Frontend lint and production build:

```bash
cd frontend
npm run lint
npm run build
cd ..
```

External mock build:

```bash
dotnet build external-service/Fundo.ExternalApi/Fundo.ExternalApi.csproj
```

The automated tests focus on the behavior requested by the challenge: rule evaluation, returning customers, real relational transactions, the transactional outbox, background delivery and the HTTP endpoint. The frontend has no automated test suite; it is checked with lint, the production build and manual end to end runs.
