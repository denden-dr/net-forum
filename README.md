# 🚀 NetForum

NetForum is a modern, high-performance, and visually premium developer forum built as a single-project **Blazor Web App** using .NET 10, PostgreSQL, and Entity Framework Core.

---

## 🏗️ Technical Stack & Architecture

*   **Runtime & Framework:** .NET 10.0 SDK
*   **Web Framework:** Blazor Web App with **Interactive Server** render mode (`@rendermode InteractiveServer`). Components synchronize UI changes in real-time over a persistent SignalR WebSocket connection.
*   **Database & ORM:** PostgreSQL 18 Alpine queried via Entity Framework Core (EF Core 10.0) using dependency-injected thread-safe database context factories (`IDbContextFactory<AppDbContext>`).
*   **Containerized Test Engine:** xUnit running sequentially against a Docker/Podman **PostgreSQL 18** database managed by **Testcontainers.PostgreSql**.
*   **UI System:** A responsive **Minimalist Slate Light Theme** built with Vanilla CSS variables, Google Fonts (Inter), and CDN Bootstrap Icons.

---

## 📂 Project Organization

The solution keeps a clear separation between production code and automated test assemblies:

```text
net-forum/                       # Repository Root
├── NetForum.sln                 # .NET Solution File
├── NetForum.slnx                # Modern Visual Studio Solution Descriptor
├── README.md                    # System Guide & Onboarding
├── GEMINI.md                    # Developer Guidelines & TDD Shortcuts
├── Makefile                     # Build, Run, Test, and Container Shortcuts
├── docs/                        # Modular Technical Documents
│   ├── architecture.md          # Blazor Circuits, DB Schemas, and CSS HSL variables
│   ├── testing.md               # Unit vs Integration splits, Testcontainers, and Truncation
│   └── api.md                   # IForumService API definitions and specifications
├── NetForum/                    # Production Blazor Web Application
│   ├── Program.cs               # Web Application Entry Point & DI Container
│   ├── App.razor                # Primary HTML host & script loader
│   ├── Data/
│   │   ├── AppDbContext.cs      # EF Core DbContext, entity bindings, and seed data
│   │   ├── Entities/            # Relational database models (Category, Thread, Post, Notification)
│   │   └── Repositories/        # Persistence Abstraction (Repository Pattern)
│   │       ├── IForumRepository.cs # Data access interface contract
│   │       ├── ForumRepository.cs  # Optimized thread-safe EF queries (Primary Constructor)
│   │       ├── INotificationRepository.cs # Data access interface contract for notifications
│   │       └── NotificationRepository.cs  # Database queries for notification logs
│   ├── Services/
│   │   ├── IForumService.cs     # Forum operations interface contract
│   │   ├── ForumService.cs      # Pure business logic, trimming, and sanitizations
│   │   ├── INotificationService.cs # Notification operations interface contract
│   │   └── NotificationService.cs  # Mentions parsing and non-blocking notification delivery
│   └── wwwroot/
│       └── app.css              # Custom Vanilla Light variables and layout rules
└── NetForum.Tests/              # Developer Automated Testing Suite
    ├── Unit/
    │   ├── ForumServiceUnitTests.cs  # RAM-only fast mock service unit tests (Moq backed)
    │   └── HomeComponentTests.cs     # bUnit component rendering tests (Mock Service backed)
    └── Integration/
        ├── ForumServiceIntegrationTests.cs # Full DB constraint & cascading integration tests (IAsyncLifetime)
        ├── TestcontainersVerificationTests.cs # Migration pipeline & latency checks (Primary Constructor)
        └── TestDbContextFactory.cs        # Collection Fixture & container lifecycles
```

---

## ⌨️ Quickstart: Setup & Local Execution

We provide a comprehensive **Makefile** to automate local dependencies. Ensure you have **Podman** (or Docker) and the **.NET 10 SDK** installed.

### 1. Boot up the Development Database
Start a local PostgreSQL 18 container in the background:
```bash
make db-start
```

### 2. Apply Schema Migrations
Create your database tables and seed core discussion categories automatically:
```bash
make migration-update
```

### 3. Run the Automated Tests (47 Tests!)
Verify all unit, component, and Postgres integration tests are passing using our fast Makefile targets:
* **Run everything (Unit + Integration):**
  ```bash
  make test
  ```
* **Run RAM-only fast Unit tests (Under 1s!):**
  ```bash
  make test-unit
  ```
* **Run Postgres container Integration tests:**
  ```bash
  make test-integration
  ```

### 4. Start the Web Server
Launch the Blazor host in development mode:
```bash
make run
```
Open **`http://localhost:5201`** in your browser to experience the forum!

---

## 🌟 Key Features

1. **User Authentication & Role System:** Complete registration, interactive login, and secure session management backed by ASP.NET Core Identity and local PostgreSQL databases.
2. **Interactive Email Verification:** Automatic generation of verification links printed directly to the development console. Interactive Blazor pages verify tokens and confirm accounts seamlessly.
3. **Write Privilege Safety Locks:** Automatic service-level validation blocks threads/posts/upvotes for unverified email users.
4. **Dev Fallback User Context:** High-productivity context fallback provider (`DevCurrentUserService`) ensuring local development workflows and automated mock testing run perfectly out-of-the-box.
5. **Dynamic Category Directory:** Sidebar layouts tracking active tags, icons, views, and chronological threads.
6. **Cascading Upvote System:** C# state mutations for threads and replies with instant reactive UI increments.
7. **Quoted Comment Threading:** Linear comment timelines supporting self-referencing parent quotations.
8. **Instant Keyword Filtering:** Fast case-insensitive search matching Thread Title and Content.
9. **Asynchronous User Notifications & @Mentions:** Fire-and-forget background worker execution to parse usernames and notify recipients on thread replies, quotations, and active mentions without blocking the UI rendering cycle.
