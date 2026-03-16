# Feature Spec: SignalR Real-Time Notifications (Web-First)

> **Status**: Ready for Implementation
> **Priority**: P0 (Critical — blocks web launch)
> **Estimated Effort**: 3-4 days
> **Module(s)**: Notifications, Orders, Delivery, Restaurants
> **Date**: 2026-03-16

---

## 1. Problem Statement

With the web-first pivot (March 14), FCM push notifications are deferred. Web users need real-time updates for order status, rider offers, and incoming restaurant orders. Without SignalR, users would need to manually refresh — unacceptable for a food delivery app.

## 2. User Stories

- As a **customer**, I want to see my order status update instantly without refreshing
- As a **restaurant**, I want to receive new orders in real-time so I can accept/reject quickly
- As a **rider**, I want to receive delivery offers in real-time so I can accept before others
- As an **admin**, I want to see live order flow and escalations

## 3. Acceptance Criteria

- [ ] Order status changes appear on customer screen within 2 seconds
- [ ] New orders appear on restaurant dashboard instantly (no refresh)
- [ ] Rider receives delivery offers in real-time
- [ ] Rider location updates stream to customer tracking screen
- [ ] Authenticated connections only (JWT in handshake)
- [ ] Auto-reconnect with exponential backoff on disconnect
- [ ] Fallback: Server-Sent Events → Long Polling (SignalR handles this)
- [ ] Works in all modern browsers (Chrome, Firefox, Safari, Edge)

## 4. Technical Design

### SignalR Hubs

#### OrderHub (`/hubs/orders`)
Handles order lifecycle events for all roles.

| Server → Client Event | Payload | Audience |
|----------------------|---------|----------|
| `OrderPlaced` | `{orderId, restaurantId, items, total, customerName}` | Restaurant group |
| `OrderStatusChanged` | `{orderId, status, timestamp, note?}` | Customer group + Restaurant group |
| `OrderCancelled` | `{orderId, reason, cancelledBy}` | All parties |

| Client → Server Method | Payload | Auth |
|------------------------|---------|------|
| `JoinOrderGroup` | `{orderId}` | Customer (own order), Restaurant (own order), Rider (assigned) |
| `LeaveOrderGroup` | `{orderId}` | Any authenticated |

#### DeliveryHub (`/hubs/delivery`)
Handles rider assignment and location tracking.

| Server → Client Event | Payload | Audience |
|----------------------|---------|----------|
| `DeliveryOfferReceived` | `{offerId, orderId, pickup, dropoff, earnings, expiresAt}` | Specific rider |
| `DeliveryOfferExpired` | `{offerId}` | Specific rider |
| `RiderLocationUpdated` | `{riderId, lat, lng, heading, speed, timestamp}` | Customer group (active order) |
| `RiderAssigned` | `{riderId, name, phone, vehicleType}` | Customer group + Restaurant group |

| Client → Server Method | Payload | Auth |
|------------------------|---------|------|
| `UpdateLocation` | `{lat, lng, heading, speed}` | Rider only |
| `AcceptOffer` | `{offerId}` | Rider only |
| `DeclineOffer` | `{offerId}` | Rider only |

#### NotificationHub (`/hubs/notifications`)
General notifications (not tied to a specific order).

| Server → Client Event | Payload | Audience |
|----------------------|---------|----------|
| `NewNotification` | `{id, type, title, body, data?, createdAt}` | Specific user |
| `NotificationRead` | `{id}` | Specific user |

### Domain Changes

```csharp
// Module: Notifications/Domain
public record SignalRConnectionInfo(
    string ConnectionId,
    Guid UserId,
    string Role,        // Customer, Restaurant, Rider, Admin
    DateTimeOffset ConnectedAt
);

// Domain Events (existing events that should trigger SignalR)
// Module: Orders/Domain
public record OrderPlacedEvent(Guid OrderId, Guid RestaurantId) : IDomainEvent;
public record OrderStatusChangedEvent(Guid OrderId, OrderStatus NewStatus) : IDomainEvent;

// Module: Delivery/Domain  
public record DeliveryOfferCreatedEvent(Guid OfferId, Guid RiderId, Guid OrderId) : IDomainEvent;
public record RiderLocationUpdatedEvent(Guid RiderId, double Lat, double Lng) : IDomainEvent;
```

### Commands & Queries

| Type | Name | Description |
|------|------|-------------|
| (none — SignalR is event-driven) | | Hub methods are thin — they call existing MediatR commands |

### API Endpoints (REST — supplementary)

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| GET | `/api/v1/notifications` | Any auth | Get notification history (paginated) |
| PATCH | `/api/v1/notifications/:id/read` | Any auth | Mark notification as read |
| GET | `/api/v1/notifications/unread-count` | Any auth | Unread badge count |

### Architecture

```
Domain Event (e.g., OrderStatusChangedEvent)
    ↓
MediatR Notification Handler (in Notifications module)
    ↓
IHubContext<OrderHub> — sends to appropriate SignalR group
    ↓
Client receives event → updates React Query cache
```

Key principle: **Hubs don't contain business logic.** Domain events trigger notifications via MediatR handlers that inject `IHubContext<T>`.

### SignalR Groups Strategy

| Group Name Pattern | Members | Purpose |
|-------------------|---------|---------|
| `order_{orderId}` | Customer + Restaurant + Assigned Rider | Order lifecycle events |
| `restaurant_{restaurantId}` | Restaurant staff | New orders, general notifications |
| `rider_{riderId}` | Individual rider | Delivery offers, assignments |
| `user_{userId}` | Individual user | Personal notifications |
| `admin` | All admins | System-wide events |

### Authentication

```csharp
// In Program.cs hub mapping
app.MapHub<OrderHub>("/hubs/orders")
    .RequireAuthorization();

// JWT from query string (WebSocket can't send headers)
services.AddAuthentication().AddJwtBearer(options =>
{
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});
```

### Redis Backplane (Required for Railway scaling)

```csharp
// If running multiple Railway instances, SignalR needs Redis backplane
services.AddSignalR()
    .AddStackExchangeRedis(redisConnectionString, options =>
    {
        options.Configuration.ChannelPrefix = RedisChannel.Literal("Rally");
    });
```

### Cross-Module Communication

| From Module | To Module | Via | Event |
|------------|-----------|-----|-------|
| Orders | Notifications | Domain Event | OrderPlacedEvent, OrderStatusChangedEvent |
| Delivery | Notifications | Domain Event | DeliveryOfferCreatedEvent, RiderAssignedEvent |
| Delivery | Notifications | Domain Event | RiderLocationUpdatedEvent |

### Frontend Integration (React)

```typescript
// hooks/useSignalR.ts — shared connection manager
// hooks/useOrderUpdates.ts — subscribe to order events, invalidate React Query
// hooks/useRiderLocation.ts — stream rider GPS to map component
// hooks/useDeliveryOffers.ts — rider app: incoming offers with accept/decline
// hooks/useNotifications.ts — notification bell with unread count
```

## 5. Edge Cases & Error Handling

| Scenario | Expected Behavior |
|----------|-------------------|
| WebSocket blocked (corporate firewall) | SignalR falls back to SSE → Long Polling automatically |
| JWT expires during active connection | Client detects 401, refreshes token, reconnects |
| Rider GPS unavailable | Send last known location with `isStale: true` flag |
| Multiple browser tabs open | Each tab gets its own connection, all receive events |
| Server restarts (Railway deploy) | Clients auto-reconnect, rejoin groups |
| Rider sends location but not assigned to order | Ignore location update (no group to broadcast to) |
| Restaurant has no active staff online | Orders queue normally, appear when staff connects |

## 6. Testing Plan

- **Unit tests**: MediatR notification handlers (mock IHubContext, verify correct group/method called)
- **Integration tests**: Hub connection with JWT, group join/leave, event delivery
- **Frontend tests**: useSignalR hook reconnection, React Query cache invalidation on events
- **Load test**: 500 concurrent connections, 100 location updates/sec
- **Manual test**: Open customer + restaurant + rider in 3 browser tabs, walk through full order flow

## 7. Rollout

- [ ] Feature flag: `FEATURE_SIGNALR_ENABLED` (allows graceful disable if issues)
- [ ] Metrics: connection count, reconnection rate, message latency, failed deliveries
- [ ] Rollback: Disable flag → frontend falls back to polling every 10s
- [ ] Redis backplane from day 1 (even single instance) to avoid migration pain later
