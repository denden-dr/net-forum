# NetForum — Agent Instructions

.NET 10 Blazor Server forum with PostgreSQL, EF Core, xUnit, and MinIO/S3 storage.

## Architecture

- **Single Blazor Server project** — no WASM/Client split. All components in `NetForum/Components/`. Render mode: `@rendermode InteractiveServer`.
- **No REST controllers for internal data** — Blazor components inject `IForumService` directly (server-side).
- **Thread-safe DB**: `IDbContextFactory<AppDbContext>` injected everywhere (never `DbContext` directly). Required for Blazor Server concurrent circuits.
- **User context**: `ICurrentUserService` is the only way to resolve the current user. Services must never use `IHttpContextAccessor` or `AuthenticationStateProvider`.
- **Dev fallback**: `DevCurrentUserService` auto-authenticates as seeded dev profile (`00000000-0000-0000-0000-000000000001`). On /login, /register, /api/auth paths it respects real auth state.
- **Notifications**: Mentions parsing dispatched fire-and-forget via `Task.Run` to avoid blocking SignalR circuits.
- **MinIO/S3** required at startup — all five `Storage:*` config keys must be present or app fails fast.

## Commands

All from repo root:

| Task | Make | Direct |
|------|------|--------|
| Build | `make build` | `dotnet build` |
| Run | `make run` | `dotnet run --project NetForum/NetForum.csproj` |
| All tests | `make test` | `dotnet test` |
| Unit tests | `make test-unit` | `dotnet test --filter "FullyQualifiedName~NetForum.Tests.Unit"` |
| Integration tests | `make test-integration` | `dotnet test --filter "FullyQualifiedName~NetForum.Tests.Integration"` |
| Start dependencies | `make compose-up` | `docker compose up -d` / `podman compose up -d` |
| Stop dependencies | `make compose-down` | `docker compose down` / `podman compose down` |
| Add migration | `make migration-add name=Name` | `dotnet ef migrations add Name -p NetForum/NetForum.csproj --output-dir Data/Migrations` |
| Apply migration | `make migration-update` | `dotnet ef database update -p NetForum/NetForum.csproj` |
| Remove migration | `make migration-remove` | `dotnet ef migrations remove -p NetForum/NetForum.csproj` |

- `dotnet-ef` is a **local tool** (see `dotnet-tools.json`), not a global install.
- Dev app at `http://localhost:5201`.
- Dev DB: `Host=localhost;Database=netforum_db;Username=postgres;Password=postgres`.

## Testing

- **xUnit** + **Moq** (unit) + **bUnit** (component) + **Testcontainers.PostgreSql** (integration).
- Unit tests are pure RAM, no DB. Integration tests boot a real `postgres:18-alpine` container.
- **Collection Fixture**: `PostgreSqlTestFixture` starts the container once, applies schema, seeds categories. All integration tests use `[Collection("PostgreSqlCollection")]`.
- **Truncation before each integration test**: classes implement `IAsyncLifetime` with `TRUNCATE TABLE "Posts", "Threads", "Users" RESTART IDENTITY CASCADE;` in `InitializeAsync()`. Categories are preserved.
- **Podman rootless**: the fixture auto-detects `/run/user/1000/podman/podman.sock` and sets `RYUK_CONTAINER_PRIVILEGED=true`.
- **Seeding**: Categories are seeded programmatically via `DbInitializer.SeedCategoriesAsync` — **not** via EF Core `HasData` migrations. Dev users (DevUser, TestMember, TestModerator, TestAdmin) seeded at dev startup.

## Conventions

- **TDD**: Write failing test first in `NetForum.Tests.Unit/` (or `.Integration/`), confirm red with `make test`, then implement.
- **Write verification required**: `CreateThreadAsync`, `CreatePostAsync`, `UpvoteThreadAsync`, `UpvotePostAsync` all throw `UnauthorizedAccessException` if email unconfirmed.
- **No dark mode** — light palette only (`--bg-primary: #f8fafc`, `--accent: #4f46e5`, `--text-primary: #0f172a`).
- **No placeholders** — never `// TODO`, stubs, or `NotImplementedException`.
- **No direct DB access in Razor components** — use `IForumService` only.

## .agents integration

- `.agents/rules/antigravity-rtk-rules.md`: prefix shell commands with `rtk` for token compression.
- `.agents/rules/graphify.md`: knowledge graph at `graphify-out/`. After code changes run `graphify update .`.
- `.agents/workflows/graphify.md` and `.agents/skills/agent-browser/` — consult before starting work (per GEMINI.md mandate).
