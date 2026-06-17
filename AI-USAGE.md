# AI Usage Documentation

## Project: Inventory Hold Microservice

### AI Tools Used

**Primary:** Claude (Anthropic) via OpenCode CLI

### How AI Was Used

#### 1. Architecture & Design (Spec Phase)
- Generated the full DDD architecture specification (`spec/architecture.md`)
- Created API contracts and schemas (`spec/contracts/`)
- Designed 7 Architecture Decision Records (`spec/decisions.md`)
- Created task graph decomposition (`spec/task-graph.json`)

#### 2. Backend Implementation (.NET)
- Scaffolded the .NET solution with 5 projects (Contracts, Domain, Infrastructure, WebApi, UnitTests)
- Implemented domain entities, interfaces, and domain services
- Implemented infrastructure layer (MongoDB repositories, Redis cache, RabbitMQ publisher, background expiration service)
- Created REST controllers with proper HTTP status codes and ProblemDetails error handling
- Configured dependency injection and application startup

#### 3. Frontend Implementation (React/TypeScript)
- Built the React SPA with Vite + TypeScript
- Created inventory dashboard with real-time polling
- Built hold creation form with dynamic item management
- Implemented active holds list with release functionality
- Configured nginx reverse proxy for production builds

#### 4. Infrastructure
- Created multi-stage Dockerfile for .NET API
- Created docker-compose.yml with 5 services (API, MongoDB, Redis, RabbitMQ, Frontend)
- Configured service health checks and dependencies

#### 5. Testing
- Generated unit tests for domain entities (Hold, InventoryItem)
- Created service tests with Moq mocking (HoldService, InventoryService)
- 21 total tests covering business logic and edge cases

### What AI Did NOT Do
- No code was blindly accepted without review
- Domain business rules were validated against assignment requirements
- Concurrency strategy (atomic MongoDB operations) was specifically designed for inventory management
- All AI-generated code was verified via `dotnet build` and `dotnet test`

### Prompting Strategy
- Used Software Factory methodology: specs first, then small-batch implementation
- Each phase was verified before moving to the next
- Task graph decomposition kept changes under 150 LOC per task
- Quality gates enforced at each phase boundary

### Limitations Encountered
- MongoDB.Driver v3 has breaking API changes from v2 (documented in code)
- RabbitMQ.Client v7 is async-only (all publish operations use async pattern)
- TypeScript `erasableSyntaxOnly` in newer Vite requires `const` objects instead of `enum`
- No list endpoint for holds required client-side tracking via localStorage
