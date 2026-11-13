# 05 — Access, secrets and offboarding checklist

Goal: after the last day, nothing the business depends on is owned by, or only known to, one departing person.
**Do not put secret values in this file or the repo.** Use a company-owned password manager.

## A. Accounts to transfer or share (owner today → action)

Fill in the "New owner" column during the handoff meeting. "Where I saw it" is from project history — confirm each in the actual console.

| Account / system | What it holds | Where I saw it | New owner |
|------------------|---------------|----------------|-----------|
| GitHub org `Hivago` | Backend repo `Hivago/Rally` | PR URLs | ☐ ≥2 admins |
| GitHub personal `yashdvishwakarma` | 3 frontend repos (`hivago`, `hivago_admin`, `hivago_restaurant`) + Actions secrets | CI/CD docs | ☐ **transfer to org** |
| Railway | Prod + staging services, Postgres, Redis, all env vars | project `perceptive-essence` | ☐ company-owned workspace, ≥2 members |
| Hostinger (hPanel) | Frontend hosting, DNS, FTP accounts | CI/CD docs | ☐ |
| Domain registrar / DNS for `hivago.in` | `api.`, `admin.`, `restaurant.` | — | ☐ confirm registrar + owner |
| PayU India | Merchant account, live key/salt, webhook config, MID/UPI | PayU notes | ☐ business contact on the MID |
| ProRouting | 3PL account, API key, webhook config, invoices | integration docs | ☐ |
| AuthKey.io | SMS OTP (sender `RALLYO`, DLT PE id + template) | appsettings | ☐ |
| Exotel | Former SMS provider (account `hivago1`) | appsettings (commented) | ☐ close or rotate |
| MSG91 | WhatsApp OTP (blocked on Meta verification) | appsettings | ☐ |
| Meta Business | WhatsApp verification | notes | ☐ |
| Google Cloud | Maps keys (Geocoding/Places/Routes/Maps JS), Vision (KYC OCR) | appsettings | ☐ billing owner |
| Cloudflare | R2 bucket `rally-media` | appsettings | ☐ |
| Sentry | Error tracking | CLAUDE.md | ☐ confirm project exists |
| Discord | Ops-alert + onboarding webhooks, CI failure webhook | appsettings, CI | ☐ |
| Vercel | **Orphaned** frontend deploys (personal account of frontend dev) | CI/CD notes | ☐ disconnect |
| Apple/Google developer accounts | Flutter apps (future) | — | ☐ if any exist |

## B. Secrets to rotate

Rotate anything that I (or the frontend dev, who has already been cut off from FTP) have seen. Do it *after* the new owner holds the accounts, one at a time, checking the app after each.

1. **Exotel `ApiKey` + `ApiToken`** — verified present in `HEAD:src/RallyAPI.Host/appsettings.json` (commented-out block) and introduced in commit `3eb9e16`, so they are in git history on every clone. Rotate/close the Exotel account, remove the block, and (optionally) rewrite history or accept that old clones hold them.
2. **JWT RSA keypair** — `Keys/` and `*.pem` are gitignored (verified: not tracked), but the same pair has been used locally, on staging (set via env 2026-06-24) and possibly prod, and lives on my machine. Generate a fresh pair, set `JwtSettings__PrivateKeyPem/PublicKeyPem` on Railway, and use *different* pairs for staging and prod. **This logs out every user** — schedule it.
3. PayU merchant key + salt (ask PayU to reset salt; update Railway; confirm hash still verifies with a test txn).
4. ProRouting API key; `PROROUTING_INBOUND_API_KEY`.
5. AuthKey, MSG91 auth keys.
6. Google Maps keys (old shared key `AIzaSyBeHL…` is to be deleted; create a referrer-locked browser key and a server key).
7. R2 access key/secret; Google Vision credentials JSON.
8. Railway Postgres/Redis passwords (regenerate; the public proxy URL was used from my machine).
9. Discord webhook URLs; GitHub personal access tokens / git credential caches on my machine; any Railway/CLI tokens.
10. Hostinger FTP passwords were already rotated 2026-07-24; rotate again if the same password was shared.

## C. Run these checks before leaving

```bash
# anything secret-looking in tracked files / history?
git ls-files | grep -E 'pem$|\.log$|\.env'
git log --all -p -S"ApiToken" -- src/RallyAPI.Host/appsettings.json | head
gh secret list -R Hivago/Rally   # and the 3 frontend repos
```
- Confirm `.gitignore` covers `*.pem` (except what must be committed), `*.log`, `.env`.
- Run a secret scanner (gitleaks/trufflehog) over full history and record the findings.

## D. Knowledge transfer

- [ ] 60–90 min recorded walkthrough with the new owner using `HANDOFF.md` + `01-architecture.md`.
- [ ] New owner does one deploy and one migration (local → staging → promote) while I watch.
- [ ] Walk through R1, R3, R5, R6 live on staging.
- [ ] Hand over the ProRouting contact, PayU relationship manager, AuthKey contact (names only; no personal numbers in the repo).
- [ ] Explain the payout process and who operates it (`docs/icici-payout-operator-guide.md`).
- [ ] Agree a post-departure Q&A window, if the contract allows it.

## E. My machine / personal tooling (check your contract before copying anything)

- Local AI memory notes (`~/.claude/projects/.../memory/*.md`) contain the incident history this handoff was built from; the useful content is now in `docs/handoff/`. They also contain environment identifiers, so don't copy them to a personal machine unless your contract allows it.
- Remove the company's repos, tokens and Railway CLI login from personal devices at the end.

## F. Commit plan for these docs

Commit on a `docs/handoff` branch, PR into `staging`, request review from the CTO. These docs contain no secret values.
