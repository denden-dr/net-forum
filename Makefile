# Auto-detect container engine and compose tool dynamically
ifeq ($(origin COMPOSE), undefined)
  # Check if CONTAINER_ENGINE is predefined
  ifeq ($(origin CONTAINER_ENGINE), undefined)
    ifeq ($(shell docker compose version >/dev/null 2>&1 && docker info >/dev/null 2>&1 && echo yes), yes)
      CONTAINER_ENGINE = docker
      COMPOSE = docker compose
    else ifeq ($(shell podman compose version >/dev/null 2>&1 && podman info >/dev/null 2>&1 && echo yes), yes)
      CONTAINER_ENGINE = podman
      COMPOSE = podman compose
    else ifeq ($(shell command -v podman-compose >/dev/null 2>&1 && podman info >/dev/null 2>&1 && echo yes), yes)
      CONTAINER_ENGINE = podman
      COMPOSE = podman-compose
    else ifeq ($(shell command -v docker >/dev/null 2>&1 && echo yes), yes)
      CONTAINER_ENGINE = docker
      COMPOSE = docker compose
    else ifeq ($(shell command -v podman >/dev/null 2>&1 && echo yes), yes)
      CONTAINER_ENGINE = podman
      COMPOSE = podman compose
    else
      CONTAINER_ENGINE = docker
      COMPOSE = docker compose
    endif
  else
    # CONTAINER_ENGINE is specified, define COMPOSE based on it
    ifeq ($(CONTAINER_ENGINE), podman)
      ifeq ($(shell podman compose version >/dev/null 2>&1 && echo yes), yes)
        COMPOSE = podman compose
      else ifeq ($(shell command -v podman-compose >/dev/null 2>&1 && echo yes), yes)
        COMPOSE = podman-compose
      else
        COMPOSE = podman compose
      endif
    else
      COMPOSE = docker compose
    endif
  endif
else
  # COMPOSE is predefined, define CONTAINER_ENGINE based on it if not set
  ifeq ($(origin CONTAINER_ENGINE), undefined)
    ifeq ($(findstring podman,$(COMPOSE)),podman)
      CONTAINER_ENGINE = podman
    else
      CONTAINER_ENGINE = docker
    endif
  endif
endif


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
