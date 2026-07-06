# Frontend Hand-off — Silent Token Refresh (replace the "Stay signed in?" prompt)

## TL;DR
Remove the 10-minute "Stay signed in?" modal. It's an idle-timeout pattern meant for
banking apps and is the wrong fit for a food-delivery app — it logs hungry users out
mid-browse/mid-checkout. The backend already supports invisible, long-lived sessions;
the frontend just needs a **silent refresh interceptor**.

## How the backend session model works
- **Access token**: 15 min, RS256 JWT. Sent as `Authorization: Bearer <token>`.
- **Refresh token**: 60-day **sliding** window. A *new* refresh token is issued on every
  use, each with a fresh 60-day life. An active user effectively never needs to re-login.
- **Rotation + theft detection**: reusing an already-rotated refresh token normally revokes
  the whole session. There is now a **30-second grace window** so concurrent refreshes
  (multiple tabs / parallel requests) are NOT treated as theft.

## What to build
On the API client (axios/fetch wrapper):

1. **On `401`** → call `POST /api/auth/refresh` with the stored refresh token, then retry
   the original request with the new access token.
2. **Single-flight the refresh**: if several requests 401 at once, run **one** refresh and
   queue the others behind it. Do not fire N parallel `/auth/refresh` calls — even with the
   30s grace window, single-flight is the correct pattern and avoids edge cases.
3. **Persist the refresh token** in `localStorage` (or an httpOnly Secure cookie) so a page
   reload or reopened tab keeps the session.
4. **Only redirect to login** when `/auth/refresh` itself fails (refresh token expired/revoked
   → 60 days idle, or explicit logout). Never on a normal access-token 401.
5. **Delete the "Stay signed in?" modal and its 10-min timer entirely.** If you still want a
   "Remember me" concept: checked = persist refresh token (localStorage/cookie);
   unchecked = keep it in memory only (session ends when the tab closes). No timer, no prompt.

## Endpoint
`POST /api/auth/refresh`
```json
// request
{ "refreshToken": "<stored refresh token>" }
// response
{ "accessToken": "...", "refreshToken": "...", "accessTokenExpiresAt": "2026-07-06T10:30:00Z" }
```
Store the returned `refreshToken` (it rotates every call) and use `accessTokenExpiresAt` if you
want to refresh proactively ~1 min before expiry instead of reactively on 401.

## Reference interceptor shape (axios)
```ts
let refreshing: Promise<string> | null = null;

api.interceptors.response.use(undefined, async (error) => {
  const original = error.config;
  if (error.response?.status !== 401 || original._retried) throw error;
  original._retried = true;

  refreshing ??= doRefresh().finally(() => { refreshing = null; }); // single-flight
  try {
    const newAccess = await refreshing;
    original.headers.Authorization = `Bearer ${newAccess}`;
    return api(original);
  } catch {
    redirectToLogin();
    throw error;
  }
});
```
