# Inventory Hold Microservice

E-Commerce checkout inventory hold system — when a customer begins checkout, their items are held so they can't be sold to another customer. Holds expire after a configurable duration.

## Architecture

**Domain-Driven Design** with clean separation across 5 projects:

```
src/
├── InventoryHold.Contracts/        # DTOs, enums, request/response models
├── InventoryHold.Domain/
│   ├── Services/                   # Business logic (HoldService, InventoryService)
│   └── Repositories/               # Data access interfaces (IHoldRepository, IInventoryRepository, ICacheService, IEventPublisher)
├── InventoryHold.Infrastructure/   # MongoDB, Redis, RabbitMQ implementations
├── InventoryHold.WebApi/           # ASP.NET Core controllers, DI setup, Program.cs
└── InventoryHold.UnitTests/        # xUnit tests with Moq mocking
frontend/
└── src/                            # React/TypeScript SPA (Vite)
```

### Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Framework | .NET 10, C# | Assignment requirement; latest LTS |
| Database | MongoDB 7 | Native JSON documents fit variable hold item shapes; `findOneAndUpdate` provides atomic decrement without row locks |
| Caching | Redis (cache-aside, 30s TTL) | Inventory reads >> writes; explicit invalidation on every hold mutation prevents stale data |
| Messaging | RabbitMQ fanout exchange `hold.events` | Decoupled event publishing; downstream consumers can subscribe independently |
| Testing | xUnit + Moq | Mocks repository, cache, and messaging interfaces — no running infrastructure needed |
| Concurrency | MongoDB atomic operations + optimistic locking (version field) | Atomic decrement prevents overselling; version-based release prevents double inventory restoration |
| Hold expiration | Background service polling every 30s | Simpler than TTL-based approach; ensures inventory is restored promptly |
| Frontend | React 19 + TypeScript + Vite | Fast dev server, proper type safety, modern build tooling |

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Backend | .NET 10, ASP.NET Core |
| Database | MongoDB 7 (atomic operations) |
| Cache | Redis 7 (cache-aside pattern) |
| Messaging | RabbitMQ 3.13 (fanout exchange) |
| Frontend | React 19, TypeScript, Vite |
| Testing | xUnit, Moq (40 tests) |
| Container | Docker, docker-compose |

## Quick Start

### Docker (Recommended)

```bash
docker-compose up --build
```

This starts all 5 services:

| Service | URL | Purpose |
|---------|-----|---------|
| Frontend | http://localhost:3000 | React SPA |
| API | http://localhost:5051 | ASP.NET Core REST API |
| MongoDB | localhost:27017 | Persistent data store |
| Redis | localhost:6379 | Caching layer |
| RabbitMQ | http://localhost:15672 (guest/guest) | Message broker |

### Local Development

**Backend:**
```bash
dotnet restore
dotnet build
dotnet run --project src/InventoryHold.WebApi
```

**Frontend:**
```bash
cd frontend
npm install
npm run dev
```

The frontend dev server proxies `/api` requests to the backend on port 5051.

## API Endpoints

| Method | Endpoint | Description | Status Codes |
|--------|----------|-------------|-------------|
| POST | /api/holds | Create inventory hold | 201, 400, 404, 409 |
| GET | /api/holds | List active holds | 200 |
| GET | /api/holds/{holdId} | Get hold details | 200, 404 |
| DELETE | /api/holds/{holdId} | Release hold, restore inventory | 200, 404, 410 |
| GET | /api/inventory | View inventory levels | 200 |

## Key Features

- **Atomic Inventory Operations**: MongoDB `findOneAndUpdate` with `$inc` ensures no overselling under concurrent requests
- **Optimistic Locking**: Hold release uses version-based concurrency control to prevent double inventory restoration
- **Automatic Hold Expiration**: Background service polls every 30s, releases expired holds and restores inventory
- **Cache-Aside Pattern**: Redis caches inventory with configurable TTL (default 30s), invalidated on every hold mutation
- **Event Publishing**: Hold lifecycle events (Created, Released, Expired) published to RabbitMQ fanout exchange with full context payload
- **ProblemDetails Errors**: RFC 7807 compliant error responses with meaningful HTTP status codes
- **Structured Logging**: Serilog with console + rolling file output (7-day retention)
- **Distributed Tracing**: OpenTelemetry with OTLP exporter and Prometheus metrics endpoint
- **Per-IP Rate Limiting**: Token bucket rate limiter with 30 requests/min per IP
- **Security Headers**: CSP, X-Frame-Options, X-Content-Type-Options, Referrer-Policy

## Testing

```bash
dotnet test
```

**40 unit tests** covering:

| Test Class | Tests | Coverage |
|------------|-------|----------|
| HoldEntityTests | 8 | Entity state transitions (activation, release, expiration, expiry computation) |
| HoldServiceTests | 9 | Create, get, release with mocked repositories; concurrent modification guard |
| InventoryServiceTests | 4 | Cache hit/miss, race-safe seeding |
| HoldsControllerTests | 16 | Full CRUD validation, HTTP status codes, edge cases |
| HoldExpirationServiceTests | 3 | Expiry processing, skip claimed, handle empty list |

All tests mock `IHoldRepository`, `IInventoryRepository`, `ICacheService`, and `IEventPublisher` — no running infrastructure required.

## Data Seed

On startup, 5 products are seeded into MongoDB (idempotent upsert):

| Product | SKU | Stock |
|---------|-----|-------|
| Laptop Pro 16" | PROD-001 | 25 |
| Wireless Mouse | PROD-002 | 150 |
| USB-C Hub | PROD-003 | 80 |
| Mechanical Keyboard | PROD-004 | 45 |
| 4K Monitor | PROD-005 | 30 |
