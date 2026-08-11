# Restaurant Onboarding Form — Notes

> Last updated: 2026-08-04.

## What this is

A public, standalone single-file page — lives OUTSIDE this repo, at
`C:\Users\vishw\source\repos\hivago-partner-onboarding\index.html` (not yet its own git repo;
create one when ready to deploy) — where a restaurant can submit their details to apply to
join Hivago. Submission creates a `Pending` record only. **Nothing is live.** No owner
account, no restaurant listing, no login credentials are created by this form. A human
reviews every submission.

Because the page is a **different origin** from the API, it calls the API cross-origin — see
"CORS" below. The page auto-switches its API target based on `location.hostname`: `localhost`
→ `http://localhost:5023`, anything else → `https://api.hivago.in` (update that fallback if
the real API domain differs).

## CORS

Two places in `src/RallyAPI.Host/Program.cs` need the page's origin:
- **Local testing**: `http://localhost:5500` / `http://127.0.0.1:5500` are already allowlisted
  (VS Code Live Server's default port) — see the "Testing locally" section below.
- **Production**: once this page has a real hosted domain, add it to `productionOrigins` in
  `Program.cs` (there's a `TODO` comment marking exactly where) and redeploy the API — until
  then, a production-hosted copy of this page cannot call the API (browsers will block it).

## Testing locally

1. Run RallyAPI locally (Docker Postgres/Redis + `dotnet run --project src/RallyAPI.Host`,
   with `Encryption__Key` set — see below). Confirm it's on `http://localhost:5023`.
2. Serve `index.html` on port 5500 — easiest via the VS Code "Live Server" extension
   (right-click → "Open with Live Server"), or any static server pinned to that port, e.g.:
   ```
   npx serve -l 5500 C:\Users\vishw\source\repos\hivago-partner-onboarding
   ```
3. Open `http://localhost:5500` in a browser and submit the form — it'll call
   `http://localhost:5023/api/restaurant-onboarding` directly. Opening the file directly via
   `file://` will NOT work — browsers treat that as a null origin and the API's CORS policy
   will reject it; it must be served over `http://`.

## Required env var — the app won't start without it

```
Encryption__Key=<base64 32-byte key>
```
Generate with `openssl rand -base64 32`. This encrypts bank account number, PAN, and GST at
rest (AES-256-GCM, `RallyAPI.SharedKernel.Security.AesGcmFieldEncryptionService`). Set this on
Railway before deploying — the service throws on startup if it's missing or the wrong length,
by design, rather than silently storing plaintext financial data.

**Losing this key means losing the ability to ever decrypt existing submissions** — back it up
somewhere durable (password manager / secrets vault), not just the Railway env var UI.

## The review flow

1. Restaurant submits via the public form → `Pending`.
2. Any admin can list/view applications (`GET /api/admin/restaurant-onboarding`,
   `GET .../restaurant-onboarding/{id}`) — but the raw bank account number, PAN, and GST are
   only decrypted and returned **if the caller is `AdminRole.SuperAdmin`**. Everyone else sees
   masked values (`•••• 9012`) only — the API never even calls decrypt for a non-Super-Admin
   viewer, not just a display-layer hide.
3. Any admin can approve or reject (`POST .../approve`, `POST .../reject` — reject requires a
   reason, ≥1 non-empty string). This is deliberately **not** Super-Admin-gated — approving
   doesn't create an account or move money, it just records a decision.
4. **Approving does NOT create the live owner/restaurant account.** That's a separate manual
   step: someone with the decrypted details uses the existing admin flows
   (`CreateOwnerCommand`, `CreateAdminPanelRestaurantCommand`) to actually set up the account,
   after independently verifying the documents (this form has no document/photo upload — it's
   text fields only, v1 scope). Treat "Approved" as "cleared to onboard," not "onboarded."

## Anti-abuse measures already in place

- Rate limit: 5 submissions/minute per IP (`restaurant-onboarding` policy in `Program.cs`,
  stricter than the 30/min `lead-capture` policy since this collects financial PII).
- Honeypot field (`website`, hidden via CSS) — any bot that fills every input trips it; the
  request gets a fake success response and creates nothing. Verified live: a filled honeypot
  returns `201` but no row lands in the database.
- Duplicate-pending guard — a second submission with the same phone or email while a prior one
  is still `Pending` is rejected with `409 Conflict`, not silently double-queued.
- No CAPTCHA (would need a third-party key we don't have provisioned) — the rate limit +
  honeypot are the current defenses. Worth adding CAPTCHA if spam becomes a real problem.

## What's NOT built (v1 scope boundary)

- No document/photo upload (FSSAI certificate scan, menu photos, etc.) — text fields only.
- No email/SMS confirmation to the applicant on submit or on approve/reject.
- No automatic account creation on approval (see above — intentional).
- No admin-panel UI for this yet — same situation as the ICICI payout work, someone needs to
  build list/detail/approve/reject screens. The endpoints are ready; see the API shapes in the
  code (`RestaurantOnboardingApplicationSummaryDto`, `RestaurantOnboardingApplicationDetailDto`).
