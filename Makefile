# Auto-detect container engine (Docker or Podman)
CONTAINER_ENGINE ?= $(shell command -v docker >/dev/null 2>&1 && echo "docker" || (command -v podman >/dev/null 2>&1 && echo "podman" || echo "docker"))
COMPOSE ?= $(CONTAINER_ENGINE) compose

.PHONY: build test test-unit test-integration run migration-add migration-update migration-remove compose-up compose-down compose-test-up compose-test-down compose-test-logs

# Build & Restore
build:
	dotnet build

# Run all automated tests (Unit & Integration)
test:
	dotnet test

# Run only pure Unit Tests (In-memory, extremely fast, no Postgres required)
test-unit:
	dotnet test --filter "FullyQualifiedName~NetForum.Tests.Unit"

# Run only PostgreSQL Integration Tests (Requires Postgres Testcontainers)
test-integration:
	dotnet test --filter "FullyQualifiedName~NetForum.Tests.Integration"

# Run Web Application locally
run:
	dotnet run --project NetForum/NetForum.csproj

# Generate a new EF Core migration (Usage: make migration-add name=MyMigrationName)
migration-add:
	@if [ -z "$(name)" ]; then \
		echo "❌ Error: 'name' parameter is required. Usage: make migration-add name=YourMigrationName"; \
		exit 1; \
	fi
	dotnet ef migrations add $(name) -p NetForum/NetForum.csproj --output-dir Data/Migrations

# Apply migrations to development PostgreSQL
migration-update:
	dotnet ef database update -p NetForum/NetForum.csproj

# Remove the last migration that hasn't been applied
migration-remove:
	dotnet ef migrations remove -p NetForum/NetForum.csproj

# Docker Compose — local dependencies only
compose-up:
	$(COMPOSE) up -d

compose-down:
	$(COMPOSE) down

# Docker Compose — full stack (containerized app + dependencies)
compose-test-up:
	$(COMPOSE) -f compose.test.yaml up --build -d

compose-test-down:
	$(COMPOSE) -f compose.test.yaml down

compose-test-logs:
	$(COMPOSE) -f compose.test.yaml logs -f app
