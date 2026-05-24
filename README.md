# Resume Review Platform

End-to-end local platform that ingests Word `.docx` resumes, enriches them with AI insights via Azure AI Foundry (with a deterministic local stub for offline dev), surfaces them in an Angular 20 review UI, and produces a one-click HTML report with 7 visualizations.

Implementation of [`requirements.md`](./requirements.md). Design choices are recorded in [`architecture-decision-records.md`](./architecture-decision-records.md).

## Stack

- **Backend:** .NET 10 Minimal API + EF Core 10 (SQL Server Express; SQLite for tests), background `BackgroundService` worker.
- **Frontend:** Angular 20, Angular Material, Tailwind CSS, ngx-charts, signal-based services.
- **Tests:** xUnit + WebApplicationFactory (.NET); Jest + Playwright (Angular).
- **AI:** Azure AI Foundry chat client by default; deterministic `StubAiProvider` when `AzureAi__UseStub=true` or no key is configured (the local default).

## Repo layout

```
api/                          .NET solution (Domain, Infrastructure, Api, Worker, Tests)
samples-resumes/
  generator/                  Console tool that emits 18 diverse synthetic .docx files
  output/                     Generated resumes (git-ignored)
template/                     resumetemplate.dotx (canonical content-control source)
web/                          Angular 20 application
  e2e/                        Playwright UI tests
  src/                        App source (features/candidates-list, candidate-detail, report, shared)
```

## First-time setup

```pwsh
# 0. Tools required: .NET 10 SDK, Node 20+, SQL Server (Express works)

# 1. Generate sample resumes (18 diverse .docx files)
cd samples-resumes/generator
dotnet run -- --template ../../template/resumetemplate.dotx --out ../output --count 18 --seed 42

# 2. Configure env (copy and edit if you want real Azure AI Foundry)
cd ../../api
copy sample.env .env
# .env defaults to AzureAi__UseStub=true so no key is needed

# 3. Build + create the DB schema (EnsureCreated runs at startup)
dotnet build ResumeReview.slnx

# 4. Seed the database with all sample resumes
#    Note: `dotnet run --project` sets the process CWD to the project directory,
#    so the --path must be relative to ResumeReview.Api/ (or use an absolute path).
dotnet run --project ResumeReview.Api -- seed --path ../../samples-resumes/output

# 5. Run the API (Swagger/OpenAPI spec at /openapi/v1.json in dev)
dotnet run --project ResumeReview.Api

# In a separate shell — install + run the UI
cd ../web
npm install --legacy-peer-deps
npm start          # serves on http://localhost:4200
```

The Angular app calls the API at `http://localhost:5201` (override in `web/src/environments/environment.ts`). The API port is set by `api/ResumeReview.Api/Properties/launchSettings.json` — keep the two in sync if you change it.

## Reset

`reset.ps1` (repo root) drops `ResumeReviewDb`, wipes generated resumes, regenerates the cohort against the template, and re-seeds. Stop the API first so it releases its DB connections.

```pwsh
pwsh ./reset.ps1                                       # defaults: count=18, seed=42
pwsh ./reset.ps1 -SqlInstance "localhost\SQLEXPRESS" -Count 25 -Seed 7
```

## AI provider

`api/.env` controls whether enrichment hits real Azure AI Foundry or the local deterministic stub:
- `AzureAi__UseStub=true` (or missing endpoint/key) → `StubAiProvider`. Offline, fast, byte-deterministic — so regenerating a field returns the same text it had before.
- `AzureAi__UseStub=false` with real `Endpoint` + `ApiKey` + `DeploymentName` → `AzureFoundryAiProvider`.

Provider selection happens once at startup in DI ([`Infrastructure/DependencyInjection.cs`](api/ResumeReview.Infrastructure/DependencyInjection.cs)), so restart the API after toggling `UseStub`. The model name recorded in `AiGenerationHistory` is the easy way to confirm which provider serviced a regeneration.

## Tests

```pwsh
# .NET unit + integration tests (xUnit, in-memory SQLite + WebApplicationFactory)
cd api
dotnet test ResumeReview.Tests/ResumeReview.Tests.csproj

# Angular unit tests (Jest)
cd ../web
npm test

# Angular UI tests (Playwright — boots the dev server + stubs the API)
npm run e2e
```

## Acceptance criteria

| # | Criterion | Status |
|---|---|---|
| 1 | Sample generator produces ≥ 15 diverse `.docx` resumes | ✅ 18 by default (`--count` is configurable) |
| 2 | EF Core code-first schema with all entities in §4.3 | ✅ See [`api/ResumeReview.Domain/Entities.cs`](api/ResumeReview.Domain/Entities.cs) |
| 3 | Seed command ingests every sample resume | ✅ `dotnet run --project ResumeReview.Api -- seed --path ../../samples-resumes/output` |
| 4 | AI enrichment populates 7 fields, with history + override snapshot | ✅ `EnrichmentService` + `StubAiProvider` (or Azure Foundry when configured) |
| 5 | Angular list view, table + card toggle, filters | ✅ Persisted in `localStorage` |
| 6 | Detail view shows every DB field with AI fields visually distinct | ✅ Purple border, sparkle icon, badge per state |
| 7 | Regenerate → modal → new value + history row + badge becomes Regenerated | ✅ |
| 8 | Edit → badge becomes Modified by user → Revert restores original AI value | ✅ |
| 9 | Mark as reviewed updates list view + nav counter | ✅ |
| 10 | One-click report contains all 7 visualizations from §4.8.3 | ✅ `/report` route |
| 11 | Fails fast when required config missing; `sample.env` checked in | ✅ `EnvValidation.RequireKeys` |
