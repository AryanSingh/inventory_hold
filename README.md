# Inventory Hold Microservice

E-Commerce checkout inventory hold system built with .NET, MongoDB, Redis, RabbitMQ, and React.

## Architecture

**Domain-Driven Design** with clean separation:

```
src/
├── Contracts/        # Shared DTOs, events, request/response models
├── Domain/           # Entities, interfaces, domain services (no infrastructure dependencies)
├── Infrastructure/   # MongoDB, Redis, RabbitMQ implementations
├── WebApi/           # ASP.NET Core controllers, DI, startup
tests/
└── UnitTests/        # xUnit tests with Moq
frontend/
└── src/              # React/TypeScript SPA
spec/
├── architecture.md   # Full design specification
├── decisions.md      # Architecture Decision Records
├── contracts/        # API and data schemas
└── task-graph.json   # Implementation task decomposition
```

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Backend | .NET 8, ASP.NET Core |
| Database | MongoDB 7 (atomic operations) |
| Cache | Redis 7 (cache-aside pattern) |
| Messaging | RabbitMQ 3.13 (fanout exchange) |
| Frontend | React 19, TypeScript, Vite |
| Testing | xUnit, Moq |
| Container | Docker, docker-compose |

## Quick Start

### Docker (Recommended)

```bash
docker-compose up --build
```

Services:
- **Frontend**: http://localhost:3000
- **API**: http://localhost:5051
- **MongoDB**: localhost:27017
- **Redis**: localhost:6379
- **RabbitMQ**: http://localhost:15672 (guest/guest)

### Local Development

**Backend:**
```bash
dotnet restore
dotnet build
dotnet run --project src/WebApi
```

**Frontend:**
```bash
cd frontend
npm install
npm run dev
```

The frontend dev server proxies `/api` requests to the backend on port 5051.

## API Endpoints

| Method | Endpoint | Description | Status |
|--------|----------|-------------|--------|
| POST | /api/holds | Create inventory hold | 201, 400, 404, 409 |
| GET | /api/holds/{holdId} | Get hold details | 200, 404 |
| DELETE | /api/holds/{holdId} | Release hold | 200, 404, 410 |
| GET | /api/inventory | Get inventory levels | 200 |

## Key Features

- **Atomic Inventory Operations**: MongoDB `findOneAndUpdate` ensures no overselling under concurrent requests
- **Automatic Hold Expiration**: Background service polls every 30s, releases expired holds and restores inventory
- **Cache-Aside Pattern**: Redis caches inventory with 30s TTL, invalidated on every hold mutation
- **Event Publishing**: Hold lifecycle events (Created, Released, Expired) published to RabbitMQ fanout exchange
- **ProblemDetails Errors**: RFC 7807 compliant error responses

## Testing

```bash
dotnet test
```

21 unit tests covering:
- Hold entity business rules (activation, release, expiration)
- Inventory service (cache hit/miss, seeding)
- Hold service (create, get, release with mocking)

## Data Seed

On startup, 5 products are seeded into MongoDB (if empty):

| Product | SKU | Stock |
|---------|-----|-------|
| Laptop Pro 16" | PROD-001 | 25 |
| Wireless Mouse | PROD-002 | 150 |
| USB-C Hub | PROD-003 | 80 |
| Mechanical Keyboard | PROD-004 | 45 |
| 4K Monitor | PROD-005 | 30 |
