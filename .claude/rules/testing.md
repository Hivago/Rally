# Testing Rules — Rally

## Backend (.NET) Testing

### Framework
- xUnit for test runner
- FluentAssertions for readable assertions
- NSubstitute or Moq for mocking
- Respawn for database cleanup in integration tests
- TestContainers for PostgreSQL in CI (when GitHub Actions added)

### What to Test

| Layer | What | How |
|-------|------|-----|
| Domain | Entity behavior, value object validation, state transitions | Unit test, no mocking needed |
| Application | Command/Query handlers, business rules | Unit test, mock repositories |
| Infrastructure | Repository queries, EF Core mappings | Integration test with real DB |
| API | Endpoint routing, auth, validation, response shape | Integration test with WebApplicationFactory |

### Test Naming

`MethodName_Scenario_ExpectedBehavior`

```csharp
[Fact]
public async Task Handle_WhenOrderIsPlaced_ShouldTransitionToConfirmed()

[Fact]
public async Task Handle_WhenOrderAlreadyConfirmed_ShouldReturnError()

[Fact]
public async Task CreateOrder_WithEmptyItems_ShouldReturn400()
```

### Coverage Targets

- Domain: 90%+ (critical business logic)
- Application handlers: 80%+
- Infrastructure: 60%+ (integration tests are slower)
- API endpoints: 70%+

### Test Data

- Use builder pattern for test entities: `new OrderBuilder().WithStatus(Confirmed).Build()`
- Factory methods for common test setups
- Never depend on seed data or other test's state

## Frontend (React) Testing

### Framework
- Vitest for unit tests
- React Testing Library for component tests
- MSW (Mock Service Worker) for API mocking
- Playwright for E2E (when ready)

### What to Test

| Layer | What | How |
|-------|------|-----|
| Hooks | Custom hooks (useOrders, useAuth) | renderHook + MSW |
| Components | Interactive components, conditional rendering | RTL + user-event |
| Pages | Page-level integration (data loading, navigation) | RTL + MSW |
| E2E | Critical flows (login, accept order, update menu) | Playwright |

### What NOT to Test

- Tailwind class application (visual testing if needed, not unit)
- Third-party library internals (React Query, SignalR)
- Simple presentational components with no logic
- GSAP animation details (test that elements render, not animation values)
