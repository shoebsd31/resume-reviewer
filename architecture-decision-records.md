# Architecture Decision Records

A log of the load-bearing design choices in this repo and the reasoning behind them. Each record states the context, the decision, and the trade-offs accepted.

---

## ADR-001 — Layered solution: Domain · Infrastructure · Api · Worker · Tests

**Context.** The system has three distinct concerns: a pure data model (resumes, AI fields, overrides), I/O-heavy plumbing (EF Core, Azure SDK, .docx parsing), and the hosting surface (HTTP + background processing).

**Decision.** Split the .NET solution into four projects, plus a test project:
- `ResumeReview.Domain` — POCO entities and enums, no dependencies.
- `ResumeReview.Infrastructure` — EF Core context, Azure AI provider, document parsing, enrichment service. Depends on `Domain`.
- `ResumeReview.Api` — Minimal API endpoints, DTO mapping, env validation, CLI seed mode. Depends on `Infrastructure`.
- `ResumeReview.Worker` — `BackgroundService` host for async enrichment. Hosted by `ResumeReview.Api`.
- `ResumeReview.Tests` — xUnit suite covering all the above.

**Consequences.**
- `Domain` is reference-able from anywhere without dragging in EF or Azure SDKs.
- Worker logic is reusable: today it runs inside the API process; lifting it into a standalone host requires no code changes, only a different `Program.cs`.
- Cost: one extra project boundary to maintain. We mitigated this by keeping `Worker` thin — it only contains the `EnrichmentWorker` class.

---

## ADR-002 — EF Core code-first with `EnsureCreated`, no migrations

**Context.** Schema is owned by C# entity classes ([`api/ResumeReview.Domain/Entities.cs`](api/ResumeReview.Domain/Entities.cs)). The platform is positioned as a local demo / starter — there is no production deployment story yet, and the schema is still in flux.

**Decision.** Use `db.Database.EnsureCreated()` at startup ([`api/ResumeReview.Api/Program.cs`](api/ResumeReview.Api/Program.cs)) instead of EF migrations. Schema is created exactly once per fresh DB; existing DBs are untouched. There is no `Migrations/` folder.

**Consequences.**
- Pro: no migration churn while we iterate. Zero ceremony for first-run setup.
- Pro: pairs cleanly with the `reset.ps1` workflow — drop DB → start API → schema recreated.
- Con: no incremental upgrade path. Schema changes against an existing DB require either a manual `ALTER` or a full reset.
- When this project takes on real users, replace with `dotnet ef migrations add` + `db.Database.Migrate()`.

---

## ADR-003 — SQL Server in dev, SQLite for tests, chosen by connection-string shape

**Context.** SQL Server Express is the natural Windows dev DB and what the README assumes. Tests need to be fast, hermetic, and provider-portable.

**Decision.** A single `DependencyInjection` registration sniffs the connection string and picks the provider at runtime ([`ResumeReview.Infrastructure/DependencyInjection.cs`](api/ResumeReview.Infrastructure/DependencyInjection.cs)):

```csharp
var isSqlite = connectionString.Contains("DataSource=", …)
            || connectionString.Contains("Data Source=:memory:", …)
            || connectionString.StartsWith("Filename=", …);
if (isSqlite) options.UseSqlite(connectionString);
else          options.UseSqlServer(connectionString);
```

**Consequences.**
- Pro: tests run against an in-memory SQLite with the same `DbContext` configuration as production.
- Pro: no separate `appsettings.Test.json` / `IDbContextFactory` plumbing.
- Con: the two providers handle some types differently (e.g. `decimal(4,1)` on AI-years estimate). We pinned the SQL types explicitly in the model to keep both providers honest. Tests caught the divergence early.

---

## ADR-004 — AI fields normalized into a separate table + history + override snapshot

**Context.** Each candidate has eight AI-generated fields (summary, seniority, rationale, strengths, etc.). The UI must distinguish AI-generated from user-edited values, let the user revert, and show full regeneration history with prompts and token usage.

**Decision.** Three-table pattern ([`Entities.cs`](api/ResumeReview.Domain/Entities.cs)):
- `CandidateAiFields` — current displayed value per AI field, plus an `EnrichmentStatus` and `LastError`. One row per candidate.
- `CandidateAiFieldOverride` — per-field record holding `OriginalAiValue`, `CurrentValue`, and `IsUserEdited`. Enables "Revert to AI value".
- `AiGenerationHistory` — append-only log of every generation attempt: prompt text, extra instructions, response, model name, latency, token usage, status.

**Consequences.**
- Pro: edits, regenerations, and reverts are all expressible without mutating the original AI output.
- Pro: history table doubles as an audit log for compliance / debugging.
- Pro: keeping AI fields off `Candidate` itself prevents the row from getting overwritten by future ingestion.
- Con: three writes per regeneration. Acceptable given the operation is user-initiated and not high-throughput.

---

## ADR-005 — `IAiProvider` abstraction with Foundry + deterministic Stub implementations

**Context.** The platform needs to work offline (CI, demo, first-run) without a live Azure key, while still calling real Azure AI Foundry when configured.

**Decision.** Define `IAiProvider` ([`Infrastructure/AiEnrichment/IAiProvider.cs`](api/ResumeReview.Infrastructure/AiEnrichment/IAiProvider.cs)) with two implementations:
- `AzureFoundryAiProvider` — uses `Azure.AI.OpenAI` chat completions against the configured deployment.
- `StubAiProvider` — deterministic, fast, no network. Computes plausible field values from the candidate's own data.

DI picks the implementation at startup based on `AzureAi__UseStub` and whether `Endpoint`/`ApiKey` are populated.

**Consequences.**
- Pro: tests run offline. README's "first-time setup" is a no-key experience.
- Pro: swapping providers (Bedrock, OpenAI direct, on-prem) is a one-class change.
- Con: the stub is deterministic by design, so regenerating most fields produces byte-identical output and the UI badge "Regenerated" flips without the text visibly changing. Documented and accepted because real Foundry is the intended runtime once configured.

---

## ADR-006 — In-process `Channel<T>` queue + `BackgroundService`, not a broker

**Context.** Enrichment is asynchronous and concurrent (up to N candidates in parallel), with retry/backoff. There is no requirement today for cross-process work distribution or durability across restarts.

**Decision.** Use `System.Threading.Channels.Channel<EnrichmentJob>` ([`ChannelEnrichmentQueue`](api/ResumeReview.Infrastructure/AiEnrichment/EnrichmentQueue.cs)) as an unbounded in-memory queue, drained by `EnrichmentWorker : BackgroundService` ([`api/ResumeReview.Worker/EnrichmentWorker.cs`](api/ResumeReview.Worker/EnrichmentWorker.cs)). Concurrency is bounded by a `SemaphoreSlim` sized from `Worker__MaxConcurrentEnrichments`. Worker is hosted inside the API process via `AddHostedService`.

**Consequences.**
- Pro: zero infrastructure (no RabbitMQ, no Service Bus, no Hangfire). Works on a laptop with no setup.
- Pro: the `IEnrichmentQueue` abstraction means swapping in a broker later is one class.
- Con: queue is non-durable — jobs in flight at API shutdown are lost. Acceptable because seed-time enrichment is idempotent (re-running ingestion enqueues again) and user-triggered regenerations are best-effort with visible status in `EnrichmentStatus`.
- Con: worker and API share a process / connection pool. We accepted the coupling for simplicity.

---

## ADR-007 — `.docx` parsing: extract content controls by tag, with plain-text fallback

**Context.** The template (`template/resumetemplate.dotx`) uses Word's structured document tags (SDTs) — `name`, `title`, `contact`, `skills`, `experience`, `Education`, `awards`. But the ingestion pipeline must also accept arbitrary external resumes that have no content controls at all.

**Decision.** Two-pass parser ([`Infrastructure/Parsing/ResumeDocumentParser.cs`](api/ResumeReview.Infrastructure/Parsing/ResumeDocumentParser.cs)):
1. Walk `body.Descendants<SdtElement>()`, key the inner text by tag value. Use case-insensitive lookup so the template's `Education` (capitalized) and an external resume's `education` both work.
2. If no SDTs are found, fall back to flat-text parsing: regex out email/phone/location/URLs, treat the first non-empty line as the candidate's name.

**Consequences.**
- Pro: template-aware ingestion is precise (no guessing where the skills section starts).
- Pro: foreign resumes still ingest, just with less structure.
- Con: free-text parsing of experience/education is fragile and relies on conventions (e.g. `Title @ Company`, `YYYY-MM - YYYY-MM` date ranges). Real-world resumes will need an LLM extraction pass — out of scope for this iteration.

---

## ADR-008 — Sample generator clones the template, doesn't build a document from scratch

**Context.** The original generator wrote a fresh `.docx` from nothing — same SDT tags, but no styles, fonts, or layout from the template. The generated files looked nothing like `resumetemplate.dotx`.

**Decision.** Generator now copies the `.dotx` to the output path, opens the copy in edit mode, calls `ChangeDocumentType(WordprocessingDocumentType.Document)` to flip the content-type, then walks each `SdtBlock` by tag and replaces its content while cloning the original paragraph's `pPr` / `rPr` so styles propagate ([`samples-resumes/generator/ResumeGenerator.cs`](samples-resumes/generator/ResumeGenerator.cs)). Hard-fails if the template isn't supplied.

**Consequences.**
- Pro: every sample resume opens in Word with the template's exact theme, fonts, page setup, and heading styles.
- Pro: the SDT tags survive intact, so the parser in ADR-007 still does its job.
- Con: outputs depend on a specific `.dotx`. A different template with different tag names would require either renaming there or extending the tag map in `BuildFieldValues`.

---

## ADR-009 — `.env` via DotNetEnv, not `appsettings.Development.json` or user-secrets

**Context.** The Azure Foundry endpoint and API key are secrets that must not be checked in, but should be easy to set per developer without IDE configuration steps.

**Decision.** `Program.cs` loads `.env` via `DotNetEnv.Env.Load()` before `WebApplication.CreateBuilder`. `api/.env` is gitignored; `api/sample.env` (placeholders only) is committed as the canonical reference. `EnvValidation.RequireKeys` fails fast at startup if required keys are missing.

**Consequences.**
- Pro: 12-factor-style configuration; the same `AzureAi__ApiKey` key works whether sourced from `.env`, OS env, or container env.
- Pro: cross-platform — no dependency on `dotnet user-secrets` (which only works inside the user profile).
- Pro: `sample.env` self-documents the config surface.
- Con: secrets live on disk in plaintext. Production would substitute Key Vault / managed identity behind the same key names.

---

## ADR-010 — Angular: standalone components + signal-based services (no NgRx)

**Context.** The Angular UI has limited cross-cutting state — a candidates list, current detail, derived counts. Reactivity to user filters and AI regenerations is the main UX requirement.

**Decision.** All components are standalone (no NgModules). `CandidatesService` ([`web/src/app/core/candidates.service.ts`](web/src/app/core/candidates.service.ts)) holds state in `signal<…>()` and exposes derived `computed(…)` values (e.g. `reviewedCount`). Components read state via `inject(CandidatesService)` and call methods to mutate. No store, no observables-as-state, no effects.

**Consequences.**
- Pro: minimal boilerplate; the entire state layer is one service.
- Pro: signals integrate cleanly with the template via `@if` / `@for` control flow.
- Pro: components are trivially unit-testable because there's no module configuration.
- Con: doesn't scale to deeply nested cross-cutting state. When that's needed (e.g. multi-user collaboration), revisit with NgRx Signal Store.

---

## ADR-011 — Angular Material + Tailwind utilities, side by side

**Context.** Material gives us the high-level controls we want (table, form fields, dialogs, snackbar, chips, sidenav). Tailwind gives us cheap layout and spacing utilities without writing component-scoped SCSS.

**Decision.** Both are loaded globally in `styles.scss`. Material theme is configured via M3 (`mat.theme(...)`). Tailwind handles spacing, flex/grid, and the AI badge color system. Component templates freely mix Material elements with Tailwind classes (e.g. `<mat-toolbar class="!sticky top-0 z-10 shadow-md">`).

**Consequences.**
- Pro: no need to hand-roll layout shells around Material components.
- Pro: AI-specific styles (`ai-field-border`, `ai-glow`, AI-purple palette) are pure utility — no theme overrides.
- Con: occasional `!` Tailwind important prefix needed to win over Material's own styles. Tolerable.
- Con: Material requires the Material Icons + Roboto fonts to render correctly. These are loaded from Google Fonts in `index.html`. Without them, icons render as literal text words — a real failure mode the project hit during development.

---

## ADR-012 — Testing: xUnit + WebApplicationFactory; Jest + Playwright

**Context.** Three layers need independent verification: domain/service logic, HTTP contract, and UI behavior.

**Decision.**
- .NET unit & integration tests: xUnit. Integration tests use `WebApplicationFactory<Program>` against the real Minimal API with a per-test SQLite database ([`ResumeReview.Tests/TestHelpers/TestDbFactory.cs`](api/ResumeReview.Tests/TestHelpers/TestDbFactory.cs)).
- Angular unit tests: Jest with Angular's Testing Library bindings.
- Angular E2E: Playwright, which boots the dev server and stubs the API at the HTTP boundary.

**Consequences.**
- Pro: integration tests exercise real EF Core + real routing without mocks; bugs in serialization or DI surface in CI.
- Pro: Playwright stubbing the API keeps E2E hermetic and fast; the real backend is only required for the .NET integration tests.
- Pro: `StubAiProvider` (ADR-005) means all suites run without network.
- Con: three test runners to maintain. Acceptable for the three distinct surfaces.

---

## Revisit triggers

These ADRs should be re-examined when any of these change:

| Trigger | Revisit |
|---|---|
| Schema needs to evolve against a live DB | ADR-002 (migrations) |
| Multi-instance API deployment | ADR-006 (broker), ADR-009 (Key Vault) |
| Resumes from sources we don't control become common | ADR-007 (LLM extraction) |
| State sharing across many components | ADR-010 (signal store) |
| First production tenant | ADR-002, ADR-009 (secrets) |
