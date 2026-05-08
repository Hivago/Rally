# Agent Instructions — RallyAPI

> Read by Claude Code, Codex, Cursor, Antigravity.
> Defines how AI agents work on this specific project.

## Rule #1: Read CLAUDE.md First

Every session, every agent, every time. It has the architecture, conventions, and current project state.

## General Rules

1. **This is a Modular Monolith with Clean Architecture.** Respect module boundaries.
2. **CQRS via MediatR.** Commands for writes, Queries for reads. No exceptions.
3. **Domain layer is pure.** Zero dependencies on infrastructure, frameworks, or external packages.
4. **Specs drive features.** Check `specs/` before implementing. If no spec exists for a complex feature, create one first.
5. **Test after every change.** Run `dotnet build` then `dotnet test` for backend. Run `npm run typecheck` for frontend.

## Agent Roles

### Planner
When asked to plan a feature:
1. Identify which module(s) are affected
2. Create a spec in `specs/` using the template
3. Define: domain entities, commands/queries, API endpoints, SignalR events, frontend components
4. Flag cross-module communication needs (domain events)
5. Estimate effort

### Backend Implementer
When building .NET features:
1. **Domain first**: Create/modify entities, value objects, domain events in `Domain/`
2. **Application second**: Create Command/Query record, Handler, FluentValidation validator in `Application/`
3. **Infrastructure third**: Repository implementation, EF configuration, migrations in `Infrastructure/`
4. **API last**: Minimal API endpoint in `Api/`
5. **Tests**: Unit test the handler. Integration test the endpoint.
6. **Verify**: `dotnet build` → `dotnet test` → check no warnings

Pattern for a new command:
```
Modules/{Module}/
  Domain/Entities/Thing.cs              # Entity with behavior
  Application/Commands/CreateThing/
    CreateThingCommand.cs               # record with properties
    CreateThingCommandHandler.cs        # IRequestHandler<,>
    CreateThingCommandValidator.cs      # AbstractValidator<>
  Infrastructure/Repositories/ThingRepository.cs
  Api/ThingEndpoints.cs                 # app.MapPost("/api/things", ...)
```

### Frontend Implementer
When building React features:
1. Check existing components before creating new ones
2. Use Tailwind — no custom CSS files
3. Type everything — interfaces for API responses in `types/`
4. Use React Query (`useQuery`, `useMutation`) for API calls
5. Use `@microsoft/signalr` for real-time connections
6. GSAP animations: always clean up with `gsap.context()` in `useEffect`

### Code Reviewer
When reviewing, check for:
- **Module boundary violations** — does code import from another module's internals?
- **Missing validation** — every command needs a FluentValidation validator
- **Auth missing** — is the endpoint protected? Correct role?
- **Error handling** — is Result pattern used? No exceptions for business logic?
- **PayU security** — webhook hash verified? No trusting frontend redirects alone?
- **Missing tests** — handler logic should have unit tests
- **EF Core issues** — N+1 queries? Missing includes? No tracking for read-only queries?

### Debugger
1. Reproduce with a failing test
2. Check Railway logs / local Docker logs
3. For EF Core issues: enable SQL logging in dev
4. For SignalR issues: check connection state and group membership
5. For PayU issues: verify hash calculation matches PayU docs exactly
6. Fix root cause, verify test passes, check no regressions

## Module Communication Rules

| From | To | Allowed Method |
|------|----|---------------|
| Orders | Payments | Domain Event → MediatR Notification |
| Orders | Delivery | Domain Event → MediatR Notification |
| Orders | Notifications | Domain Event → MediatR Notification |
| Delivery | Notifications | Domain Event → MediatR Notification |
| Any | Users | Shared contract / interface in Shared/ |
| Any | Any (direct import) | ❌ NEVER |

## Context Management

- `/compact` after completing a feature, before starting the next
- `/clear` when switching between backend and frontend work
- `/model opus` for architecture decisions and complex debugging
- `/model sonnet` for routine implementation (saves ~60% tokens)

## MCP Tools (When Connected)

- **GitHub**: Create PRs, check issues, review code — use directly, don't ask
- **Notion**: Check project status, read specs, update progress

## What NOT to Do

- Don't put business logic in Minimal API endpoints
- Don't create `Service` classes — use MediatR handlers
- Don't reference another module's `Domain/` types directly
- Don't use `DateTime` — use `DateTimeOffset`
- Don't skip FluentValidation for "simple" commands
- Don't use `var` when the type isn't obvious from the right side
- Don't create React class components
- Don't use CSS modules or styled-components — Tailwind only
- Don't call APIs directly in components — use React Query hooks
