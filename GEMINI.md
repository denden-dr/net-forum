# NetForum - AI Assistant Guideline (GEMINI.md)

This file contains strict instructions, developer shortcuts, and context rules for any AI Assistant pair-programming on the **NetForum** project.

---

## 🛠️ Tech Stack Constraints & Context

*   **Platform:** .NET 10.0 C# Solution.
*   **Web Model:** Unified Blazor Web App with **Interactive Server** render mode (`@rendermode InteractiveServer`).
    *   *Do NOT split components into Client assemblies or use InteractiveWebAssembly/InteractiveAuto.*
    *   *Keep components inside `NetForum/Components/`.*
*   **Database & DB Operations:**
    *   **PostgreSQL** for local development and production.
    *   **Entity Framework Core** for schema definition, relationships, and migrations.
    *   Use `IDbContextFactory<AppDbContext>` injected in services for thread-safe concurrent database access under Blazor Server.
    *   No REST Controllers, endpoints, or DTO models are allowed for internal data. Blazor components must **directly inject** and query `IForumService` in C#.
*   **UI System (Vanilla Light Mode):**
    *   Minimalist, professional, high-contrast Light Mode theme.
    *   *Variables:* `--bg-primary: #f8fafc;`, `--bg-card: #ffffff;`, `--text-primary: #0f172a;`, `--accent: #4f46e5;` (Indigo).
    *   *Typography:* Google Font `Inter`, size-bound, smooth shadows, and soft rounded borders.
    *   *Icons:* CDN-loaded Bootstrap Icons (`bi-*`).

---

## 🤖 AI Assistant Workflows & .agents Integration

> [!IMPORTANT]
> **MANDATORY PRE-FLIGHT RULE:**
> Before proposing any code changes, starting feature implementations, executing shell operations, or providing *any* technical answers or solutions to the developer, the AI Assistant MUST proactively load, scan, and strictly apply all custom skills, execution workflows, and compliance rules defined in the **`.agents/`** directory.
> 
> Prioritize all rules and directory workflows under `.agents/` as core execution constraints for every single turn.

---

## 🧪 Strict Test-Driven Development (TDD) Rules

1.  **Write the Failing Test First (RED):**
    *   Any behavior change, refactoring, or new feature MUST have a failing unit test in `NetForum.Tests/Unit/ForumServiceUnitTests.cs` (or component test in `NetForum.Tests/Unit/HomeComponentTests.cs` using bUnit) before writing production code.
2.  **Verify RED:**
    *   Run `make test` and confirm the test fails because the logic is missing (not due to compiler syntax issues).
3.  **Write the Minimal Code to Pass (GREEN):**
    *   Write only the simplest implementation needed to make the test pass. Avoid speculative pre-coding or adding extra options.
4.  **Refactor:**
    *   Once green, clean up variables, deduplicate code, and improve architecture while keeping all tests in a green passing state.

---

## ⌨️ Makefile Command Shortcuts

Use these fast `make` commands from the repository root for developer workflows:

| Task / Operation | Makefile Shortcut | Default CLI Command |
| :--- | :--- | :--- |
| **Build Project** | `make build` | `dotnet build` |
| **Run Tests** | `make test` | `dotnet test` |
| **Run Application** | `make run` | `dotnet run --project NetForum/NetForum.csproj` |
| **Start Postgres** | `make db-start` | `podman start netforum-db` |
| **Stop Postgres** | `make db-stop` | `podman stop netforum-db` |
| **Postgres Status** | `make db-status` | `podman ps -a --filter name=netforum-db` |
| **Create Migration** | `make migration-add name=Name` | `dotnet ef migrations add Name -p NetForum/NetForum.csproj ...` |
| **Apply Migrations** | `make migration-update` | `dotnet ef database update -p NetForum/NetForum.csproj` |
| **Remove Migration** | `make migration-remove` | `dotnet ef migrations remove -p NetForum/NetForum.csproj` |


---

## 🚫 Forbidden Patterns

*   **No Placeholders:** Never write `// TODO`, `// implement later`, or stub out error handling inside files.
*   **No Direct DB Access in Components:** Blazor Razor components should never inject `AppDbContext` or use EF Core directly. They must interact only with `IForumService` services.
*   **No Dark Mode components:** Do not write dark theme variables or toggles unless explicitly requested by the user. All elements must default to the defined Light Mode palette.
