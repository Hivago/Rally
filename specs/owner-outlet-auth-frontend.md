# Frontend Hand-off — Owner Access Inside an Outlet Session

Backend branch: `fix/owner-refresh-token` (not yet deployed). Read together with
`specs/auth-silent-refresh-frontend.md`.

## TL;DR
1. **Owner sessions can now refresh.** Before, an owner got a 401 from `/api/auth/refresh` as soon as the
   15-min access token expired. The standard silent-refresh interceptor now works for owners too.
   Remove any owner-specific workaround (forced re-login, longer-lived hacks).
2. **An owner who switches into an outlet keeps owner access.** The outlet token now carries
   `owner_id` and `owner_access: "true"`, so the owner can call owner-level endpoints (bank, password,
   availability, time-off) from inside the outlet view, with one token.
3. **A restaurant that logs in directly (email/password or OTP) never gets owner access.** Hide owner-only
   UI for those sessions. The server enforces it with 403.

No request or response shapes changed. Only the claims inside the access token changed.

## Token claims you can read (decode the JWT payload, do not trust it for security)
| Session | `user_type` | `sub` | `owner_id` | `owner_access` |
|---|---|---|---|---|
| Owner login | `owner` | owner id | absent | absent |
| Owner switched into outlet | `restaurant` | outlet id | owner id | `"true"` |
| Direct restaurant login | `restaurant` | outlet id | absent | absent |

Claims are re-issued on every refresh. Re-decode after each refresh instead of caching them.

```ts
const claims = decodeJwt(accessToken);
const isOwnerSession    = claims.user_type === 'owner';
const isOwnerInOutlet   = claims.user_type === 'restaurant' && claims.owner_access === 'true';
const canUseOwnerFeatures = isOwnerSession || isOwnerInOutlet;
// For API calls that need the owner id: claims.owner_id ?? claims.sub (only when isOwnerSession)
```

## What to change in the UI
1. **Gate owner-only screens on `canUseOwnerFeatures`.** Bank details, change password, "all outlets"
   availability, time-off, and the outlet switcher. Hide them for direct restaurant logins.
2. **After `POST /api/owners/outlets/{restaurantId}/switch`**, replace BOTH stored tokens with the response
   (`accessToken`, `refreshToken`) and treat that as the active session. You no longer need to keep the
   separate owner token around for owner-level calls.
3. **Switching to another outlet from inside an outlet session works.** Call the same `/switch` endpoint with
   the current outlet token. No need to go back to an owner login first.
4. **Keep storing the rotated refresh token on every refresh** (unchanged). Outlet-session refresh now returns
   a token that still has `owner_access`.
5. **Handle `403` on owner endpoints** with a clear message ("Owner access required"), not a logout. A 403
   means the session is valid but not an owner session. A `401` that survives a refresh means log in again.
6. **Owner deactivated or outlet deactivated** → `/auth/refresh` returns 401 → send to login (already the rule).

## Endpoints reachable with the outlet token (`owner_access`)
| Method | Path |
|---|---|
| GET | `/api/owners/me` |
| GET | `/api/owners/me/outlets` |
| PUT | `/api/owners/me/bank` |
| PATCH | `/api/owners/me/password` |
| PUT | `/api/owners/me/outlets/availability` (all outlets) |
| PUT | `/api/owners/me/outlets/{restaurantId}/availability` |
| GET / POST | `/api/owners/me/outlets/{restaurantId}/time-off` |
| POST | `/api/owners/me/outlets/{restaurantId}/time-off/quick-pause` |
| DELETE | `/api/owners/me/outlets/{restaurantId}/time-off/{timeOffId}` |
| POST | `/api/owners/outlets/{restaurantId}/switch` |

Existing restaurant endpoints (orders, menu, `GET /api/restaurants/me/outlets`) keep working with the same token.

## Rollout note (important)
Sessions created **before** this deploys are plain restaurant sessions with no `owner_access`. For those
owners, owner endpoints return 403 until they sign in as owner again and re-select the outlet.
Show a one-time message on 403: "Sign in again as owner to use owner features." Nothing else breaks.

## Test checklist
- [ ] Owner logs in, waits for the access token to expire, a request silently refreshes. No logout.
- [ ] Owner switches into an outlet: owner menu visible, bank/password/time-off calls succeed.
- [ ] Refresh inside the outlet session: owner menu still visible afterwards.
- [ ] Owner switches to a second outlet from inside the first. Works, owner menu still visible.
- [ ] Direct restaurant login: owner menu hidden; forcing an owner call returns 403 and shows the message.
- [ ] Owner deactivated by admin: next refresh sends the user to login.
