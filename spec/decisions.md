# Design Decisions (ADRs)

## ADR-001: MongoDB as Primary Data Store
**Decision:** Use MongoDB for persistent storage of holds and inventory.
**Rationale:** Document model fits the nested hold items structure. Atomic `findOneAndUpdate` provides concurrency safety without transactions. Native JSON compatibility with .NET.

## ADR-002: Redis Cache-Aside for Inventory
**Decision:** Cache inventory levels in Redis with 30s TTL, invalidate on mutations.
**Rationale:** GET /api/inventory is the highest-frequency read. Cache-aside is simple and correct. 30s TTL is a reasonable freshness/performance tradeoff for an assignment.

## ADR-003: Fanout Exchange for Events
**Decision:** Use RabbitMQ fanout exchange `hold.events` instead of topic/direct.
**Rationale:** Simple, no routing key logic needed. Allows adding consumers later without changing the publisher. Each event type published as separate message with `event_type` header.

## ADR-004: Background Service for Expiry
**Decision:** Polling hosted service (30s interval) instead of RabbitMQ delayed messages or Hangfire.
**Rationale:** Simplest correct implementation. 30s granularity is acceptable for hold expiration. No additional dependencies.

## ADR-005: xUnit over NUnit
**Decision:** Use xUnit for testing (modern, parallel-by-default, better async support).
**Rationale:** Assignment allows either. xUnit is the more common choice in modern .NET.

## ADR-006: Client-Generated GUIDs for HoldId
**Decision:** HoldId is a client-provided GUID string.
**Rationale:** Avoids database round-trip for ID generation. Enables idempotent retries. Common pattern in distributed systems.

## ADR-007: ProblemDetails Error Responses
**Decision:** Use RFC 7807 ProblemDetails for all error responses.
**Rationale:** Standardized error format. Built-in support in ASP.NET Core. Clean for frontend consumption.
