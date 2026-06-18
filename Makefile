.PHONY: build test run stop clean logs lint

# Build all projects
build:
	dotnet build --no-restore
	cd frontend && npm run build

# Restore packages
restore:
	dotnet restore
	cd frontend && npm install

# Run all unit tests
test:
	dotnet test tests/UnitTests/UnitTests.csproj --no-restore

# Run with coverage
test-coverage:
	dotnet test tests/UnitTests/UnitTests.csproj --no-restore /p:CollectCoverage=true /p:CoverletOutputFormat=opencover /p:CoverletOutput=./coverage/

# Start Docker services
up:
	docker compose up -d --build

# Stop Docker services
down:
	docker compose down

# Stop and remove volumes
clean:
	docker compose down -v

# View logs
logs:
	docker compose logs -f

# View API logs only
logs-api:
	docker compose logs -f api

# Health check
health:
	curl -sf http://localhost:5051/health || echo "API not healthy"

# Lint frontend
lint:
	cd frontend && npm run lint

# Frontend dev server
frontend-dev:
	cd frontend && npm run dev
