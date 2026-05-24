# Resume Review Platform — Requirements

**Status:** Draft v0.1
**Last updated:** 2026-05-24
**Owner:** TBD

---

## 1. Overview

A local, end-to-end system for ingesting candidate resumes (Word `.docx`), enriching them with AI-generated insights via **Azure AI Foundry**, presenting them in an Angular review UI, and producing a one-click visual HTML report for hiring decisions.

The system is built around a Word `.dotx` template whose content controls drive the canonical data model — i.e. the database schema mirrors the content controls of `resumetemplate.dotx`. Sample resumes are generated from this template to seed the system.

### 1.1 High-level flow

```
resumetemplate.dotx
        │
        ▼
[Generate 15+ fake resumes (.docx)]
        │
        ▼
[.NET 10 Web API ingests resumes]
        │
        ├──► Extract content-control values → SQL Server (ResumeReviewDb)
        │
        └──► Background job: call Azure AI Foundry → populate AI columns
                    │
                    ▼
[Angular 20 review UI]  ← user reviews / regenerates AI fields / marks reviewed
                    │
                    ▼
[One-click HTML report with visualizations]
```

---

## 2. Goals & Non-Goals

### 2.1 Goals

- Demonstrate a production-shaped end-to-end pipeline (template → docx → API → DB → AI enrichment → review UI → report) in a local dev environment.
- Keep the data model **driven by the resume template** so changes to the template propagate cleanly to schema/UI.
- Provide a **very good user experience** in the review UI: fast, legible, visually distinct AI fields, low-friction regeneration.
- Make AI enrichment **auditable** (original AI output preserved, history of prompts/responses tracked).
- One-click visual report that helps a hiring manager compare candidates intuitively.

### 2.2 Non-Goals (Out of Scope for v1)

- Authentication / multi-tenant access control (single local user assumed).
- Cloud deployment / CI/CD pipelines.
- Production-grade observability (basic logging only).
- Real (non-synthetic) resume parsing from arbitrary PDF/Word formats outside the template.
- Mobile-responsive UI (desktop-first; mobile is best-effort).

---

## 3. Repository Structure (Monorepo)

```
/
├── README.md
├── requirements.md                 (this document)
├── .gitignore
├── docs/
│   └── architecture.md
├── template/
│   └── resumetemplate.dotx         (user-supplied)
├── samples-resumes/
│   ├── generator/                  (script/tool that produces fake resumes)
│   └── output/                     (generated .docx files, git-ignored)
├── api/
│   ├── ResumeReview.Api/           (.NET 10 Minimal API)
│   ├── ResumeReview.Domain/        (entities, value objects)
│   ├── ResumeReview.Infrastructure/(EF Core, Azure AI client, docx parsing)
│   ├── ResumeReview.Worker/        (background AI enrichment worker)
│   ├── ResumeReview.Tests/
│   ├── sample.env
│   └── ResumeReview.sln
└── web/
    ├── package.json
    ├── angular.json
    └── src/
        ├── app/
        │   ├── core/               (services, models, interceptors)
        │   ├── features/
        │   │   ├── candidates-list/
        │   │   ├── candidate-detail/
        │   │   └── report/
        │   └── shared/             (AI field component, dialogs)
        └── styles/                 (Tailwind + Material theme)
```

---

## 4. Functional Requirements

### 4.1 Resume Template & Sample Resume Generation

**FR-4.1.1** The system shall use a user-supplied `resumetemplate.dotx` file placed at `/template/resumetemplate.dotx` as the canonical structural definition for all resumes.

**FR-4.1.2** A sample generator (under `/samples-resumes/generator`) shall produce **at least 15** synthetic `.docx` resumes by filling the template's content controls with fake data.

**FR-4.1.3** The generated cohort shall satisfy the following diversity profile:

| Dimension | Distribution |
|---|---|
| **Seniority** | Balanced mix across Junior, Mid, Senior, Staff, Principal |
| **Specialization** | Predominantly **ML / AI** roles (e.g. ML Engineer, Applied Scientist, MLOps, Research Engineer, Data Scientist, AI Platform Engineer) |
| **Locale** | International diversity in names, locations, education institutions, and date formats |
| **Edge cases** | Must include: at least 2 career switchers (e.g. from physics/finance into ML), at least 2 candidates with employment gaps (>6 months), at least 2 contractors / freelancers |

**FR-4.1.4** Generated resumes shall be valid `.docx` files that round-trip through Word without errors and preserve all content-control IDs/tags.

**FR-4.1.5** The generator shall be deterministic when given a seed value (for reproducible test runs) and non-deterministic by default.

> **Open item (OI-1):** Exact content-control inventory will be confirmed once `resumetemplate.dotx` is uploaded. The assumed schema below (§4.3) is a placeholder.

---

### 4.2 Resume Ingestion API

**FR-4.2.1** The API shall expose an ingestion endpoint:

- `POST /api/resumes/upload` — multipart upload of one or more `.docx` files. Returns `202 Accepted` with one `ingestionId` per file.

**FR-4.2.2** The API shall also provide a CLI seed command:

- `dotnet run --project ResumeReview.Api -- seed --path ./samples-resumes/output` — ingests every `.docx` in the given directory.

**FR-4.2.3** Ingestion shall:
1. Parse the `.docx` and extract values keyed by content-control tag.
2. Validate that all required controls are present (reject with a structured error if not).
3. Persist a `Candidate` row plus all child rows (skills, experience, etc.) in a single transaction.
4. **Enqueue** an AI enrichment job for the candidate (do not block the request on AI calls).

**FR-4.2.4** Ingestion shall be **idempotent** by `(FullName + Email)` — re-ingesting the same candidate updates rather than duplicates, and an audit row is written.

**FR-4.2.5** All endpoints shall be documented via **Swagger / OpenAPI** at `/swagger`, enabled by default in `Development` environment.

---

### 4.3 Data Model

> **Note:** Tables and fields below are derived from the **assumed** content-control set and will be reconciled with the actual `.dotx` once uploaded. Names use PascalCase per .NET convention.

#### 4.3.1 Core entities

**Candidate**
| Column | Type | Notes |
|---|---|---|
| `Id` | `Guid` PK | |
| `FullName` | `nvarchar(200)` | from control `FullName` |
| `Email` | `nvarchar(320)` | indexed |
| `Phone` | `nvarchar(50)` | |
| `Location` | `nvarchar(200)` | |
| `LinkedInUrl` | `nvarchar(500)` | nullable |
| `GitHubUrl` | `nvarchar(500)` | nullable |
| `Summary` | `nvarchar(max)` | |
| `SourceFileName` | `nvarchar(500)` | original `.docx` filename |
| `ReviewStatus` | `enum` | `Pending` \| `Reviewed` \| `Rejected` (default `Pending`) |
| `CreatedAt` | `datetime2` | audit |
| `UpdatedAt` | `datetime2` | audit |
| `LastEditedBy` | `nvarchar(200)` | audit (defaults to `"system"` locally) |

**Skill** *(child of Candidate, repeating control)*
- `Id`, `CandidateId` FK, `Name`, `OrderIndex`

**WorkExperience** *(child of Candidate, repeating group control)*
- `Id`, `CandidateId` FK, `Company`, `Title`, `StartDate`, `EndDate` (nullable = current), `Description`, `OrderIndex`

**Education** *(child of Candidate, repeating group control)*
- `Id`, `CandidateId` FK, `Institution`, `Degree`, `Field`, `GraduationYear`, `OrderIndex`

**Certification** *(child of Candidate, repeating control)*
- `Id`, `CandidateId` FK, `Name`, `Issuer`, `Year`, `OrderIndex`

**Project** *(child of Candidate, repeating group control)*
- `Id`, `CandidateId` FK, `Name`, `Description`, `TechStack`, `OrderIndex`

#### 4.3.2 AI enrichment entities

**CandidateAiFields** *(1:1 with Candidate)*
| Column | Type | Notes |
|---|---|---|
| `CandidateId` | `Guid` PK/FK | |
| `AiSummary` | `nvarchar(max)` | 2–3 sentence elevator pitch |
| `AiSeniorityLevel` | `nvarchar(50)` | Junior\|Mid\|Senior\|Staff\|Principal |
| `AiSeniorityRationale` | `nvarchar(max)` | |
| `AiTopStrengths` | `nvarchar(max)` | JSON array of 3–5 strings |
| `AiSkillCategories` | `nvarchar(max)` | JSON: `{Languages,Frameworks,Cloud,Databases,SoftSkills}` |
| `AiYearsExperienceEstimate` | `decimal(4,1)` | |
| `AiSuggestedRoles` | `nvarchar(max)` | JSON array |
| `AiInterviewFocusAreas` | `nvarchar(max)` | JSON array |
| `LastEnrichedAt` | `datetime2` | |
| `EnrichmentStatus` | `enum` | `Pending` \| `InProgress` \| `Completed` \| `Failed` |

**CandidateAiFieldOverride** *(captures user edits — preserves original AI output)*
| Column | Notes |
|---|---|
| `Id`, `CandidateId` FK, `FieldName` (e.g. `"AiSummary"`) |
| `OriginalAiValue` | snapshot at first generation |
| `CurrentValue` | latest value (user-edited or latest regen) |
| `IsUserEdited` | bool |
| `UpdatedAt`, `UpdatedBy` |

**AiGenerationHistory** *(append-only audit log per field)*
| Column | Notes |
|---|---|
| `Id`, `CandidateId` FK, `FieldName` |
| `ModelName` | e.g. `gpt-5.4-mini` |
| `PromptText` | full prompt sent |
| `ExtraInstructions` | optional user-supplied steering text |
| `ResponseText` | raw model output |
| `LatencyMs`, `TokenUsage` (JSON) |
| `RequestedBy`, `RequestedAt` |
| `Status` | `Success` \| `Failure`, with `ErrorMessage` |

**FR-4.3.3** All entities shall include the audit columns `CreatedAt`, `UpdatedAt`, `LastEditedBy` (auto-populated via EF Core interceptors).

**FR-4.3.4** All foreign keys cascade on delete from `Candidate`.

#### 4.3.3 Database setup

- **Server:** `localhost\SQLEXPRESS`
- **Database name:** `ResumeReviewDb`
- **ORM:** EF Core 10, **code-first** with migrations
- **Initialization:** `dotnet ef database update` (documented in `/api/README.md`)
- **Seed:** optional `dotnet run -- seed` command

---

### 4.4 AI Enrichment (Azure AI Foundry)

**FR-4.4.1** AI enrichment shall be performed by a **background worker** (`ResumeReview.Worker`, implemented as a hosted `BackgroundService`) consuming jobs from an in-process queue (`Channel<T>`) populated at ingestion time. This keeps ingestion fast and async.

**FR-4.4.2** The worker shall call **Azure AI Foundry** using the configured model (default: `gpt-5.4-mini`).

**FR-4.4.3** For each candidate the worker shall generate the following fields (per §4.3.2): `AiSummary`, `AiSeniorityLevel` + rationale, `AiTopStrengths`, `AiSkillCategories`, `AiYearsExperienceEstimate`, `AiSuggestedRoles`, `AiInterviewFocusAreas`.

**FR-4.4.4** Each field generation shall:
- Be persisted to `CandidateAiFields` (current value).
- Append a row to `AiGenerationHistory` with prompt, response, model, latency, token usage.
- On first successful generation, also populate `CandidateAiFieldOverride.OriginalAiValue`.

**FR-4.4.5** Failures shall be retried with exponential backoff (3 attempts, max 30s), then marked `Failed` with the error captured in history. Failed candidates are surfaced in the UI with a "Retry enrichment" affordance.

**FR-4.4.6** A manual regeneration endpoint shall be available:

- `POST /api/candidates/{id}/ai-fields/{fieldName}/regenerate` — body: `{ "extraInstructions": "optional steering text" }`. Synchronous; returns the new value and history record id.
- `POST /api/candidates/{id}/ai-fields/regenerate-all` — bulk regenerate all AI fields for a candidate (used by "Regenerate all" UI action).

---

### 4.5 Angular Review UI

**FR-4.5.1** The UI shall be built with **Angular 20**, **Angular Material**, **Tailwind CSS** (for utilities), and **ngx-charts** for visualizations. State management via **Angular Signals** and signal-based services (no NgRx).

**FR-4.5.2 — Candidates List view**

The list view shall display all candidates with:
- A **view toggle** (table ↔ card grid), preference persisted in `localStorage`.
- **Table mode:** sortable columns for Name, Location, Seniority (AI), Years Exp (AI), Top Skill (AI), Review Status, Updated.
- **Card mode:** name, avatar placeholder, AI summary preview, seniority badge, top 3 skills as chips, review status indicator.
- **Filters:** by review status, by AI seniority level, by free-text search across name/skills/summary.
- **Bulk actions:** none in v1 (single-candidate flows only).

**FR-4.5.3 — Candidate Detail view**

The detail view shall show **all** fields stored in the database, grouped into sections:
1. Identity & contact
2. Summary
3. Skills
4. Work experience (chronological timeline component)
5. Education
6. Certifications
7. Projects
8. **AI Insights** (visually distinct — see §4.6)

Each section is collapsible. A sticky right-side panel shows: review status, "Mark as reviewed" button, "Regenerate all AI fields" button, and a link to the source `.docx`.

**FR-4.5.4** All numeric counts (e.g. years of experience, skill count) shall be computed reactively from signals so edits are reflected instantly without a page reload.

---

### 4.6 AI Field Correction & Regeneration

**FR-4.6.1** Every AI-derived field shall be rendered with a **distinct visual treatment**:
- Subtle purple/violet left border
- ✨ sparkle icon in the field header
- A small badge: `"AI-generated"` (default) or `"Modified by user"` (after edit) or `"Regenerated"` (after explicit regen).

**FR-4.6.2** Each AI field shall expose a **Regenerate** button (primary action) and a **manual edit** affordance (secondary action, preserved from the original requirement).

**FR-4.6.3** Clicking **Regenerate** opens a modal dialog containing:
- Model name (read-only): e.g. `gpt-5.4-mini`
- Original prompt (read-only, collapsible)
- "Extra instructions" textbox (optional steering, e.g. *"make it more concise"*, *"emphasize cloud experience"*)
- Buttons: **Cancel** | **Regenerate**
- After successful regen, the new value replaces the current value; the original AI value remains accessible via a "View original" link (sourced from `CandidateAiFieldOverride.OriginalAiValue`).

**FR-4.6.4** Clicking **Edit** on an AI field allows inline text editing. On save, `IsUserEdited = true` and the badge updates to `"Modified by user"`. A "Revert to AI value" link is then shown.

**FR-4.6.5** A **"Regenerate all AI fields"** action shall be available at the candidate level, with a confirmation dialog and a progress indicator.

**FR-4.6.6** All regenerations append to `AiGenerationHistory`; the detail page shall expose a "View history" drawer per field showing chronological prompt/response pairs.

---

### 4.7 Review Workflow

**FR-4.7.1** Each candidate has a `ReviewStatus`: `Pending` (default after ingest) → `Reviewed` or `Rejected`.

**FR-4.7.2** A **"Mark as reviewed"** button on the candidate detail page sets the status to `Reviewed`. A "Reject" option sets it to `Rejected`. Status changes write to the audit log.

**FR-4.7.3** Report generation operates on the **subset of candidates with `ReviewStatus = Reviewed`**. The report button is enabled whenever at least one candidate is `Reviewed`. The list view shall display a count of reviewed candidates and a "Generate report" CTA.

---

### 4.8 One-Click Report Generation

**FR-4.8.1** A single click on **"Generate report"** shall produce an **interactive HTML report** (rendered in-app as a route, e.g. `/report`) covering all currently-`Reviewed` candidates. No download/export step is required in v1.

**FR-4.8.2** The report shall contain **both** a combined cohort dashboard and per-candidate detail panels (accessible via in-page navigation).

**FR-4.8.3** The combined dashboard shall include the following visualizations:

| # | Visualization | Source data |
|---|---|---|
| 1 | **Skills heatmap** (candidates × skills, intensity = inferred strength) | `Skill` + `AiSkillCategories` |
| 2 | **Experience timeline** per candidate (Gantt-style) | `WorkExperience` |
| 3 | **Seniority distribution** chart (bar/pie) | `AiSeniorityLevel` |
| 4 | **Tech-stack frequency / tag cloud** | `Skill`, `Project.TechStack` |
| 5 | **Side-by-side comparison cards** (configurable candidate picker) | All Candidate + AI fields |
| 6 | **Education / certification breakdown** (stacked bar by institution type / issuer) | `Education`, `Certification` |
| 7 | **Top-N leaderboard** by computed score (weighted AI seniority + years exp + skill match) | computed |

**FR-4.8.4** All charts shall be rendered with **ngx-charts** (consistent with the rest of the app) and shall be responsive within the report layout.

**FR-4.8.5** The leaderboard scoring formula shall be transparent — clicking a candidate row reveals the component weights and values.

**FR-4.8.6** The report shall reflect the latest data on every navigation (no cached snapshot in v1).

---

## 5. Non-Functional Requirements

| ID | Requirement |
|---|---|
| **NFR-5.1** | Ingestion of a single `.docx` (excluding AI enrichment) shall complete in **< 500 ms** on a developer laptop. |
| **NFR-5.2** | AI enrichment for one candidate (all 7 fields) shall typically complete in **< 30 seconds** (subject to Azure latency). |
| **NFR-5.3** | The list view shall render 15–100 candidates without perceptible lag (< 200 ms first paint after data load). |
| **NFR-5.4** | All API responses shall return structured JSON errors with `traceId` for correlation. |
| **NFR-5.5** | Secrets (Azure AI endpoint, key) shall **never** be committed; only `sample.env` is checked in. |
| **NFR-5.6** | Code shall be formatted via `dotnet format` (.NET) and Prettier + ESLint (Angular), enforced by pre-commit hook (optional). |
| **NFR-5.7** | Logging shall use `Microsoft.Extensions.Logging` with structured logs; AI calls log model, latency, token usage (never the candidate's PII verbatim — log the candidate id only). |
| **NFR-5.8** | The UI shall meet WCAG 2.1 AA contrast on AI field distinguishing styles (purple accents must remain accessible). |

---

## 6. Technology Stack (Summary)

| Layer | Choice |
|---|---|
| Backend framework | **.NET 10** Web API, **Minimal APIs** with endpoint groups *(default — confirm)* |
| ORM | EF Core 10, code-first migrations |
| Database | SQL Server Express (`localhost\SQLEXPRESS`), database `ResumeReviewDb` |
| Background processing | `BackgroundService` + in-process `Channel<T>` queue |
| AI provider | Azure AI Foundry, default model `gpt-5.4-mini` |
| .docx parsing | `DocumentFormat.OpenXml` |
| API docs | Swagger / OpenAPI (Swashbuckle or built-in `Microsoft.AspNetCore.OpenApi`) |
| Auth | **None** for v1 *(local single-user — confirm)* |
| Frontend framework | **Angular 20** |
| UI kit | Angular Material + Tailwind CSS utilities |
| Charts | ngx-charts |
| State | Angular Signals + signal-based services |
| Tests | xUnit (.NET), Jest + Playwright (Angular) — basic coverage in v1 |

---

## 7. Configuration & Secrets

**FR-7.1** All environment-specific configuration shall be loaded from a `.env` file in `/api/`. A `sample.env` shall be committed with placeholders:

```env
# === Database ===
ConnectionStrings__ResumeReviewDb=Server=localhost\SQLEXPRESS;Database=ResumeReviewDb;Trusted_Connection=True;TrustServerCertificate=True;

# === Azure AI Foundry ===
AzureAi__Endpoint=https://<your-foundry-resource>.services.ai.azure.com
AzureAi__ApiKey=<your-api-key>
AzureAi__DeploymentName=gpt-5.4-mini
AzureAi__ApiVersion=2024-12-01-preview

# === Worker ===
Worker__MaxConcurrentEnrichments=4
Worker__RetryMaxAttempts=3
Worker__RetryBaseDelaySeconds=2

# === Logging ===
Logging__LogLevel__Default=Information
Logging__LogLevel__ResumeReview=Debug
```

**FR-7.2** The API shall fail-fast at startup if any required env var is missing, with a clear error message naming the missing key(s).

**FR-7.3** `.env` shall be listed in `.gitignore`. `sample.env` shall **not** contain real credentials.

---

## 8. Acceptance Criteria

The system is considered complete for v1 when **all** of the following are demonstrably true on a clean dev machine:

1. ✅ Running the sample generator produces ≥ 15 valid `.docx` resumes in `/samples-resumes/output` matching the diversity profile in §4.1.3.
2. ✅ Running `dotnet ef database update` creates `ResumeReviewDb` on `localhost\SQLEXPRESS` with all tables in §4.3.
3. ✅ Running the seed command ingests all sample resumes; every candidate appears in the DB with their child rows populated.
4. ✅ Within ~5 minutes of ingestion, every candidate has all 7 AI fields populated; `AiGenerationHistory` contains a row per generation; `CandidateAiFieldOverride.OriginalAiValue` is set.
5. ✅ The Angular app loads, shows all candidates in both table and card modes, with filters and view toggle working.
6. ✅ Opening any candidate displays every DB field; AI fields are visually distinct (purple border + ✨ + badge).
7. ✅ Regenerating an AI field via the dialog updates the value, creates a new history row, and badges the field as "Regenerated".
8. ✅ Manually editing an AI field marks it "Modified by user" and exposes "Revert to AI value".
9. ✅ Marking a candidate as Reviewed updates the status and the list view reflects it.
10. ✅ Clicking "Generate report" produces an HTML report containing **all 7 visualizations** in §4.8.3, populated from currently-`Reviewed` candidates.
11. ✅ `sample.env` exists and the API fails fast with a helpful error when `.env` is missing.

---

## 9. Out of Scope (v1)

- Authentication, authorization, multi-user collaboration.
- PDF/PPTX export of the report (HTML only).
- Mobile-first responsive layout.
- Real-time push updates (UI re-fetches on user action).
- Parsing of arbitrary resumes (must be template-derived).
- Internationalization of the UI (English only).
- Production deployment artifacts (Docker, k8s, IaC).

---

## 10. Open Items (need confirmation)

| ID | Item | Resolution needed |
|---|---|---|
| **OI-1** | `resumetemplate.dotx` not yet supplied; §4.3 schema is assumed. | Upload template; reconcile schema. |
| **OI-2** | Azure AI Foundry deployment name `gpt-5.4-mini` — confirm this is the exact deployment name available in your Foundry resource. | Confirm or supply correct value. |
| **OI-3** | Minimal APIs vs Controllers — defaulting to Minimal APIs. | Confirm or override. |
| **OI-4** | No auth for local dev — confirm. | Confirm or specify auth scheme. |
| **OI-5** | Ingestion via both CLI seed + upload endpoint — confirm both are wanted, or pick one. | Confirm. |
| **OI-6** | Leaderboard scoring formula weights — to be decided during implementation; suggest exposing config in `.env`. | Confirm approach. |
| **OI-7** | "Regenerate all AI fields" bulk action — added per §4.6.5; confirm desired. | Confirm. |

---

## 11. Change Log

| Version | Date | Author | Notes |
|---|---|---|---|
| 0.1 | 2026-05-24 | Draft | Initial draft from clarifying-questions session. |
