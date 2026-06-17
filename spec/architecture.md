# Inventory Hold Microservice — Architecture

## Overview

A .NET 10 microservice implementing the Inventory Hold pattern for e-commerce checkout. When a customer begins checkout, items are temporarily held so they can't be sold to another customer. Holds expire after a configurable duration (default: 15 minutes).

## Technology Stack

| Component | Technology | Version |
|-----------|-----------|---------|
| API Runtime | .NET | 10.0 |
| Language | C# | 13 |
| Database | MongoDB | 7.0 |
| Cache | Redis | 7.2 |
| Message Broker | RabbitMQ | 3.13 |
| Frontend | React + TypeScript + Vite | 18.x |
| Testing | NUnit + Moq | 3.x / 4.x |
| Containerization | Docker + docker-compose | latest |

## DDD Layering

```
src/
├── InventoryHold.Contracts/          # DTOs, enums, request/response models
├── InventoryHold.Domain/             # Core business logic (no infrastructure dependencies)
│   ├── Entities/                     # Hold, InventoryItem aggregates
│   ├── Services/                     # HoldService (orchestrates business rules)
│   └── Repositories/                 # IInventoryRepository, IHoldRepository interfaces
├── InventoryHold.Infrastructure/     # External system implementations
│   ├── Persistence/                  # MongoDB repositories
│   ├── Caching/                      # Redis cache service
│   └── Messaging/                    # RabbitMQ event publisher
├── InventoryHold.WebApi/             # API layer
│   ├── Controllers/                  # HoldsController, InventoryController
│   ├── Middleware/                    # Error handling middleware
│   └── Program.cs                    # DI registration, app configuration
└── InventoryHold.UnitTests/          # nUnit tests with Moq
```

**Dependency Rule:** Domain has zero infrastructure dependencies. Infrastructure implements Domain interfaces. WebApi references both.

## Data Model

### InventoryItem (MongoDB collection: `inventory`)
```json
{
  "_id": "ObjectId",
  "ProductId": "string (GUID)",
  "ProductName": "string",
  "AvailableQuantity": "int",
  "ReservedQuantity": "int",
  "TotalQuantity": "int",
  "UpdatedAt": "DateTime UTC"
}
```

### Hold (MongoDB collection: `holds`)
```json
{
  "_id": "ObjectId",
  "HoldId": "string (GUID)",
  "Items": [
    {
      "ProductId": "string",
      "ProductName": "string",
      "Quantity": "int"
    }
  ],
  "Status": "Active | Released | Expired",
  "CreatedAt": "DateTime UTC",
  "ExpiresAt": "DateTime UTC",
  "ReleasedAt": "DateTime UTC?"
}
```

## Concurrency Strategy

**MongoDB atomic operations** for inventory mutations:
- Hold creation: `findOneAndUpdate` with filter `AvailableQuantity >= requested` and update `$inc: { AvailableQuantity: -qty, ReservedQuantity: +qty }` — atomic, prevents race conditions
- Hold release: `$inc: { AvailableQuantity: +qty, ReservedQuantity: -qty }`
- Use `ReturnDocument.After` to get updated state

## Caching Strategy

**Redis** for `GET /api/inventory` (high-frequency read path):
- Cache key: `inventory:levels`
- TTL: 30 seconds (balances freshness vs performance)
- Invalidate on: hold creation, hold release, hold expiry
- Cache-aside pattern: check cache first, fallback to MongoDB, populate cache

## Messaging

**RabbitMQ** — fanout exchange `hold.events` with 3 queues:

| Event | Trigger | Payload |
|-------|---------|---------|
| HoldCreated | POST /api/holds success | holdId, items, expiresAt |
| HoldReleased | DELETE /api/holds/{id} success | holdId, items, releasedAt |
| HoldExpired | Background timer fires | holdId, items, expiredAt |

Exchange type: `fanout` (simple, allows multiple consumers later)

## API Endpoints

| Method | Path | Status Codes | Description |
|--------|------|-------------|-------------|
| POST | /api/holds | 201, 400, 409 | Create hold, deduct inventory atomically |
| GET | /api/holds/{holdId} | 200, 404 | Get hold by ID (handles expired) |
| DELETE | /api/holds/{holdId} | 200, 404, 410 | Release hold, restore inventory |
| GET | /api/inventory | 200 | List all inventory levels |

## Background Processing

A hosted service (`HoldExpirationService`) runs every 30 seconds:
- Queries MongoDB for holds where `Status == Active && ExpiresAt <= now`
- For each expired hold: marks as Expired, restores inventory, publishes HoldExpired event

## Error Handling

- Global exception middleware catches unhandled exceptions → 500 with structured error
- Business exceptions return appropriate status codes (400, 404, 409)
- All responses use `ProblemDetails` format (RFC 7807)

## Key Design Decisions

1. **HoldId as GUID string** — client-generated, no DB round-trip for ID
2. **Soft expiration** — holds marked Expired, not deleted; audit trail preserved
3. **Configurable TTL** — default 15min, override via `HOLD_DURATION_MINUTES` env var
4. **No authentication** — per assignment requirements
5. **Seed data on startup** — 5 products inserted if inventory collection is empty
