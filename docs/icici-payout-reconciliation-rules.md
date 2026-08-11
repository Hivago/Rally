# ICICI Payout Export/Reconciliation — Rules for Anyone Touching This Code

> Handoff doc. Read this before changing anything under the file map below.
> Full design/rationale: `specs/icici-manual-payout-export.md` (read section 4a — the trust
> boundary — before touching reconcile or manual-resolve).
> Financial calculation background (GST/TDS/commission): `docs/restaurant-payout-system.md`.
> Last updated: 2026-08-04.

---

## What this system does

Restaurants and riders are paid by hand via ICICI's Corporate Internet Banking bulk-transfer
portal — **not** through an automated gateway (PayU quoted ₹35k; we built the calculation +
reconciliation software instead and kept the actual money movement manual and bank-side).

Two parallel flows, one for restaurants (Orders module) and one for riders (Users module),
identical in shape:

```
Pending ──export──► Processing ──reconcile/manual-resolve──► Paid | Failed
   ▲                                                              │
   └──────────────────────── retry (Failed only) ─────────────────┘
```

- **Export**: `Pending` payouts → bulk-transfer `.xlsx` → flip to `Processing` (atomic, so a
  payout can never be exported twice).
- **Reconcile**: upload ICICI's Consolidated Status Report → match rows to `Processing`
  payouts → `Paid` (with UTR) or `Failed` (with reason).
- **Manual-resolve**: escape hatch for a `Processing` payout the automatic matcher can't
  confidently resolve.

---

## File map

| Concern | Restaurant (Orders module) | Rider (Users module) |
|---|---|---|
| Payout entity | `src/Modules/Orders/RallyAPI.Orders.Domain/Entities/Payout.cs` | `src/Modules/Users/RallyAPI.Users.Domain/Entities/RiderPayoutLedger.cs` |
| Export batch entity | `.../Orders.Domain/Entities/RestaurantPayoutExportBatch.cs` | `.../Users.Domain/Entities/RiderPayoutExportBatch.cs` |
| Repository interface | `.../Orders.Domain/Repositories/IPayoutRepository.cs` | `.../Users.Application/Abstractions/IRiderPayoutLedgerRepository.cs` |
| Repository impl | `.../Orders.Infrastructure/Repositories/PayoutRepository.cs` | `.../Users.Infrastructure/Persistence/Repositories/RiderPayoutLedgerRepository.cs` |
| Export command | `.../Orders.Application/Commands/GenerateRestaurantPayoutExport/` | `.../Users.Application/Admins/Commands/GenerateRiderPayoutExport/` |
| Reconcile command | `.../Orders.Application/Commands/ReconcileRestaurantPayouts/` | `.../Users.Application/Admins/Commands/ReconcileRiderPayouts/` |
| Manual-resolve command | `.../Orders.Application/Commands/ManuallyResolveRestaurantPayout/` | `.../Users.Application/Admins/Commands/ManuallyResolveRiderPayout/` |
| Stale-report query | `.../Orders.Application/Queries/GetStaleRestaurantPayouts/` | `.../Users.Application/Admins/Queries/GetStaleRiderPayouts/` |
| Batch-listing query | `.../Orders.Application/Queries/ListRestaurantPayoutExportBatches/` | `.../Users.Application/Admins/Queries/ListRiderPayoutExportBatches/` |
| All endpoints (both flows) | `src/Modules/Users/RallyAPI.Users.Endpoints/Admins/*Payout*.cs` (yes — restaurant endpoints live in `Users.Endpoints`, the admin-panel composition root; this is intentional, see below) | same directory |
| Shared xlsx writer/parser | `src/RallyAPI.SharedKernel/Utilities/Payouts/IciciBulkTransferExcelWriter.cs` (export) and `IciciReconciliationParser.cs` (import) — **one copy, used by both modules** | same |

**Why restaurant endpoints live in `Users.Endpoints`**: the admin panel is a single composition
root that dispatches MediatR commands into whichever module owns the aggregate. This is the
established pattern (`ExportRestaurantPayouts.cs` already did this before this feature existed)
— don't "fix" it by moving files into `Orders.Endpoints`, that would break the SuperAdmin-check
pattern below.

---

## Non-negotiable rules

1. **Never let `MarkPaid` be reachable without a real bank-issued reference.** There is no
   "admin types in a UTR and clicks pay" button — that was deliberately removed (see
   `docs/restaurant-payout-system.md` §API Endpoints, item 8). `Paid` is only ever reached via
   `ReconcileXPayoutsCommandHandler` (parsed from the bank's own report) or
   `ManuallyResolveXPayoutCommandHandler` (human-asserted, logged, justified). If you're adding
   a new path to `Paid`, it needs the same UTR-format check + duplicate-UTR check + audit log —
   don't skip them because "it's just for testing."

2. **Reconcile/manual-resolve are Super-Admin-only, checked in the endpoint, not by ASP.NET
   policy.** There's only one blanket `"Admin"` authorization policy in this codebase — role
   granularity (`AdminRole.SuperAdmin`) is checked by loading the `Admin` row via
   `IAdminRepository.GetByIdAsync` inside the endpoint handler (see
   `ReconcileRestaurantPayouts.cs` / `ManuallyResolveRestaurantPayout.cs`). If you add a new
   Paid-marking endpoint, copy that exact check — don't assume `.RequireAuthorization("Admin")`
   alone is enough.

3. **Matching key is `(BankAccountNumber, BankIfscCode, Amount)`, scoped to the target export
   batch.** Ambiguous (>1 match) or unmatched rows are reported, **never guessed**. If you're
   tempted to "just pick the first match" — don't. That's exactly the silent-mispay risk this
   design avoids.

4. **Restaurant `Payout` stores bank details at export time; `RiderPayoutLedger` does not.**
   `Payout.BankAccountNumber`/`BankIfscCode` are set once, at creation (mirrors the owner's bank
   details at that moment). `RiderPayoutLedger` has no such fields — the rider reconcile handler
   re-fetches **live** bank details via `IRiderRepository.GetBankDetailsByIdsAsync` every time.
   This is an intentional asymmetry, not a bug — but it means if a rider changes their bank
   account between export and reconcile, that payout's row won't match anything and needs manual
   resolution. Don't "fix" this by adding stored bank fields to `RiderPayoutLedger` without first
   checking whether that changes the live-lookup guarantee the export side already promises.

5. **Export is read-then-flip, atomic, only-Pending.** `GetPendingByPeriodAsync`/
   `GetPendingByCycleAsync` select only `Pending` rows; `MarkProcessing()` throws if the payout
   isn't `Pending`. This is what makes double-export impossible. Never add a path that flips
   `Processing`/`Paid`/`Failed`/`OnHold` back to a re-exportable state except `MarkRetry()`
   (`Failed → Pending`, explicit admin action only).

6. **A batch closes (`Generated → Reconciled`) only when every payout in it is resolved.** Don't
   call `batch.MarkReconciled(...)` unconditionally after a reconcile/manual-resolve — check
   `siblings.Any(p => p.Status == Processing)` first (see either handler for the pattern). A
   batch can legitimately take multiple reconcile uploads (partial bank reports) before it closes.

7. **Reconciliation is idempotent — re-uploading a file with already-`Paid`/`Failed` rows must
   be a no-op for those rows**, reported as `alreadyResolvedSkipped`, never reapplied. Verified
   live (see the smoke-test results below) — don't regress this while refactoring the matching
   loop.

---

## Known gotchas (already bitten us once — don't repeat)

- **`ReconciliationFileHash` / `GeneratedFileHash` columns are `HasMaxLength(64)`** — sized for
  a real SHA-256 hex digest (64 chars). The manual-resolve handlers stamp a synthetic
  `"MANUAL-" + 56-char-hash` marker (63 chars) instead of a real file hash when a batch closes
  without a reconciliation file. **If you change that marker format, it must still fit in 64
  chars** — this exact bug (an 80-char marker) was caught only by running a real Postgres insert
  during the live smoke test on 2026-08-04, not by unit tests (NSubstitute mocks don't enforce
  column lengths). Any new field you add to these entities that gets set from computed/hashed
  strings needs the same check against its `HasMaxLength(...)`.

- **`UseXminAsConcurrencyToken()` shows as obsolete (CS0618) on `Payout` and
  `RiderPayoutLedger`.** It is used **deliberately** — Postgres' built-in `xmin` system column
  gives optimistic concurrency without a migration, which is what prevents two admins exporting
  the same period simultaneously from both winning. **Do not "fix" the warning** by removing it
  or swapping to `[Timestamp]`/`IsRowVersion()` without understanding the two-admin-race
  scenario in `PayoutConfiguration.cs`'s comments first.

- **Real ICICI UTRs are 16 characters** (e.g. `IN42619755781929`), not the 22 originally guessed
  in the spec. The parser/handlers validate length ≥ 10 + alphanumeric rather than an exact
  length, specifically so this doesn't need to change again if RTGS/IMPS references differ in
  length from NEFT's. Don't tighten this to `== 16` without checking real RTGS/IMPS samples first.

- **`Payout`/`RiderPayoutLedger` GET-batch-by-id was a one-shot secret.** Before the
  batch-listing endpoints existed, `exportBatchId` only ever appeared once, in the export
  response's `X-Payout-Export-Meta` header — lose it and there was no way to look it back up.
  This is now fixed (`GET .../batches`), but if you add a new export flow elsewhere, give it a
  listing endpoint from day one.

- **`ExistsWithTransactionReferenceAsync` checks duplicates across *all* payouts of that type,
  not just the current batch.** Don't narrow this to batch-scoped — the whole point is catching
  a UTR replayed across batches (real fraud/error scenario per spec section 4a).

---

## Security checklist for any change here

- [ ] Does this add a new path to `Status = Paid`? If yes: UTR format check, duplicate-UTR
      check, SuperAdmin gate (endpoint-level, per rule #2), structured `LogWarning` audit entry.
- [ ] Does this touch matching logic? If yes: ambiguous/no-match must still report-not-guess.
- [ ] Does this touch the batch-close condition? If yes: re-verify "closes only when zero
      siblings still Processing."
- [ ] Does this add a decimal/string field written from computed data (hashes, markers,
      concatenations)? Check its `HasMaxLength`/`HasPrecision` against the real max length of
      what you're writing — see the 64-char gotcha above.
- [ ] Did you re-run the live smoke test (below), not just unit tests, if you touched a
      handler, repository query, or entity column?

---

## Testing before you ship a change

**Unit tests** (fast, run every time):
```powershell
dotnet test tests/Modules/Orders/RallyAPI.Orders.Application.Tests/RallyAPI.Orders.Application.Tests.csproj
dotnet test tests/Modules/Orders/RallyAPI.Orders.Domain.Tests/RallyAPI.Orders.Domain.Tests.csproj
dotnet test tests/Modules/Users/RallyAPI.Users.Application.Tests/RallyAPI.Users.Application.Tests.csproj
dotnet test tests/Modules/Users/RallyAPI.Users.Domain.Tests/RallyAPI.Users.Domain.Tests.csproj
```
Relevant test files: `IciciReconciliationParserTests.cs`, `ReconcileRestaurantPayoutsCommandHandlerTests.cs`,
`ReconcileRiderPayoutsCommandHandlerTests.cs`, `ManuallyResolveRestaurantPayoutCommandHandlerTests.cs`,
`ManuallyResolveRiderPayoutCommandHandlerTests.cs`, `GetStaleRestaurantPayoutsQueryHandlerTests.cs`,
`ListRestaurantPayoutExportBatchesQueryHandlerTests.cs`.

**Live smoke test** (required if you touch a handler, repository, or entity/column — mocks
don't catch DB constraint violations, see the 64-char gotcha):

1. `docker compose up -d postgres redis` (start Docker Desktop first if the daemon is down).
2. `rm -rf src/RallyAPI.Host/bin src/RallyAPI.Host/obj` (stale build → phantom Sentry crash).
3. Run the API: `dotnet run --project src/RallyAPI.Host` with env vars
   `ASPNETCORE_ENVIRONMENT=Development`,
   `ConnectionStrings__Database=Host=localhost;Port=5432;Database=rallydb;Username=rally;Password=rally123`,
   `ConnectionStrings__Redis=localhost:6379`, `JwtSettings__Issuer=RallyAPI`,
   `JwtSettings__Audience=RallyApp`, `JwtSettings__PrivateKeyPath=Keys/private.pem`,
   `JwtSettings__PublicKeyPath=Keys/public.pem`. Port is **5023** (hardcoded in
   `launchSettings.json`, `ASPNETCORE_URLS` is ignored).
4. Mint a SuperAdmin JWT (RS256, signed with `src/RallyAPI.Host/Keys/private.pem`, claims
   `sub`/`user_type=admin`/`iss=RallyAPI`/`aud=RallyApp`) against a seeded `users.admins` row
   with `role='SuperAdmin'`.
5. Seed a `Pending` payout (owner/rider + matching bank details) via SQL against
   `orders.payouts` or `users.rider_payout_ledger` — see column names in the EF
   `IEntityTypeConfiguration<T>` files listed in the file map above, not guesses.
6. Drive the real endpoints with curl: export → build a reconcile `.xlsx` matching the exported
   row's account/IFSC/amount exactly → reconcile → verify `Paid`/`Failed` in the DB → re-upload
   the same file and confirm it's a no-op → hit `/stale` and `/batches` → try manual-resolve
   with a non-SuperAdmin token and confirm `403`.
7. Tear down: kill the `dotnet run` process. Leave Postgres/Redis running or `docker compose down`
   as you prefer — the DB volume persists across sessions either way.

This exact sequence caught the 64-char marker bug above; unit tests alone did not.

---

## What's deliberately NOT built yet

Don't assume these exist — check before relying on them:

- **No raw-file byte storage for the audit trail.** Only the SHA-256 hash of the uploaded
  reconciliation file is persisted (`ReconciliationFileHash`). R2 storage is listed as "pending"
  project-wide; wiring this up is a follow-up, not a bug.
- **No "notify all Super Admins" alert on a Paid transition.** The spec calls for this
  (section 4a); today it's a `LogWarning` only. If a notification service gets built for
  something else, this is the first place that should consume it.
- **No maker-checker (second-approval) on reconcile/manual-resolve.** Scoped as a v2 option in
  the spec, not required for v1 given money never routes through Rally.

---

## Quick status reference

| Status | Meaning | Who sets it |
|---|---|---|
| `Pending` | Owed, not yet exported | Weekly aggregation job |
| `Processing` | In an export file, awaiting bank outcome | `MarkProcessing()` at export |
| `Paid` | Reconciled with a real UTR | Reconcile handler or manual-resolve (SuperAdmin) |
| `Failed` | Bank rejected/reversed | Reconcile handler or manual-resolve (SuperAdmin) |
| `OnHold` | Admin paused it (dispute etc.) | `PutOnHold()` — admin action, Pending only |

A payout that never appears in any reconcile report simply stays `Processing` forever unless
someone checks `GET .../stale` — there is still no automatic paging/alerting for this.
