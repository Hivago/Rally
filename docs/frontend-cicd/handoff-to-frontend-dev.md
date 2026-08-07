# Message to Send the Frontend Developer

This is a copy-paste-ready message + the concrete list of things you need *from* him and
*for* him. Send the message; use the checklist to track the migration.

---

## Copy-paste message

> Hey — we're standardizing how the web apps ship, same as we already do on the backend.
> Going forward everything goes through Pull Requests and I'll review + merge. This gives us
> a clean history, preview links for every change, and a rollback path if something breaks.
> Nothing about how you build changes — just the last step.
>
> **New flow:**
> 1. Branch off `production` (or `main`), do your work as usual.
> 2. Open a Pull Request. CI runs automatically (typecheck + lint + build) and you get a
>    live **preview URL** on the PR.
> 3. Drop me the PR — I'll review the preview and merge. The merge triggers the prod deploy.
>
> To set this up I need a few things from you (below). Should take ~20 minutes total.

---

## What you need FROM him (send these questions)

1. GitHub repo URL(s) for the Customer web app and Rider web app.
2. Confirm the **production branch** name for each (`main` / `master` / `production`?).
3. What hosts the live sites — **Vercel, Netlify, Railway,** or something else?
4. What package manager — **npm, pnpm, or yarn?**
5. Confirm the repos have `typecheck`, `lint`, and `build` npm scripts (if not, he adds
   `"typecheck": "tsc --noEmit"`).

## What he needs to DO (migration steps for him)

- [ ] **Transfer the repo(s)** to the `Hivago` GitHub org (Settings → Danger Zone →
      Transfer). You then accept the transfer and set yourself as Owner.
- [ ] **Transfer the deploy project** (Vercel/Netlify/Railway) to the company account/team
      you own — or add you as Owner so you can take it over. See `deploy-gating.md`.
- [ ] **Add the CI workflow**: create `.github/workflows/ci.yml` in each repo from
      `frontend-ci.yml`, adjusted for the package manager.
- [ ] Push one branch so CI runs once (lets you mark the check "required").
- [ ] Hand over/rotate any deploy tokens or build hooks so old credentials stop working.

## What YOU do after he hands over

- [ ] Set his repo role to **Write** (not Admin).
- [ ] Set his deploy-platform role to **Member** (not Owner).
- [ ] Add `CODEOWNERS` with your username.
- [ ] Turn on branch protection (`github-branch-protection.md`).
- [ ] Set the platform's **production branch** to the protected branch; confirm PRs produce
      previews.
- [ ] Run one **test PR** end-to-end: confirm he can't merge, you can, and merging deploys.

---

## If he pushes back

This is a completely standard team workflow — it's the same thing every company with more
than one engineer does, and it's identical to how your backend already works. Framing that
usually helps:

- It's **not about trust** — it's about having a review step, preview links, an audit trail,
  and a one-click rollback (revert the PR). It protects *him* too: if a deploy breaks prod,
  there's a clear record and an instant undo.
- His day-to-day doesn't change; only the final "go live" click moves to you.
