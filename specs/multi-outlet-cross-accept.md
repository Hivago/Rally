# Feature Spec: Multi-Outlet Cross-Outlet Order Acceptance

> **Status**: Draft — held for execution, resume next session
> **Priority**: P1 (High) — investor-requested
> **Estimated Effort**: ~1 week backend (see §8 breakdown)
> **Module(s)**: Users, Orders, Delivery, SharedKernel, Host (SignalR)
> **Owner**: Yash
> **Date**: 2026-09-18
> **Requested by**: Balchandra (investor), citing Zomato/Swiggy multi-outlet behavior

---

## 0. Business context (read this first)

Balchandra owns two restaurants on the platform — **Kalp** and **Lord Fork** — same
owner, same physical setup he described, and (per his ask) wants to use **one login**
to see and accept orders for **both**. He's seen this on Zomato/Swiggy and considers it
a simple comparison-and-copy feature.

It is not a toggle. See the WhatsApp explanation already sent to him (summarized): the
system currently treats every restaurant login as a key that opens exactly one door —
this is the same mechanism that guarantees one owner's restaurant can never
accidentally see or act on a *different* owner's orders. The ask is to let one key open
two doors, but only the two that belong to the same owner, and the check that enforces
"only your own doors" has to run correctly on every single action, forever — it doesn't
get to relax just because the owner matches.

**Concrete proof this is already blocked by design, not by accident:**
`VerifyRestaurantOtpCommandHandler.cs:55-57` — when a phone number matches more than
one restaurant account, OTP login is **rejected outright**:
> `"Multiple accounts are linked to this phone number. Please log in with email and password, or contact support."`

This is almost certainly what Balchandra or his staff hit if Kalp and Lord Fork share a
phone number for OTP login today. Fixing this rejection into "log in once, authorized
for both" is the core of this feature.

## 1. Problem Statement

A restaurant owner with multiple outlets under one `RestaurantOwner` cannot act on more
than one outlet's orders per login session. Each `Restaurant` row has its own
credentials (email/password, and/or phone+OTP) and its own JWT, and every
order-related authorization check does an **exact equality** match between the JWT's
identity and the order's `RestaurantId`. There is no way today to be logged in as one
outlet and confirm/reject/manage an order that belongs to a sibling outlet under the
same owner — the only existing workaround is `Owners/SwitchOutlet`, which mints a
*new*, single-outlet JWT one outlet at a time (not simultaneous access).

## 2. User Stories

- As a **restaurant owner with multiple outlets** (e.g. Kalp + Lord Fork, same
  `OwnerId`), I want to log in once and be able to confirm, reject, and manage orders
  for **any** of my outlets, so I don't have to log out and back in per outlet.
- As a **restaurant owner**, I want my dashboard to get real-time new-order alerts for
  **all** my outlets while logged into one session, not just the outlet I logged into.
- As the **business**, I want this to be impossible to exploit across *different*
  owners — Kalp's login must never be able to touch an unrelated restaurant's orders,
  even by URL manipulation.

## 3. Acceptance Criteria

- [ ] OTP login with a phone number shared by two+ *same-owner*, *active* restaurants
      succeeds (previously hard-rejected) and returns a token authorized for all of them.
- [ ] OTP login with a phone number shared by restaurants under **different** owners
      still rejects with the existing "Multiple accounts..." error (genuinely ambiguous
      case — unchanged).
- [ ] A restaurant JWT issued via email/password login, OTP login, outlet-switch, or
      token refresh carries the full set of sibling outlet ids (same owner, active) in
      a new `restaurant_ids` claim.
- [ ] `PUT /api/orders/{orderId}/confirm` succeeds for an order belonging to **any**
      outlet in the caller's `restaurant_ids` set, not just the `sub` outlet.
- [ ] Same for reject, start-preparing, mark-ready-for-pickup, assign-rider,
      mark-customer-pickup, kitchen ticket, order label, get-order-by-id/number.
- [ ] `GET /api/orders/restaurant/{restaurantId}` — **new** ownership check: 403 if
      `restaurantId` is not in the caller's `restaurant_ids`. (Today this has **no
      check at all** — any restaurant-role token can view any restaurant's order list
      by URL. This is a pre-existing gap this feature must close, not widen.)
- [ ] Delivery-side: `GetDeliveryCodes`, `RefreshDeliveryStatus`, `PushOtpsToProvider`
      honor the same expanded set.
- [ ] A dashboard session connected via SignalR receives `NewOrderReceived` /
      `OrderStatusUpdate` events for **all** outlets in `restaurant_ids`, not just the
      login outlet.
- [ ] Edge case: an order belonging to a restaurant **outside** the caller's set still
      returns the existing `NotRestaurantOrder` / 400/403 failure — this must not
      regress for genuinely unrelated restaurants.
- [ ] Edge case: an outlet deactivated (`IsActive = false`) after token issuance is
      excluded from a *newly issued* token's sibling set (existing tokens self-heal on
      the next 15-minute refresh — no forced logout needed).
- [ ] Out of scope this round (see §7): menu editing, restaurant profile/logo/KYC, and
      any merged/combined order feed endpoint. Existing per-outlet endpoints are reused
      as-is (with the ownership-check fix above).

## 4. Technical Design

### 4.1 Design decision: broaden the existing Restaurant JWT (not a new token type)

Rejected alternative: route this through the existing separate "Owner" JWT/policy
(`user_type: owner`, `Owners/*` endpoints) and add it as an accepted identity on every
order endpoint's authorization policy. Rejected because it roughly doubles the surface
area touched (every `RequireAuthorization("Restaurant")` endpoint would need a combined
policy, plus dual-path handling in every handler for "am I an Owner-token caller or a
Restaurant-token caller") for no functional benefit — the outcome the owner wants
(single login, multiple outlets, order actions) is achievable by making a
**Restaurant** token authorize a *set* of restaurant ids instead of exactly one.

Chosen approach: a restaurant-role JWT keeps `user_type: restaurant` and `sub` = the
outlet actually logged into (unchanged — preserves rate-limiting, refresh-token
rotation, and audit "who logged in" semantics, all of which key off `sub` today). A new
claim, `restaurant_ids`, carries the **full set** of outlet ids (including itself) that
this login is authorized to act on — computed from `Restaurant.OwnerId` at token-issue
time. No change to `Program.cs` authorization policies. No new token type.

Staleness: access tokens already expire in 15 minutes
(`JWT_ACCESS_TOKEN_EXPIRY_MINUTES=15`) and are re-minted on refresh
(`RefreshTokenCommandHandler`), which re-resolves the sibling set — so an outlet
added/removed/deactivated under an owner self-heals within 15 minutes with zero extra
invalidation machinery.

### 4.2 JWT claim + token issuance

**File:** `src/Modules/Users/RallyAPI.Users.Infrastructure/Services/JwtProvider.cs`

Current `GenerateRestaurantTokenPair(Restaurant restaurant)` (≈lines 143-158) builds:
```csharp
new(JwtRegisteredClaimNames.Sub, restaurant.Id.ToString()),
new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
new(JwtRegisteredClaimNames.Email, restaurant.Email.Value),
new("name", restaurant.Name),
new("role", "Restaurant"),
new("user_type", "restaurant")
```

Change: add a parameter `IReadOnlyList<Guid> restaurantIds` (must include
`restaurant.Id`) and one claim:
```csharp
new("restaurant_ids", string.Join(",", restaurantIds))
```
Keep `JwtProvider` a pure claims builder — **do not** give it a repository dependency.
Resolve the sibling set in each of the 4 call sites instead:

1. `Restaurants/Commands/Login/LoginRestaurantCommandHandler.cs:118`
2. `Restaurants/Commands/VerifyOtp/VerifyRestaurantOtpCommandHandler.cs:61` — **also
   remove the hard rejection** at lines 55-57 for the same-owner case:
   ```csharp
   // current (lines 48-59):
   var matches = (await _restaurantRepository.GetByPhoneAsync(phoneResult.Value, ct))
       .Where(r => r.IsActive).ToList();
   if (matches.Count == 0) return Result.Failure<...>(Error.Validation("No active restaurant account found for this phone number."));
   if (matches.Count > 1) return Result.Failure<...>(Error.Validation("Multiple accounts are linked to this phone number. Please log in with email and password, or contact support."));
   var restaurant = matches[0];

   // new: only reject if the matches span more than one owner (genuinely ambiguous)
   var distinctOwners = matches.Select(r => r.OwnerId).Distinct().ToList();
   if (distinctOwners.Count > 1)
       return Result.Failure<...>(Error.Validation("Multiple accounts are linked to this phone number. Please log in with email and password, or contact support."));
   var restaurant = matches[0]; // acting/sub outlet — pick deterministically, e.g. earliest CreatedAt
   var restaurantIds = matches.Select(r => r.Id).ToList(); // all same-owner matches
   ```
   Note: if `distinctOwners.Count == 1` but that owner is `null` (two unrelated,
   not-yet-linked restaurants happen to share a phone with no `OwnerId` set), treat as
   ambiguous too — only proceed when `OwnerId` is non-null and shared.
3. `Owners/Commands/SwitchOutlet/SwitchToOutletCommandHandler.cs:46`
4. `Auth/Commands/RefreshToken/RefreshTokenCommandHandler.cs:154`

Shared resolution logic (add as a method on `IRestaurantRepository`, e.g.
`GetSiblingOutletIdsAsync(Restaurant restaurant)`, or inline at each call site):
```csharp
restaurant.OwnerId is null
    ? new[] { restaurant.Id }
    : (await _restaurantRepository.GetByOwnerIdAsync(restaurant.OwnerId.Value))
        .Where(r => r.IsActive)
        .Select(r => r.Id)
        .ToList();
```
`GetByOwnerIdAsync` already exists (`IRestaurantRepository.cs:13`,
`RestaurantRepository.cs:49-56`) and already filters only by `OwnerId` (no `IsActive`
filter baked in — apply it at the call site as above, since `SetAllOutletsAvailability`
and `GetOwnerOutlets` intentionally want inactive outlets visible, but token scoping
should not authorize action on a deactivated outlet).

### 4.3 `ICurrentUserService`

**Interface:** `src/RallyAPI.SharedKernel/Abstractions/ICurrentUserService.cs` (lines 7-21)
Add:
```csharp
IReadOnlyList<Guid> RestaurantIds { get; }
```

**Implementation** — confirmed there is exactly **one** implementation in the entire
codebase, registered once and shared by every module through the SharedKernel
abstraction + Host's shared DI container:
`src/Modules/Orders/RallyAPI.Orders.Infrastructure/Services/CurrentUserService.cs`
(registered at `Orders.Infrastructure/DependencyInjection.cs:106`,
`services.AddScoped<ICurrentUserService, CurrentUserService>();`). This means the
change happens in exactly one file, not several near-duplicates.

```csharp
public IReadOnlyList<Guid> RestaurantIds
{
    get
    {
        var claim = User?.FindFirst("restaurant_ids")?.Value;
        if (string.IsNullOrEmpty(claim))
            return UserId.HasValue ? new[] { UserId.Value } : Array.Empty<Guid>();

        return claim.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => Guid.TryParse(s, out var id) ? id : (Guid?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();
    }
}
```
Fallback to `[UserId]` covers: legacy tokens still in flight during rollout, and
restaurants with `OwnerId == null` (the majority — single-outlet operators).

### 4.4 Ownership checks: exact match → set membership

Identical mechanical change, repeated across every handler currently doing
`order.RestaurantId != command.RestaurantId` (or the switch-expression form
`"Restaurant" when order.RestaurantId == callerId`). Endpoints pass
`currentUser.RestaurantIds` into the command/query (in addition to, not instead of,
`currentUser.UserId` where it's used for audit/logging — keep both). Handlers change
`==`/`!=` to `.Contains(...)`.

**Keep logging the order's actual `RestaurantId` plus the acting outlet's `UserId`
(`sub`)** — don't collapse the distinction between "which outlet does this order
belong to" and "which outlet's login performed this action." Example for
`ConfirmOrderCommandHandler.cs:58`:
```csharp
_logger.LogInformation(
    "Order {OrderNumber} confirmed for restaurant {OrderRestaurantId} via login for outlet {ActingRestaurantId}",
    order.OrderNumber.Value, order.RestaurantId, command.ActingRestaurantId);
```
This audit trail matters — it's the difference between "Kalp accepted its own order"
and "Kalp's login accepted an order for Lord Fork," which the business will want to be
able to see later even though both are now allowed.

**Files to change (Orders.Application):**
| File | Line(s) | Current check |
|---|---|---|
| `Commands/ConfirmOrder/ConfirmOrderCommandHandler.cs` | 39 | `order.RestaurantId != command.RestaurantId` |
| `Commands/RejectOrder/RejectOrderCommandHandler.cs` | 39 | same pattern |
| `Commands/UpdateOrderStatus/UpdateOrderStatusCommandHandler.cs` | 118, 123 | `actorRole == "Restaurant" && order.RestaurantId == actorId` |
| `Commands/AssignRider/AssignRiderCommandHandler.cs` | 40 | `command.AssignedByRole == "Restaurant" && order.RestaurantId == command.AssignedById` |
| `Queries/GetOrderById/GetOrderByIdQueryHandler.cs` | 42 | `"Restaurant" => order.RestaurantId == callerId` |
| `Queries/GetKitchenTicket/GetKitchenTicketQueryHandler.cs` | 44 | same pattern |
| `Queries/GetOrderLabel/GetOrderLabelQueryHandler.cs` | 76 | same pattern |
| `Queries/GetOrderByNumber/GetOrderByNumberQueryHandler.cs` | 42 | same pattern |

**Files to change (Delivery.Application):**
| File | Line(s) | Current check |
|---|---|---|
| `Queries/GetDeliveryCodes/GetDeliveryCodesQueryHandler.cs` | 50 | `"Restaurant" when delivery.RestaurantId == callerId` |
| `Commands/RefreshDeliveryStatus/RefreshDeliveryStatusCommandHandler.cs` | 43 | `delivery.RestaurantId != request.CallerId` (inside `!request.IsAdmin`) |
| `Commands/PushOtpsToProvider/PushOtpsToProviderCommandHandler.cs` | 35 | `!request.IsAdmin && delivery.RestaurantId != request.CallerId` |

**Endpoints to update** (pass `currentUser.RestaurantIds` into the command/query):
- `Orders.Endpoints/OrderEndpoints.cs`: `ConfirmOrder` (~483), `RejectOrder` (~727),
  `StartPreparing` (494), `MarkReadyForPickup` (515), `AssignRider` (536),
  `MarkCustomerPickedUp` (690), `GetKitchenTicket` (324), `GetOrderLabel` (343),
  `GetOrderById` (307), `GetOrderByNumber` (362).
- `Delivery.Endpoints/DeliveryEndpoints.cs`: `GetDeliveryCodes` (72-93).
- `Delivery.Endpoints/AdminDeliveryEndpoints.cs`: `RefreshStatus` (68), `PushOtps`
  (85) — these read claims **raw** via a local `ExtractCaller(httpContext)` helper
  (lines 102-112: `user_type` + `sub`), not `ICurrentUserService`. Update
  `ExtractCaller` to also read `restaurant_ids` and return the set, not just `sub`.

**Required fix, not optional (closes a pre-existing gap):**
`OrderEndpoints.cs:405` `GetRestaurantOrders` → `GetOrdersByRestaurantQueryHandler.cs`
currently has **zero ownership check** — `restaurantId` comes straight from the route
parameter with no comparison to the caller's identity at all (confirmed: the query
handler only calls `_orderRepository.GetActiveOrdersByRestaurantAsync(query.RestaurantId,
...)` / `GetByRestaurantIdAsync(...)`, no auth check; the endpoint method doesn't even
take `ICurrentUserService` today, lines 405-426). Add, in the endpoint before
dispatching:
```csharp
if (!currentUser.RestaurantIds.Contains(restaurantId))
    return Results.Forbid();
```
This is what makes the *existing* single-outlet order-list endpoint safely reusable for
viewing a sibling outlet's orders — per the scope decision in §7, no new merged
endpoint is being built; the frontend will call this same endpoint per outlet id it has
access to.

### 4.5 SignalR — real-time alerts for sibling outlets

**File:** `src/RallyAPI.Host/Hubs/NotificationHub.cs`, `OnConnectedAsync` (≈lines 27-50)

Current behavior joins exactly one group via a switch on `user_type`:
```csharp
var groupName = userType switch
{
    "rider"      => $"rider_{userId.Value}",
    "customer"   => $"customer_{userId.Value}",
    "restaurant" => $"restaurant_{userId.Value}",
    "admin"      => "admin",
    _            => null
};
if (groupName is not null)
    await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
```

Change: for `user_type == "restaurant"`, read the `restaurant_ids` claim (same claim as
§4.2/4.3) and join **one group per id**:
```csharp
if (userType == "restaurant")
{
    var idsClaim = Context.User?.FindFirst("restaurant_ids")?.Value;
    var ids = idsClaim?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? new[] { userId.Value.ToString() };
    foreach (var id in ids)
        await Groups.AddToGroupAsync(Context.ConnectionId, $"restaurant_{id}");
}
else
{
    // existing single-group switch for rider/customer/admin, unchanged
}
```
No change needed to `RestaurantOrderFeedHandler.cs` — it already pushes to
`restaurant_{order.RestaurantId}` per order (confirmed, e.g. `PushAsync` at line 131),
so once the connection is in both outlets' groups, both outlets' events arrive on the
same session automatically.

Why this is required, not nice-to-have: without it, the API-level fix in §4.4 makes
cross-accept *possible* but not *usable* — nothing would visibly alert the dashboard
when a Lord Fork order comes in while logged into Kalp; the owner would have to
manually poll/refresh the sibling outlet's order list to ever notice a new order.

### 4.6 Database

**File:** `Users.Infrastructure/Persistence/Configurations/RestaurantConfiguration.cs`
(current `OwnerId` mapping at lines 180-187 — nullable FK, `WithMany()` with no
back-navigation, `OnDelete(DeleteBehavior.Restrict)`, **no explicit index**)

Add:
```csharp
builder.HasIndex(r => r.OwnerId);
```
`GetByOwnerIdAsync` is now called on every login, every OTP-verify, and every token
refresh for owner-linked restaurants (previously only on outlet-switch and bulk
availability toggles — much colder paths). Generate via EF, per repo convention — do
not hand-write SQL:
```powershell
dotnet ef migrations add AddRestaurantOwnerIdIndex `
  --context UsersDbContext `
  --project src/Modules/Users/RallyAPI.Users.Infrastructure `
  --startup-project src/RallyAPI.Host
```

## 5. Cross-Module Communication

No new cross-module events or contracts needed. This feature is entirely within the
Users module (token issuance) + SharedKernel (`ICurrentUserService` interface) +
Orders/Delivery modules (consuming the expanded claim via the existing shared
`ICurrentUserService` implementation) + Host (SignalR hub). Catalog module is
untouched — confirmed zero `OwnerId` references anywhere in Catalog, and this feature
does not change that.

## 6. Edge Cases & Error Handling

| Scenario | Expected Behavior |
|---|---|
| Two restaurants share a phone number, same `OwnerId`, both active | OTP login succeeds, token authorizes both |
| Two restaurants share a phone number, different (or one null) `OwnerId` | OTP login still rejected — existing "Multiple accounts..." error, unchanged |
| Owner's outlet B is deactivated after Kalp's token was issued | Existing token still lists B until its next refresh (≤15 min); after refresh, B is dropped from `restaurant_ids` |
| Kalp's login tries to confirm an order for an unrelated restaurant (different owner) | Unchanged: `NotRestaurantOrder` failure |
| Kalp's login calls `GET /api/orders/restaurant/{lordForkId}` | Now succeeds (was previously unchecked-and-always-allowed for *any* id — now correctly scoped) |
| Legacy/in-flight access token issued before this change lands (no `restaurant_ids` claim) | `ICurrentUserService.RestaurantIds` falls back to `[UserId]` — behaves exactly like today until the token naturally expires/refreshes |
| Single-outlet restaurant (`OwnerId == null`, the common case) | `restaurant_ids` claim is just `[restaurant.Id]` — behaviorally identical to today |
| Rate limiting | Unaffected — partition key is `sub` (Program.cs ≈line 294), which stays the single acting outlet id, so per-login rate limits are unchanged |

## 7. Explicitly Out of Scope This Round (confirmed with user 2026-09-18)

Deferred to a future phase — do **not** build these now:

- **Catalog module cross-outlet menu editing** (create/update/delete menu, menu items,
  option groups, availability toggles — ~12 handlers, e.g.
  `Catalog.Application/MenuItems/Commands/*`, `Menus/Commands/*`). Rationale for
  deferral: Catalog currently has **zero** `OwnerId` awareness (confirmed via repo-wide
  grep — zero hits), and its endpoints read the JWT claim raw
  (`Guid.Parse(user.FindFirstValue("sub")!)`) in every file instead of going through
  `ICurrentUserService` like Orders does. Before extending cross-outlet access there,
  those ~12-14 files should first be standardized onto `ICurrentUserService` (small,
  mechanical refactor), then get the same `.Contains()` swap as §4.4. Estimated ~2-3
  days for a mid-level developer, **after** this Orders/Delivery version has shipped
  and the pattern is proven — not a rearchitecture, just repetition of an
  already-documented pattern into a module that hasn't adopted it yet.
- **Restaurant profile/logo/KYC cross-outlet editing**
  (`Users.Endpoints/Restaurants/UploadLogo/*`, `UpdateProfile`, `UpdateBasics`,
  `UpdateHours`, etc.) — same deferral rationale, smaller surface (2-3 handlers).
- **Merged/combined order feed endpoint** — explicitly declined by the user for this
  round ("keep other things same... I only want cross order acceptance for now"). The
  existing single-outlet `GetOrdersByRestaurant` endpoint is reused as-is, just with
  the ownership-check fix in §4.4. A true unified inbox (one call returns orders from
  all outlets, tagged per-outlet) remains a reasonable Phase 2/3 idea, noted here for
  when it's revisited — it would need a new repository method (`GetByRestaurantIdsAsync`
  taking a collection) and a new query, since `IOrderRepository` today only has
  single-`Guid` overloads.
- **`RestaurantStatsService`** — confirmed strictly per-restaurant (`GetStatsAsync(Guid
  restaurantId, ...)`, every query filters `o.RestaurantId == restaurantId`). No change
  needed; a combined owner-level dashboard would be new, separate scope.
- **Payout/commission subsystem** — confirmed this is **already** owner-keyed, not
  restaurant-keyed (`PayoutLedger.OwnerId`, `Payout.OwnerId`, every payout/GST/TDS
  query and export groups by `OwnerId` already). No change needed here at all — it
  already does what this feature is adding elsewhere.

## 8. Testing Plan

- **Handler unit tests**:
  - `tests/Modules/Orders/RallyAPI.Orders.Application.Tests/ConfirmOrderCommandHandlerTests.cs`
    — update `Handle_WhenRestaurantDoesNotOwnOrder_ShouldReturnFailure` for the new
    command shape (set of ids); add
    `Handle_WhenOrderBelongsToSiblingOutlet_ShouldSucceed`.
  - Mirror the same add/update for `RejectOrderCommandHandler`,
    `UpdateOrderStatusCommandHandler`, `AssignRiderCommandHandler`,
    `GetOrderByIdQueryHandler`, `GetKitchenTicketQueryHandler` (existing test file
    `GetKitchenTicketQueryHandlerTests.cs` already covers the `RestaurantId` branch,
    extend it), `GetOrderLabelQueryHandler`, `GetOrderByNumberQueryHandler`.
  - `GetDeliveryCodesQueryHandler`, `RefreshDeliveryStatusCommandHandler`,
    `PushOtpsToProviderCommandHandler` — same pattern, Delivery.Application.Tests.
  - New test in Users.Application.Tests for `VerifyRestaurantOtpCommandHandler`:
    same-owner multi-match now succeeds; different-owner multi-match still rejects.
- **Integration tests** (`tests/RallyAPI.Integration.Tests`):
  - `Flows/OrderFlowTests.cs`: keep `ConfirmOrder_ByWrongRestaurant_Returns400` green
    (genuinely unrelated restaurant); add
    `ConfirmOrder_BySameOwnerDifferentOutlet_Succeeds`.
  - `Infrastructure/TestJwtHelper.cs` (`CreateRestaurantToken` at line 27) — add an
    overload or new helper `CreateRestaurantTokenForOwner(Guid actingOutletId,
    IReadOnlyList<Guid> siblingIds)` so tests can mint a multi-outlet token. No
    `CreateOwnerToken` helper exists today either — not needed for this scope since
    we're not touching the Owner-token path.
  - New test: `GetRestaurantOrders_ForSiblingOutlet_Succeeds` and
    `GetRestaurantOrders_ForUnrelatedOutlet_Returns403` (closes the pre-existing gap).
- **Manual smoke test** (`dotnet run --project src/RallyAPI.Host`):
  1. Seed/verify two restaurants sharing one `OwnerId` and one phone number (or use
     Kalp/Lord Fork directly if already seeded with a shared owner).
  2. OTP-login with that shared phone → previously rejected → should now succeed.
  3. Decode the returned access token, confirm `restaurant_ids` contains both outlet ids.
  4. `PUT /api/orders/{orderId}/confirm` for an order belonging to the *other* outlet →
     should return 200 (was `NotRestaurantOrder` failure before this change).
  5. `GET /api/orders/restaurant/{otherOutletId}` → succeeds for the sibling, rejected
     (403) for an unrelated restaurant id.
  6. Two SignalR-connected sessions (or one browser dev-tools check) → confirm a
     `NewOrderReceived` event for the sibling outlet arrives on the session logged into
     the other outlet.

## 9. Rollout

- No feature flag needed — this is a security-boundary correctness fix layered onto an
  additive claim; behavior for single-outlet restaurants (the majority) is unchanged
  (`restaurant_ids` degrades to `[UserId]`), and multi-outlet behavior only activates
  for `Restaurant` rows that already have a non-null `OwnerId` shared with another
  active restaurant.
- **Security review required before merge** — this changes an authorization boundary
  (exact-match → set-membership). Get a second pair of eyes on §4.2-§4.4 specifically;
  a wrong implementation here (e.g. accidentally trusting a client-supplied restaurant
  id list instead of the server-computed one) is a cross-owner data leak, not a
  cosmetic bug.
- Metrics to watch post-deploy: rate of `NotRestaurantOrder` failures (should only ever
  fire for genuinely unrelated restaurants now — a spike would indicate the sibling-set
  resolution is wrong), and rate of 403s on `GetRestaurantOrders` (should be near-zero
  in normal operation once frontend is updated, since it previously had no check to
  compare against).
- Rollback plan: revert the JWT claim addition and the `.Contains()` handler changes
  together (they're coupled — reverting one without the other either breaks
  single-outlet logins or silently no-ops the cross-access check). The DB index
  (`AddRestaurantOwnerIdIndex`) is safe to leave in place either way.

---

## Implementation Notes (updated during build)

### Files Created/Modified
Implemented per §4.2-§4.6 on branch `feat/multi-outlet-cross-accept` (off `staging`).
- JWT/token issuance: `IJwtProvider`/`JwtProvider.GenerateRestaurantTokenPair` now takes
  `restaurantIds`; all 4 call sites updated (Login, VerifyOtp, SwitchOutlet, RefreshToken).
  `IRestaurantRepository`/`RestaurantRepository` gained `GetSiblingOutletIdsAsync`.
- `VerifyRestaurantOtpCommandHandler`: same-owner multi-match now succeeds (only rejects
  when matches span >1 owner, or share no owner at all); handler made `public` (was
  `internal`) so it could be unit tested, matching every other tested handler's
  accessibility in this codebase.
- `ICurrentUserService.RestaurantIds` + `CurrentUserService` implementation (Orders
  module's shared implementation, per §4.3).
- Ownership checks swapped `==`/`!=` → `.Contains()` in: ConfirmOrder, RejectOrder,
  UpdateOrderStatus, AssignRider, GetOrderById, GetKitchenTicket, GetOrderLabel,
  GetOrderByNumber (Orders.Application) and GetDeliveryCodes, RefreshDeliveryStatus,
  PushOtpsToProvider (Delivery.Application) + their endpoints. `GetRestaurantOrders`
  403 gap closed per §4.4's "required fix, not optional."
- `DeliveryRequest.RestaurantId` is `Guid?` — ownership checks there use
  `is not Guid x || !ids.Contains(x)` rather than a direct `.Contains()`.
- SignalR `NotificationHub.OnConnectedAsync`: restaurant callers now join one
  `restaurant_{id}` group per outlet in `restaurant_ids`.
- DB: `idx_restaurants_owner_id` — migration `20260929084926_AddRestaurantOwnerIdIndex`.
  **Found real pre-existing drift while applying locally**: the model snapshot already
  believed a by-convention FK index (`IX_restaurants_owner_id`) existed (same class of
  issue as `20260422000000_EnsureRestaurantOwnersTable`'s recovery migration), so the
  auto-generated migration was a `RenameIndex` that failed against a local DB that never
  actually had that index. Rewrote it as idempotent raw SQL (rename-if-present, else
  create) — verified applied locally, `idx_restaurants_owner_id` now exists.
- Tests: updated `ConfirmOrderCommandHandlerTests` (command shape) + added
  `Handle_WhenOrderBelongsToSiblingOutlet_ShouldSucceed`; added the same sibling-outlet
  test to `GetKitchenTicketQueryHandlerTests`; added new
  `VerifyRestaurantOtpCommandHandlerTests` (5 cases covering single-match/no-owner,
  same-owner multi-match, different-owner rejection, no-shared-owner rejection,
  no-active-match). All three affected suites green: 78 Orders + 94 Delivery + 25 Users.
- **Not done this round** (deferred, not required for the core flow): `GetOrderLabel`/
  `GetOrderByNumber` sibling-outlet unit tests, `Delivery.Application.Tests` sibling
  coverage, `TestJwtHelper.CreateRestaurantTokenForOwner` + integration tests
  (`OrderFlowTests`, `GetRestaurantOrders_ForSiblingOutlet_Succeeds`/`_Returns403`) — the
  integration suite has pre-existing unrelated rot (~20 failing tests, idempotency-key +
  rate-limit related, not this feature).

### Decisions Made
- 2026-09-18: Scope locked to order actions only this round — no Catalog/profile
  cross-access, no merged order feed. See §7.
- 2026-09-18: Chose to broaden the existing Restaurant JWT (`restaurant_ids` claim)
  over introducing a dual Owner/Restaurant auth path on every endpoint. See §4.1.
- 2026-09-29: Used a nullable-with-`?? new[] { callerId }` fallback pattern on read-side
  queries (Get*) so existing 3-arg call sites/tests keep compiling unchanged; used a
  required non-nullable `RestaurantIds` param on the primary write path (`ConfirmOrder`)
  since its only caller is the endpoint; used an `effectiveRestaurantIds = ids.Count > 0
  ? ids : [actorId]` fallback inside the handler for `UpdateOrderStatus`/`AssignRider`/
  `RejectOrder` after discovering their existing unit tests construct the command
  directly without the new field and would otherwise silently fail auth.

### Open Questions
- None blocking. Security review (§9) and the deferred test coverage above are the
  remaining pre-merge items.

### Addendum (2026-09-29): Admin-controlled on/off toggle

Follow-up requirement: cross-outlet accept must be **off by default** and controllable
per owner, not unconditional for every owner sharing an outlet pair.

- `RestaurantOwner.CrossOutletAcceptEnabled` (bool, default `false`) — new domain
  property + `SetCrossOutletAcceptEnabled(bool)` method. Migration
  `20260929094057_AddCrossOutletAcceptToggle`.
- `RestaurantRepository.GetSiblingOutletIdsAsync` now checks the owner's flag before
  returning siblings — returns `[self]` if off, even when `OwnerId` is set. This gates
  Login, SwitchOutlet, and RefreshToken (all three already route through this method).
- `VerifyRestaurantOtpCommandHandler` (OTP login) has its own inline sibling-resolution
  that doesn't call `GetSiblingOutletIdsAsync` — gated separately: a same-owner
  multi-match now also requires the owner's flag to be on, otherwise it falls back to
  the original "Multiple accounts..." ambiguous rejection.
- Admin-only toggle: `PUT /api/admin/owners/{ownerId}/cross-outlet-accept` (body
  `{ enabled: bool }`) — `SetOwnerCrossOutletAcceptCommand`, Support role blocked (same
  restriction as `ResetOwnerPasswordCommand`). Idempotent (setting to the current value
  is a no-op success, not a validation error, unlike the `Activate`/`Deactivate`
  toggle-style convention elsewhere on this entity).
- `GET /api/admin/owners` (`ListOwnersQuery`) now also returns `CrossOutletAcceptEnabled`
  per owner so the admin panel can show current state without a second call.
- Tests: `VerifyRestaurantOtpCommandHandlerTests` updated for the new
  `IRestaurantOwnerRepository` dependency; added
  `Handle_MultipleMatchesSameOwnerCrossAcceptEnabled_ShouldSucceedWithFullOutletSet` and
  `Handle_MultipleMatchesSameOwnerCrossAcceptDisabled_ShouldReturnFailure`. All 26
  Users.Application tests + 78 Orders.Application tests green; migration applied
  locally, verified column exists, startup smoke test passed.
- **Not covered by this addendum**: no test directly exercises
  `GetSiblingOutletIdsAsync`'s flag-gating for the Login/SwitchOutlet/RefreshToken path
  (only the OTP path has handler-level tests) — would need either an integration test
  against a real DB or exposing the repository behind a fake in a unit test.
