# Weekly Payout Run — Operator Guide (ICICI)

> This is for whoever runs the weekly restaurant/rider payout cycle. No coding knowledge
> needed. If something here doesn't match what you're seeing, stop and check with the dev
> team rather than guessing — these steps move real money.
> Last updated: 2026-08-04.

---

## Before you start

- You need a **Super Admin** login. A regular admin account can *view* payout batches and
  reports but cannot mark anything as paid or upload bank statements — if an action below
  gives you a "Only a Super Admin" error, that's why.
- You'll need access to Rally's **ICICI Corporate Internet Banking** portal separately —
  Rally's system never moves money itself. It only produces the file you upload to the bank,
  and reads back the result once the bank has processed it.

---

## The weekly cycle, step by step

### 1. Generate the payout file

Run the export for restaurants, and separately for riders, for the week you're settling.
This produces a `.xlsx` file and locks in exactly who's getting paid — anyone included here
is taken out of next week's run automatically, so this step can't accidentally pay someone
twice.

- You'll get the file itself, **plus a batch ID** — write this down or keep the response,
  you'll need it again in step 3. (There's also a way to look up recent batch IDs later if
  you lose it — see "If you lose a batch ID" below.)
- You'll also get a list of anyone who was **excluded** — usually because their bank account
  details are missing or incomplete. Follow up with them before the next cycle; they won't be
  paid until it's fixed.

### 2. Upload to ICICI

Log into the ICICI Corporate Internet Banking portal and upload the file from step 1 through
the bulk-transfer feature, same as any manual bank upload. The bank will process the transfers
— this can take anywhere from same-day to the next business day.

### 3. Download the bank's result report

Once the bank has processed the batch, download ICICI's **Consolidated Status Report** for
that upload from the portal. This is the file that says which transfers succeeded, which
failed, and the bank's transaction reference (UTR) for each successful one.

### 4. Upload the result report back into Rally

Upload that report using the batch ID from step 1. Rally reads every row and:
- Matches successful transfers to the right payout and marks them **Paid**, recording the
  bank's UTR.
- Matches failed/reversed transfers and marks them **Failed**, recording the bank's reason.
- Anything it can't confidently match gets left alone and reported back to you — see "If a
  payout doesn't get resolved automatically" below.

You'll get a summary back: how many rows were in the file, how many got marked Paid, how many
Failed, and whether the batch is now fully closed out or still has open items.

**It's safe to upload the same report twice** if you're not sure whether it went through —
anything already resolved is just skipped, never re-applied.

---

## What the statuses mean

| Status | What it means | What you do |
|---|---|---|
| **Pending** | Owed, not yet in any file | Nothing — picked up automatically next export |
| **Processing** | In an uploaded file, waiting on the bank | Wait 1–2 business days, then reconcile |
| **Paid** | Bank confirmed, done | Nothing |
| **Failed** | Bank rejected the transfer | Fix their bank details, then retry (see below) |
| **On Hold** | Someone paused this manually (e.g. a dispute) | Release it once resolved |

---

## If a payout doesn't get resolved automatically

Sometimes a row in the bank's report can't be matched with full confidence — for example, if a
rider changed their bank account between the export and the reconciliation. Rather than guess,
Rally leaves it alone and tells you why (in the "unresolved" list from step 4).

For these, a **Super Admin** can manually mark the payout Paid or Failed — but this requires
you to have personally verified the outcome (e.g. checked the actual bank statement), and you
must type in a real reason. This is logged and treated as a deliberate decision, not a routine
action — use it only when you're sure, not as a shortcut.

## If a transfer failed

1. Find out why (bad account number, closed account, name mismatch, etc.) — the reconcile
   summary or the bank's report will tell you.
2. Get the correct bank details from the restaurant/rider.
3. Update their bank details in the system.
4. Retry the payout — it goes back to Pending and gets picked up in the next weekly export.

## If a payout seems stuck

There's a report that lists anything still "Processing" for more than a few days — run this
periodically (nothing currently pings you automatically, so make it part of your routine,
e.g. every Monday before starting the new cycle). If something's been stuck a long time:
1. Check whether you actually uploaded the reconcile report for its batch yet.
2. Check the ICICI portal directly for that transfer's real status.
3. If the bank confirms it went through (or failed) but it's still stuck in Rally, use the
   manual-resolve option above.

## If you lose a batch ID

There's a way to list recent batches (restaurant and rider separately) showing each one's
period, how many payouts are still open vs. paid vs. failed, and whether it's fully closed
out. Use this if you've lost track of a batch ID from step 1, or just want to see what's
outstanding at a glance.

---

## Monthly check (do this even if everything above seems fine)

Once a month, compare Rally's total "Paid" amount for the month against the actual ICICI
account statement. This is the real backstop against any mistake or misuse in the process
above — Rally's own logs record who did what and when, but a human cross-check against the
bank's own numbers is what actually catches a problem before it compounds. Don't skip this.

---

## Who to escalate to

- Report can't be uploaded / gives an error you don't understand → dev team.
- Numbers in Rally don't match the bank statement → dev team **and** whoever owns the ICICI
  account, immediately — don't try to fix it by re-uploading things.
- A payout has been stuck for over a week with no explanation → dev team.
