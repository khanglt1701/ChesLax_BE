# Cheslax API

ASP.NET Core 10 Web API for Cheslax: authentication, matchmaking, SignalR play, Elo ratings, and match history.

PostgreSQL is the primary database. SQLite is used only by automated tests.

## Requirements

- .NET SDK 10 (`global.json`)
- PostgreSQL 16 (local install or Docker)

## Local development

```bash
# optional: local Postgres matching appsettings.Development.json
docker compose -f docker-compose.dev.yml up -d

dotnet restore Cheslax.slnx
dotnet build Cheslax.slnx
dotnet test Cheslax.slnx
dotnet run --project src/Cheslax.Api/Cheslax.Api.csproj
```

API: `http://127.0.0.1:5080`  
Swagger: `/swagger`  
Health: `/health` and `/api/health`  
SignalR: `/hubs/game`

Development applies EF Core migrations at startup (`Database:ApplyMigrations` is true in `appsettings.Development.json`).

## Configuration

Connection string name is **`ConnectionStrings:Default`** (env: `ConnectionStrings__Default`). Do not use `DefaultConnection`.

| Variable | Purpose |
| --- | --- |
| `Database__Provider` | `Npgsql` (default) or `Sqlite` (tests) |
| `Database__ApplyMigrations` | `true` only for a controlled migrate step or local dev |
| `ConnectionStrings__Default` | PostgreSQL connection string |
| `Cors__Origins` | Comma-separated allowed origins |
| `ASPNETCORE_ENVIRONMENT` | `Development` / `Production` |
| `ASPNETCORE_URLS` | Bind address (container default `http://+:8080`) |

Never put production passwords in source. Copy `.env.example` to `.env`.

Frontend build-time `VITE_*` variables are **not** used here and must not contain database secrets.

## Migrations

SQL Server migrations are archived in `archive/sqlserver-migrations/` and are not applied.

PostgreSQL baseline: `src/Cheslax.Infrastructure/Persistence/Migrations/20261023105814_InitialPostgres.cs`.

```bash
dotnet tool restore
dotnet tool run dotnet-ef database update --project src/Cheslax.Infrastructure --startup-project src/Cheslax.Api
```

Production: do **not** let every API replica migrate. Run once per release:

```bash
docker compose run --rm api -- --migrate
docker compose up -d
```

`--migrate` applies pending migrations, seeds the system AI user, and exits.

## Docker (API + PostgreSQL)

```bash
cp .env.example .env
# edit POSTGRES_PASSWORD, ConnectionStrings__Default, Cors__Origins

docker compose build
docker compose run --rm api -- --migrate
docker compose up -d
curl http://127.0.0.1:5080/health
```

PostgreSQL is on the internal Compose network only. Port 5432 is not published.

The API is published on `127.0.0.1:5080` for a host Nginx reverse proxy. Do not expose 5080 on the public interface; terminate HTTPS at Nginx.

### Backup

The Docker volume is not a backup.

```bash
docker compose exec postgres pg_dump -U cheslax -d cheslax -Fc > cheslax-$(date +%F).dump
docker compose exec -T postgres pg_restore -U cheslax -d cheslax --clean --if-exists < cheslax-YYYY-MM-DD.dump
```

### Rollback

Keep the previous image tag. After a bad API release:

```bash
docker compose down
docker image tag cheslax-api:previous cheslax-api:latest
docker compose up -d
```

Database rollbacks are restore-from-dump, not `MigrateAsync` down.

If you still have a SQL Server `Cheslax` database from early development, export it before switching providers. Changing the connection string does not move data.

## Resource notes (2 vCPU / 4 GB)

Compose limits: Postgres ~512 MB, API ~768 MB. No Redis. Single API instance (SignalR has no backplane).
