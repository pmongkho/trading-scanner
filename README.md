# TradingScanner

Reusable ASP.NET Core + PostgreSQL + Angular stack.

## Stack

- ASP.NET Core .NET 10
- Entity Framework Core
- PostgreSQL / Npgsql
- ASP.NET Core Identity
- JWT authentication
- Swagger
- Angular 21
- Tailwind CSS 4
- Render-compatible Dockerfile
- Vercel SPA configuration

## Structure

```text
TradingScanner/
├── angular-client/
│   └── src/
│       ├── app/
│       │   ├── core/
│       │   └── pages/
│       └── environments/
├── dotnet-server/
│   ├── _Controllers/
│   ├── _Data/
│   ├── _Models/
│   │   ├── DTOs/
│   │   ├── Entities/
│   │   └── Enums/
│   ├── _Services/
│   ├── Dockerfile
│   └── Program.cs
└── vercel.json
```

## Scanner foundation

The first scanner phase defines a provider-neutral market-data contract, mutable per-symbol
state, typed scoring/setup/catalyst values, and a typed SignalR client contract. Runtime
thresholds live under the `Scanner` configuration section and are validated during startup;
score weights must total 100. PostgreSQL stores ticker metadata, news, signals, alerts,
configuration, and measured signal outcomes in the `scanner` schema.

`MarketSessionService` classifies timestamps in `America/New_York`, including daylight-saving
transitions: premarket is 04:00–09:30, opening range is 09:30–09:45, regular is 09:45–16:00,
and after-hours is 16:00–20:00 on weekdays. Exchange holidays are intentionally deferred to a
market-calendar provider phase.

## Market stream

The server runs one provider-owned stream and projects normalized trades, quotes, and minute
bars into a thread-safe per-symbol state manager. `Scanner:MarketStream:Provider` selects
`Synthetic` (the deterministic local default) or `Alpaca`. Configure the symbol list under
`Scanner:MarketStream:Symbols`. Alpaca owns a single stock websocket for all configured symbols
and reads credentials only from `ALPACA_API_KEY` and `ALPACA_API_SECRET`; the configured URL
selects the Alpaca data feed. The market stream is data-only and provides no order routing.

## Live dashboard and catalysts

The dashboard hydrates its scanner snapshot and recent news from `GET /api/scanner/dashboard`,
then follows versioned snapshots over SignalR at `/hubs/market`. It retains hydrated data
during reconnects, identifies stale streams as degraded, and reconnects with bounded
exponential backoff.

Alpaca news polling uses `ALPACA_API_KEY` and `ALPACA_API_SECRET`. Ordered,
case-insensitive rules assign a stable catalyst type and quality score; provider IDs make
ingestion idempotent. Configure polling under `Scanner:News` or set
`Scanner__News__Enabled=false` to disable it.

## First-time install of the template

Run from the `dotnet-pgsql-angular-stack` folder:

```powershell
dotnet new install .
```

Verify:

```powershell
dotnet new list pstack
```

## Create a new project

```powershell
dotnet new pstack -n MyNewApp
cd MyNewApp
```

`TradingScanner` is automatically replaced with `MyNewApp`.

## Local setup

### Backend

Set a local PostgreSQL connection string:

```powershell
cd dotnet-server
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=mynewapp;Username=postgres;Password=YOUR_PASSWORD"
```

Generate a JWT signing key:

```powershell
$bytes = New-Object byte[] 64
[System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
$key = [Convert]::ToBase64String($bytes)
dotnet user-secrets set "Jwt:Key" $key
```

Create the initial database migration:

```powershell
dotnet tool install --global dotnet-ef
dotnet ef migrations add InitialCreate
dotnet ef database update
dotnet run
```

### Frontend

In another terminal:

```powershell
cd angular-client
npm install
npm start
```

Frontend: `http://localhost:4200`

Backend Swagger in Development: use the URL shown by `dotnet run`, then `/swagger`.

## Render

Create a PostgreSQL database and a Web Service whose root directory is `dotnet-server`.

Dockerfile:

```text
dotnet-server/Dockerfile
```

Set these environment variables:

```text
ConnectionStrings__DefaultConnection=<Render PostgreSQL connection string>
Jwt__Key=<new random secret>
Jwt__Issuer=TradingScanner
Jwt__Audience=TradingScanner-client
Cors__AllowedOrigins__0=https://YOUR-VERCEL-DOMAIN.vercel.app
```

The application automatically applies EF migrations during startup.

## Vercel

Import the repository. The root `vercel.json` already builds `angular-client`.

Before production deployment, change:

```text
angular-client/src/environments/environment.production.ts
```

to the Render API URL.

## What belongs in the skeleton

Keep:
- authentication
- User entity
- AppDbContext
- PostgreSQL configuration
- JWT
- CORS
- Swagger
- Angular HTTP configuration
- auth service/interceptor/guard
- Tailwind
- Dockerfile
- Vercel config

Add per project:
- domain entities
- project controllers
- project services
- project DTOs
- third-party API integrations
- project-specific keys
