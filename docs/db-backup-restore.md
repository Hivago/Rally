# Database Backup & Restore

Railway's Hobby plan does not include managed backups or point-in-time
recovery (PITR) — those are Pro-plan only. `.github/workflows/db-backup.yml`
is the DIY substitute: a **daily snapshot**, not PITR. Worst case on restore,
you lose up to a day of writes, not up to a second. That's the accepted
trade-off for staying on Hobby.

## One-time setup (do this before the schedule can succeed)

### 1. Create a dedicated R2 bucket for backups

Don't reuse the existing media bucket (`rally-media` or similar) — a
compromised media-serving credential should never be able to read or
overwrite database backups, and vice versa.

- Cloudflare dashboard → R2 → **Create bucket** → e.g. `rally-db-backups`

### 2. Create an API token scoped only to that bucket

- R2 dashboard → **Manage API tokens** → **Create API token**
- Permissions: **Object Read & Write**
- Scope: the `rally-db-backups` bucket only (not "all buckets")
- Save the Access Key ID and Secret Access Key — R2 only shows the secret once

### 3. Enable a public connection string for Postgres

GitHub Actions runs outside Railway's private network, so it needs the
public/TCP-proxy connection string, not the internal `*.railway.internal` one:

- Railway dashboard → Postgres service → **Settings → Networking → TCP Proxy**
  → enable it if not already on
- Copy the resulting connection string (Railway's "Connect" tab shows the
  public variant once the proxy is enabled)

Optional hardening: create a read-only Postgres role for backups instead of
using the default superuser credentials (`psql` in, then
`CREATE ROLE backup_reader WITH LOGIN PASSWORD '...'; GRANT CONNECT ON DATABASE railway TO backup_reader; GRANT USAGE ON ALL SCHEMAS ... GRANT SELECT ON ALL TABLES ...`).
Not blocking for the first version — the token itself is already scoped to
GitHub's encrypted secrets — but worth doing before this backup covers real
customer/payment data.

### 4. Add GitHub Actions secrets

Repo → **Settings → Secrets and variables → Actions** → add:

| Secret | Value |
|---|---|
| `BACKUP_DATABASE_URL` | The public Postgres connection string from step 3 |
| `R2_ACCOUNT_ID` | Your Cloudflare account ID |
| `R2_BACKUP_ACCESS_KEY_ID` | From step 2 |
| `R2_BACKUP_SECRET_ACCESS_KEY` | From step 2 |
| `R2_BACKUP_BUCKET` | `rally-db-backups` (or whatever you named it) |

### 5. Set a retention (lifecycle) rule on the bucket

R2 dashboard → the backups bucket → **Settings → Object lifecycle rules** →
add a rule to expire objects after, e.g., 30 days. This deletes old dumps
automatically so storage cost doesn't grow forever — no code needed.

### 6. Verify before trusting the schedule

Actions tab → **Database Backup** workflow → **Run workflow** (this is the
`workflow_dispatch` trigger) → confirm it goes green and a `.dump` file shows
up in the R2 bucket. Until you've done this once, the daily schedule is
unverified.

## Restoring a backup (do this periodically — an untested backup is not a backup)

1. Download the dump from R2 (Cloudflare dashboard, or `aws s3 cp` with the
   same endpoint URL as the workflow).
2. Spin up a **throwaway** Postgres instance — a new Railway service, or
   local Docker (`docker run -e POSTGRES_PASSWORD=test -p 5433:5432 postgres:16`).
   Never restore onto a database anything else is using.
3. Restore:
   ```
   pg_restore --no-owner --no-acl -d "<throwaway-connection-string>" rally-db-<timestamp>.dump
   ```
4. Connect and sanity-check: schemas present (`users`, `orders`, `catalog`,
   `delivery`, `pricing`, `marketing`), row counts look plausible, a spot-check
   query against a real table returns real data.
5. Tear down the throwaway instance.

Do this at least once now (to prove the whole pipeline works end to end) and
then periodically — monthly is reasonable at this stage.
