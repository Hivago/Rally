# Feature Spec: ICICI Manual Payout Export & Reconciliation

> **Status**: Draft
> **Priority**: P1 (High)
> **Estimated Effort**: ~4-5 days (excl. final ICICI column mapping)
> **Module(s)**: Orders (restaurant payouts), Users (rider payouts)
> **Owner**: Yash
> **Date**: 2026-07-20

---

## 1. Problem Statement

PayU quoted ₹35,000 to integrate automated payouts to restaurant bank accounts. Instead,
the owner will move money himself via the **ICICI Corporate Internet Banking bulk-transfer
portal**. Rally must therefore own the **calculation and file export** side: produce an
accurate weekly bank-upload file (one per payout type) that the owner uploads to ICICI, and
then reconcile the per-beneficiary success/failure ICICI reports back into our system. No
money moves through Rally; we own the ledger, the file, and the reconciliation.

## 2. User Stories

- As an **admin**, I want to download an accurate weekly ICICI bulk-transfer file for
  restaurant payouts so I can upload it to the ICICI portal.
- As an **admin**, I want a separate weekly file for rider payouts for the same reason.
- As an **admin**, I want owners/riders with missing or invalid bank details excluded from
  the file and listed separately, so a bad row never breaks the upload or silently drops money.
- As an **admin**, I want to upload ICICI's result file and have each payout auto-marked
  Paid (with UTR) or Failed (with the bank's reason).
- As an **admin**, I want to see payouts stuck "in the bank" (exported but not yet
  reconciled) beyond N days so nothing rots silently.

## 3. Acceptance Criteria

- [ ] Weekly export produces a `.xlsx` per payout type (restaurant, rider), one row per beneficiary.
- [ ] Each file's control-sum total equals the sum of its rows exactly (no rounding drift).
- [ ] Export includes **only `Pending`** payouts and flips them to `Processing` atomically —
      a payout can never appear in two export files (anti-double-pay).
- [ ] Beneficiary bank details are read **live** from the owner/rider record at export time.
- [ ] Missing/invalid bank details → excluded from file, returned as a "cannot pay" list.
- [ ] Uploading an ICICI result file reconciles each row: `Processing → Paid` (UTR) or
      `Processing → Failed` (reason).
- [ ] Failed payout can be retried (`Failed → Pending`) after bank details are corrected and
      picked up in the next export.
- [ ] A stale-payouts report lists anything `Processing` older than a threshold.
- [ ] The exact ICICI column layout (export) and result-file layout (reconcile) live in a
      single mapping class each, swappable when ICICI templates arrive.

## 4. Technical Design

### Lifecycle (both Payout types — statuses already exist)

```
Pending ──[weekly export]──► Processing   (in a file, awaiting bank)
Processing ──[ICICI success + UTR]──► Paid
Processing ──[ICICI rejected + reason]──► Failed
Failed ──[fix bank details, retry]──► Pending   (re-exported next week)
Pending ──[admin pause]──► OnHold ──► Pending
```

**Safety rule:** the export query selects only `Pending`; the export transition is atomic
(`Pending → Processing` + stamp `ExportedAtUtc` + `ExportBatchId`). `Processing`/`OnHold`
are never re-exported.

### Domain Changes

**Orders module — `Payout`** (aggregate already exists):
- Add `ExportedAtUtc` (DateTime?) and `ExportBatchId` (Guid?).
- Reuse existing `MarkProcessing()` for the export transition; add stamping.
- Reuse `MarkPaid(txnRef)` (Processing→Paid) and `MarkFailed(reason)` (Processing→Failed).

**Users module — `RiderPayoutLedger`** (aggregate already exists):
- Add `ExportedAtUtc`, `ExportBatchId`.
- **Add `MarkProcessing()`** (Pending→Processing) — currently missing.
- **Add `MarkPaid(txnRef)`** from Processing — currently only `MarkPaidImmediate` from Pending/OnHold.
- `MarkFailed` already allows Pending/Processing→Failed. `MarkRetry` already Failed→Pending.

**New lightweight entity per module — `PayoutExportBatch`**:
`Id, PayoutType, PeriodStart, PeriodEnd, RowCount, ControlSumTotal, Status(Generated|Reconciled), CreatedAt`.
Each payout references its batch via `ExportBatchId`. Gives audit trail + re-download + reconcile anchor.

### Commands & Queries (MediatR)

| Type | Name | Module | Description |
|------|------|--------|-------------|
| Command | `GenerateRestaurantPayoutExportCommand` | Orders | Build weekly batch, flip Pending→Processing, produce rows |
| Command | `GenerateRiderPayoutExportCommand` | Users | Same, rider side |
| Command | `ReconcileRestaurantPayoutsCommand` | Orders | Parse ICICI result, apply Paid/Failed per row |
| Command | `ReconcileRiderPayoutsCommand` | Users | Same, rider side |
| Query | `GetStalePayoutsQuery` | Orders/Users | Processing older than threshold |

### API Endpoints

| Method | Endpoint | Auth | Description |
|--------|----------|------|-------------|
| POST | `/api/admin/payouts/restaurants/export?period=` | Admin | Returns `.xlsx` + excluded list |
| POST | `/api/admin/payouts/riders/export?period=` | Admin | Returns `.xlsx` + excluded list |
| POST | `/api/admin/payouts/restaurants/reconcile` | Admin | Upload ICICI result file |
| POST | `/api/admin/payouts/riders/reconcile` | Admin | Upload ICICI result file |
| GET | `/api/admin/payouts/stale` | Admin | Stuck-in-bank report |

### Excel (ClosedXML — already in the project)

- Column layout isolated in `IciciExportRowMapper` (export) and `IciciResultParser` (reconcile).
- Placeholder layout now; swap column order/headers when the real ICICI templates arrive.
- Control-sum/total row appended for reconciliation.

## 4a. Reconciliation Trust Boundary (Security)

The reconcile upload is a trust boundary: whoever uploads a "bank statement" can flip payouts
to `Paid`. It is restricted to Super Admin, but role-gating alone is not sufficient (compromised
account, insider risk) — reconciliation must be verifiable, not just authorized.

**Critical framing:** reconciliation only *records* state; it never *moves* money. The actual
transfer happens inside ICICI's own portal, behind the bank's separate credentials. So a forged
statement cannot divert funds — the worst case is mislabeling a payout as Paid when it wasn't
(bookkeeping fraud / concealment), which is a materially smaller and detectable risk. Controls
below are sized to that risk, not to a funds-diversion risk.

| Control | Mechanism | Stops |
|---------|-----------|-------|
| Amount-match | Reject any statement row whose amount != the payout's stored `NetPayoutAmount` (exact decimal match) | Forgery must reproduce every exact computed figure |
| UTR validation | Validate UTR format (22-char alphanumeric per RBI convention); reject duplicate UTRs already recorded on another payout | Replay / lazy forgery / double-counting one real transfer across rows |
| Immutable audit trail | Store the raw uploaded file + its SHA-256 hash + uploader identity + timestamp, never overwritten | Attribution; lets a later dispute compare against the real ICICI-issued file |
| Paid-transition audit log + alert | Every `Processing -> Paid` write emits an audit log entry and notifies all Super Admins | Rogue upload is visible immediately, not discovered later |
| Idempotent reconciliation | Re-uploading a statement that includes already-`Paid` rows is a no-op for those rows (skip + report), never re-applies | Double-processing, accidental or malicious re-upload |
| Monthly bank cross-check (process, not code) | Compare system `Paid` totals against the owner's actual ICICI account statement | Standard segregation-of-duties control; catches any tampering that slipped past the above |

Maker-checker (second Super Admin approval before a reconcile commits) is a v2 option if the
owner wants stronger control; not required for v1 given the framing above.

## 5. Edge Cases & Error Handling

| Scenario | Expected Behavior |
|----------|-------------------|
| Owner/rider missing bank account/IFSC/name | Excluded from file; returned in "cannot pay" list |
| Same payout exported twice | Impossible — export selects only Pending, flips to Processing atomically |
| ICICI rejects a row (bad account) | Reconcile marks that payout Failed with reason; others unaffected |
| Payout stuck Processing (NEFT pending / no result uploaded) | Surfaced by stale-payouts report; stays Processing |
| Reconcile file references unknown/already-Paid payout | Row skipped + reported; no state change |
| Rounding | Per-row nets are 2dp; batch total = sum of 2dp rows → control-sum exact |

## 6. Testing Plan

- **Domain unit tests**: payout math + control-sum (extend `PayoutLedgerTests`); new rider
  `MarkProcessing`/`MarkPaid` transitions and guards; export-only-Pending invariant.
- **Handler unit tests**: export excludes missing-bank rows; reconcile applies Paid/Failed;
  reconcile is idempotent on already-Paid rows.
- **Integration tests**: export endpoint returns xlsx + control-sum; reconcile round-trip.

## 7. Rollout

- [ ] Admin-only endpoints; no customer-facing change.
- [ ] Rollback: export is read-mostly + a reversible state flip; a mis-generated file can be
      cancelled by moving its batch's payouts Processing→Pending.
- [ ] Metrics: # payouts exported vs reconciled vs stale each week.

---

## Implementation Notes (updated during build)

### Decisions Made
- Two separate files (restaurant, rider) — keeps Orders/Users module boundaries clean.
- Reconciliation via ICICI result-file upload (parsed), not manual per-row.
- Beneficiary bank details read live at export time (no stale snapshot on the batch).
- ClosedXML (already used by Catalog menu import/template) — no new dependency, no ₹35k gateway.

### Open Questions — RESOLVED (2026-08-04)

Confirmed against a real ICICI "Consolidated Status Report" (`ConsolidatedReportNPAB.xlsx`,
a live vendor-payment batch the owner ran manually). This is the bank's actual result-file
layout — 29 columns, header row 1:

```
File_Sequence_Num | Pymt_Prod_Type_Code | Pymt_Mode | Debit_Acct_no | Beneficiary Name |
Beneficiary Account No | Bene_IFSC_Code | Amount | Debit narration | Credit narration |
Mobile Numder | Email id | Remark | Pymt_Date | Reference_no | Addl_Info1-5 |
Beneficiary LEI | STATUS | Current Step | File name | Rejected by | Rejection Reason |
Acct_Debit_date | Customer Ref No | UTR NO
```

Observed values: `STATUS` = `Success` / `Reversed` (others likely `Rejected`/`Failed` per
ICICI docs, not seen in the sample). `UTR NO` on real Success rows is **16 characters**
(e.g. `IN42619755781929`), not 22 as originally assumed in section 4a — the UTR-format
validation in `IciciReconciliationParser`/`ReconcileRestaurantPayoutsCommandHandler` checks
length ≥ 10 and alphanumeric rather than hard-coding 16, since RTGS/IMPS references can differ
in length from NEFT's.

**Reconcile matching strategy (implemented):** rather than changing the already-shipped export
template to embed a Rally-owned reference number, reconcile rows are matched to Processing
payouts *within the target export batch* by `(Beneficiary Account No, Bene_IFSC_Code, Amount)`
— exact match required, ambiguous (>1 candidate) or unmatched rows are reported for manual
review, never guessed. Restaurant `Payout` stores bank details at export time so this needs no
extra lookup; `RiderPayoutLedger` has no stored bank fields (export reads them live), so
`ReconcileRiderPayoutsCommandHandler` re-fetches live rider bank details to build the same key.

- Rider payout weekly cycle vs restaurant Mon–Sun period alignment — confirmed via shared
  `PayoutExportBatchStatus` lifecycle; both use the same Generated→Reconciled flow.

### Import/Reconcile — Implemented (2026-08-04)

- `IciciReconciliationParser` (SharedKernel.Utilities.Payouts) — header-name-based column
  lookup (resilient to reordering), classifies each row Success/Failed/Unresolved.
- `ReconcileRestaurantPayoutsCommand`/Handler (Orders.Application) and
  `ReconcileRiderPayoutsCommand`/Handler (Users.Application) — apply the security controls
  from section 4a: exact amount-match (via the matching key itself), UTR format + duplicate
  check, idempotent re-upload (already Paid/Failed rows are a no-op, reported not reapplied),
  structured audit log (Warning level) on every Processing→Paid transition, batch flips to
  `Reconciled` only once every payout in it is resolved (partial uploads leave it `Generated`
  so a fuller report can be uploaded later).
- `POST /api/admin/payouts/restaurant/reconcile?batchId=` and
  `POST /api/admin/payouts/rider/reconcile?batchId=` — multipart file upload, Super-Admin-only
  (checked against `Admin.Role`, not just the generic `Admin` policy — see section 4a).
- Not implemented (deferred, no infra to hang it on yet): raw-file byte storage for the audit
  trail (R2 is still "pending" per project status — only the SHA-256 hash is persisted on the
  batch for now) and the "notify all Super Admins" alert (no notification service exists yet;
  the Warning-level log is the interim signal). Maker-checker second-approval remains a v2
  option as originally scoped.

### Operational gaps closed (2026-08-04, same day)

Three gaps identified while walking through a year of real admin usage were closed immediately:

- `GET /api/admin/payouts/{restaurant|rider}/batches?status=&page=&pageSize=` — lists recent
  export batches with a live Processing/Paid/Failed breakdown. Recovers the `exportBatchId`
  that previously only appeared once, in the export response header.
- `GET /api/admin/payouts/{restaurant|rider}/stale?olderThanDays=3` — payouts stuck Processing
  past the threshold. A pull report, not a push alert — no notification service exists yet, so
  an admin has to check it.
- `POST /api/admin/payouts/{restaurant|rider}/{payoutId}/manual-resolve` — Super-Admin-only
  escape hatch for a Processing payout the automatic matcher can't resolve (ambiguous match,
  drifted rider bank details, a row the bank's report never covered). Requires a ≥10-char
  reason and, for Paid, a UTR checked against the same duplicate-UTR guard as automatic
  reconciliation. Closes the batch (`MarkReconciled`) if it was the last payout still
  Processing, stamping a `MANUAL-OVERRIDE-` prefixed marker instead of a file hash so a later
  audit can tell it apart from an automatic reconcile.
