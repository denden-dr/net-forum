.PHONY: build test test-unit test-integration run db-start db-stop db-status migration-add migration-update migration-remove

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

# Start development PostgreSQL 18 container (Podman)
db-start:
	podman start netforum-db || podman run --name netforum-db -p 5432:5432 -e POSTGRES_PASSWORD=postgres -d docker.io/library/postgres:18-alpine

# Stop database container
db-stop:
	podman stop netforum-db

# Check database container status
db-status:
	podman ps -a --filter name=netforum-db

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
