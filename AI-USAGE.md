# AI Usage Documentation

## Project: Inventory Hold Microservice

### AI Strategy

**Primary Tool:** Claude (Anthropic) via OpenCode CLI, using a multi-agent orchestration pattern.

**Context Management Approach:**
- Fed the AI the full project specification and assignment PDF upfront to establish clear requirements
- Used a spec-first methodology: generated architecture decisions, API contracts, and task decomposition before writing any code
- Structured prompts around DDD layering — each AI request targeted one layer (Contracts → Domain → Infrastructure → WebApi)
- Employed parallel specialist agents: `@explorer` for codebase search, `@librarian` for library docs, `@oracle` for architectural review, `@fixer` for fast implementation, `@designer` for frontend polish
- Used compression to manage context window during long multi-phase sessions

**How Architecture Was Communicated:**
- Provided the AI with the exact folder structure from the assignment PDF
- Specified target frameworks (.NET 10, React 19) and tool choices (xUnit, MongoDB.Driver, RabbitMQ.Client)
- Defined quality gates: build must pass, all tests green, TypeScript compilation clean before proceeding

### Human Audit

#### Accepted AI Suggestions

1. **Atomic MongoDB `findOneAndUpdate` for inventory decrement** — AI proposed using `FilterDefinition` with `Builders<InventoryItem>.Filter.Gte(x => x.AvailableQuantity, quantity)` combined with `$inc` decrement. Accepted because this correctly prevents overselling under concurrent requests without application-level locking.

2. **`Lazy<Task<(IConnection, IChannel)>>` pattern for RabbitMQ** — AI originally used blocking `.GetAwaiter().GetResult()` in the DI registration constructor. After review, I accepted the AI's revised proposal to use lazy async initialization, which avoids deadlocks during startup while still providing connection reuse.

3. **Optimistic locking for hold release** — AI suggested using MongoDB's version-based concurrency (`UpdateOne` with version match) for releasing holds. Accepted because it correctly handles the edge case where two concurrent requests try to release the same hold — only one succeeds, preventing double inventory restoration.

4. **Cache-aside pattern with explicit invalidation** — AI chose Redis with 30s TTL and explicit invalidation on every hold mutation rather than write-through caching. Accepted because inventory reads are much more frequent than writes, and a 30s stale window is acceptable for an inventory display.

5. **`window.confirm()` for hold release** — AI used the browser's native confirmation dialog instead of building a custom modal. Accepted as pragmatic — the assignment says "pixel-perfect design" is not expected, and native dialogs are accessible by default.

#### Rejected AI Suggestions

1. **Customer name/email fields in hold requests** — AI initially generated `CreateHoldRequest` with `customerName` and `customerEmail` fields and built matching frontend form fields. **Rejected** because the assignment specification defines only `{holdId, items, durationMinutes}` as the hold request shape. These extra fields would not be stored or used by the backend, creating dead code and a misleading API contract.

2. **HoldStatus as numeric enum (0, 1, 2)** — AI generated the frontend `HoldStatus` enum as `{ Active: 0, Released: 1, Expired: 2 }` using TypeScript numeric enums. **Rejected** because C# serializes enums as strings by default (`"Active"`, not `0`), causing all status comparisons in the frontend to silently fail. Changed to string const objects matching the backend serialization.

3. **`localStorage` for hold tracking** — AI initially used `localStorage` to track created hold IDs because there was no list endpoint. **Rejected** because this creates a per-browser silo — holds created in one browser session are invisible in another, and data is lost on cache clear. Instead, I had the AI add a proper `GET /api/holds` list endpoint backed by MongoDB queries.

4. **Blanket CORS `AllowAnyOrigin`** — AI's initial Program.cs used `.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()` for development convenience. **Rejected** because this pattern often leaks into production. Replaced with explicit origin/method/header lists that can be narrowed further per environment.

5. **Sync-over-async `IConnection.CreateConnectionAsync().GetAwaiter().GetResult()`** — AI registered RabbitMQ's `IConnection` as a singleton using blocking sync-over-async in the DI container. **Rejected** because this can deadlock in ASP.NET Core's synchronization context under load. Replaced with lazy async initialization via `Lazy<Task<>>`.

### Verification

**How AI-Generated Code Was Validated:**

1. **Build verification** — Every AI-generated change was verified with `dotnet build` before proceeding. Zero-error tolerance enforced.

2. **Unit tests (40 total)** — AI generated tests for:
   - Hold entity state transitions (8 tests): activation, release, expiration, expiry computation
   - HoldService operations (9 tests): create valid/empty/insufficient stock, get existing/nonexistent, release active/nonexistent/already-released, concurrent modification guard
   - InventoryService (4 tests): cache hit/miss, seed if empty/exists
   - HoldsController (16 tests): CRUD validation, HTTP status codes, edge cases
   - HoldExpirationService (3 tests): processes expired, skips claimed, handles empty list
   - All tests use mocked `IHoldRepository`, `IInventoryRepository`, `ICacheService`, `IEventPublisher` — no running infrastructure required

3. **TypeScript compilation** — Frontend verified with `tsc --noEmit` after every change to catch type mismatches early.

4. **Docker E2E testing** — Full stack tested via `docker-compose up --build`:
   - 12 API curl tests covering all endpoints and error codes (200, 201, 400, 404, 409, 410)
   - 19 Playwright browser tests covering inventory display, hold creation flow, hold release with confirmation, responsive layout, and console error checks

5. **Ship-it audit cycle** — Ran 4 parallel audits (QA, Security, Production Readiness, Product Manager) to identify gaps. Found and fixed 3 Critical, 10 High, and 8 Medium issues across 3 rounds, achieving 10/10 scores in all categories before final verification.
