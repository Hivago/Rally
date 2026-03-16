# C# / .NET Coding Rules

> Always-follow rules for all C# code in RallyAPI.

## Architecture

- **Modular Monolith**: Each module is self-contained with Domain → Application → Infrastructure → Api layers
- **CQRS**: Commands (writes) and Queries (reads) are separate. Both go through MediatR.
- **Domain-Driven Design**: Rich domain models with behavior, not anemic data bags.

## Domain Layer (Zero Dependencies)

- Entities inherit from `Entity` or `AggregateRoot` base class
- Value objects are `record` types with validation in constructor
- Domain events implement `IDomainEvent`
- No EF Core attributes on domain entities — configure in Infrastructure
- Entities encapsulate behavior: `order.Confirm()` not `order.Status = OrderStatus.Confirmed`
- Guard clauses at the top of public methods
- Private setters — state changes only through domain methods

```csharp
// GOOD: Rich domain model
public class Order : AggregateRoot
{
    public OrderStatus Status { get; private set; }
    
    public void Confirm()
    {
        if (Status != OrderStatus.Placed)
            throw new DomainException("Only placed orders can be confirmed");
        
        Status = OrderStatus.Confirmed;
        AddDomainEvent(new OrderConfirmedEvent(Id));
    }
}

// BAD: Anemic model
public class Order : Entity
{
    public OrderStatus Status { get; set; } // public setter = no encapsulation
}
```

## Application Layer

- Commands and Queries are `record` types
- One handler per command/query
- FluentValidation validator for EVERY command (queries can skip if trivial)
- Return `Result<T>` from handlers, not raw exceptions
- DTOs are `record` types — never expose domain entities to API layer
- Use `CancellationToken` in all async handlers

```csharp
// Command
public record CreateOrderCommand(Guid CustomerId, Guid RestaurantId, List<OrderItemDto> Items) 
    : IRequest<Result<OrderDto>>;

// Handler
public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, Result<OrderDto>>
{
    // constructor with repository injection
    public async Task<Result<OrderDto>> Handle(CreateOrderCommand request, CancellationToken ct)
    {
        // 1. Load aggregates from repo
        // 2. Call domain methods
        // 3. Save via unit of work
        // 4. Return mapped DTO
    }
}

// Validator
public class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.RestaurantId).NotEmpty();
        RuleFor(x => x.Items).NotEmpty().WithMessage("Order must have at least one item");
    }
}
```

## Infrastructure Layer

- EF Core configurations via `IEntityTypeConfiguration<T>`
- Repository implementations — interfaces live in Application layer
- External service integrations (PayU, MSG91, R2)
- Use `AsNoTracking()` for read-only queries
- Use `Include()` explicitly — no lazy loading
- Migrations per module context

## API Layer (Minimal APIs)

- Thin endpoints: deserialize → send to MediatR → return response
- Group endpoints by module: `app.MapGroup("/api/orders")`
- Use `.RequireAuthorization()` with role policies
- Return consistent response shape
- Use `TypedResults` for clear return types

```csharp
// GOOD: Thin endpoint
app.MapPost("/api/orders", async (CreateOrderCommand command, IMediator mediator) =>
{
    var result = await mediator.Send(command);
    return result.IsSuccess 
        ? Results.Created($"/api/orders/{result.Value.Id}", result.Value)
        : Results.BadRequest(result.Error);
}).RequireAuthorization("Customer");
```

## EF Core Rules

- `DateTimeOffset` for ALL timestamps, never `DateTime`
- Soft delete via `IsDeleted` bool + global query filter
- Audit fields: `CreatedAt`, `UpdatedAt`, `CreatedBy`, `UpdatedBy`
- Configure decimal precision: `.HasPrecision(18, 2)` for money
- Use `Guid` for primary keys, generated client-side with `Guid.NewGuid()`
- Index frequently filtered/sorted columns
- Use `ExecuteUpdateAsync` / `ExecuteDeleteAsync` for bulk operations (EF Core 7+)

## Error Handling

- Domain exceptions for invariant violations (e.g., invalid state transitions)
- `Result<T>` pattern for expected business failures
- Global exception middleware catches unhandled exceptions → 500 with correlation ID
- Never expose stack traces to clients
- Log with structured logging (Serilog when added)

## Testing

- Unit tests: Domain logic and handler logic (mock repositories)
- Integration tests: Full pipeline with test database
- Use `Respawn` for database cleanup between integration tests
- Test naming: `MethodName_Scenario_ExpectedBehavior`
- Arrange-Act-Assert pattern

## Performance

- `AsNoTracking()` for all read queries
- `Select` projections instead of loading full entities for queries
- Redis caching for: restaurant menus, user sessions, rate limiting
- Pagination on all list endpoints (default 20, max 100)
- Use `IQueryable` to push filtering to database, not in-memory
