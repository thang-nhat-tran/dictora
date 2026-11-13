# Dictora

A dictation website: paste a YouTube URL and practice English listening sentence by sentence.

New to the project? Start with [docs/workflow.md](docs/workflow.md).

## Getting started

### Prerequisites
- [.NET SDK 10](https://dotnet.microsoft.com/download) (the exact version is pinned in `backend/global.json`)
- [Docker](https://docs.docker.com/get-docker/) with Docker Compose
- Node.js 22+ (for the frontend)

### 1. Clone
```bash
git clone git@github.com:thang-nhat-tran/dictora.git
cd dictora
```

### 2. Start the database
```bash
docker compose -f infra/docker-compose.yml up -d --wait
```
This runs PostgreSQL 18 on `localhost:5432` (database, user and password are all `dictora`, for local use only). If port 5432 is taken, run `DICTORA_DB_PORT=5433 docker compose -f infra/docker-compose.yml up -d --wait` and change the port in `backend/src/Dictora.Api/appsettings.Development.json`.

### 3. Set local secrets
Secrets are stored in .NET user secrets, outside the repo, and only load in Development. From `backend/`:
```bash
cd backend
dotnet user-secrets set "YouTubeDataApi:ApiKey" "<key>" --project src/Dictora.Api   # ask the team for a key
dotnet user-secrets list --project src/Dictora.Api                                  # check what's set
```
You only need the key for importing videos. The API starts without it. The full list of config names is in [docs/system-architecture.md §5](docs/system-architecture.md#5-configuration).

### 4. Run the backend
```bash
cd backend
dotnet tool restore                      # installs dotnet-ef (first time only)
dotnet run --project src/Dictora.Api
```
- On startup in Development, the API **applies all pending database migrations** automatically.
- Check http://localhost:5281/health. It should return `Healthy`.
- The OpenAPI document is at http://localhost:5281/openapi/v1.json.

> On the very first run you'll see a few `fail:` log lines about `__EFMigrationsHistory` not existing. That's EF Core checking for the table before creating it, and it's expected.

### 5. Run the frontend
_Coming in #4._

### Run the tests
```bash
cd backend
dotnet test
```
The database tests start their own throwaway PostgreSQL container, so **Docker must be running**.

### Stop / reset the database
```bash
docker compose -f infra/docker-compose.yml down      # stop, keep data
docker compose -f infra/docker-compose.yml down -v   # stop and delete all local data
```
