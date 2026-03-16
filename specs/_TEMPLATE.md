# Feature Spec: [Feature Name]

> **Status**: Draft | In Progress | Ready for Review | Approved
> **Priority**: P0 (Critical) | P1 (High) | P2 (Medium) | P3 (Low)
> **Estimated Effort**: [X days]
> **Module(s)**: [Orders / Delivery / Payments / Restaurants / Users / Notifications]
> **Owner**: [Name]
> **Date**: [YYYY-MM-DD]

---

## 1. Problem Statement

[2-3 sentences: What problem? Who is affected?]

## 2. User Stories

- As a **customer**, I want to [action] so that [benefit]
- As a **restaurant**, I want to [action] so that [benefit]
- As a **rider**, I want to [action] so that [benefit]
- As an **admin**, I want to [action] so that [benefit]

## 3. Acceptance Criteria

- [ ] [Specific, testable criterion]
- [ ] [Another criterion]
- [ ] Edge case: [what happens when X]
- [ ] Error case: [what happens on failure]

## 4. Technical Design

### Domain Changes

```csharp
// New/modified entities, value objects, domain events
// Module: [which module]
```

### Commands & Queries (MediatR)

| Type | Name | Description |
|------|------|-------------|
| Command | `CreateXCommand` | |
| Query | `GetXQuery` | |

### API Endpoints

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| POST | `/api/v1/...` | Customer | |
| GET | `/api/v1/...` | Restaurant | |

### SignalR Events

| Hub | Event | Payload | Triggered When |
|-----|-------|---------|----------------|
| OrderHub | `OrderStatusChanged` | `{orderId, status, timestamp}` | |

### Database Migrations

```sql
-- Key schema changes (EF Core will generate, but document intent)
```

### Cross-Module Communication

| From Module | To Module | Via | Event/Contract |
|------------|-----------|-----|---------------|
| | | Domain Event | |

### Frontend Components (if applicable)

| Component | Page | Description |
|-----------|------|-------------|
| | | |

## 5. Edge Cases & Error Handling

| Scenario | Expected Behavior |
|----------|-------------------|
| | |
| Network failure during X | |
| Concurrent modification | |

## 6. Testing Plan

- **Domain unit tests**: [what entity/aggregate behavior to test]
- **Handler unit tests**: [what command/query handlers]
- **Integration tests**: [what endpoints]
- **Frontend tests**: [what components/flows]

## 7. Rollout

- [ ] Feature flag: `FEATURE_[NAME]`
- [ ] Metrics to track: [what success looks like]
- [ ] Rollback plan: [how to revert]

---

## Implementation Notes (updated during build)

### Files Created/Modified
- 

### Decisions Made
- 

### Open Questions
- 
