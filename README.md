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
