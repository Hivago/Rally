# Frontend CI/CD & Merge Control — CTO Playbook

**Goal:** Nothing reaches the frontend production apps (Customer Web, Rider Web) without
your explicit approval. Every deploy is traceable to a Pull Request that **you** merged.

**Owner of this process:** Yash (CTO). The frontend developer works on branches and opens
PRs; he cannot merge to the production branch and cannot deploy directly.

---

## Why a pipeline alone is not enough (read this first)

Control has **three layers**. If any one is missing, the developer can still ship to prod
without you:

| Layer | What it controls | If you skip it… |
|-------|------------------|-----------------|
| **1. Ownership** | Who owns the GitHub repo + the deploy account (Vercel/Netlify/Railway) | If he owns the account, he can bypass everything. This is the #1 gap. |
| **2. Branch protection** | Nothing merges to the production branch without your review | Anyone with push access ships straight to prod. |
| **3. Deploy gating** | Prod deploys **only** when the protected branch changes | He runs `vercel --prod` from his laptop and skips git entirely. |

The rest of this playbook sets up all three.

---

## Step 0 — Find out how the frontend is wired today

You said you're not sure of the current setup. Ask the developer these exact questions
(or check yourself). The answers decide a couple of details, but the package here covers
every common case.

1. **Where does the code live?** GitHub repo URL(s). Are Customer Web and Rider Web one
   repo or two? (Likely two separate repos.)
2. **Who owns the repo?** Is it under the `Hivago` GitHub org, or his personal account?
3. **What deploys the site?** Vercel, Netlify, Railway, Cloudflare Pages, or a manual
   FTP/server upload?
4. **Who owns that deploy account?** His personal login, or a company account you control?
5. **Which branch is "production"?** (`main`, `master`, `production`, `prod`?)
6. **Does he deploy from the CLI** (`vercel --prod`, `netlify deploy --prod`, `railway up`),
   or only via automatic git deploys?
7. **What package manager?** npm, pnpm, or yarn (decides one line in the CI file).

> **The single most important answer is #4.** Whoever owns the deploy account is who
> ultimately controls prod. If it's his personal account, migrating ownership to a company
> account is your first move — see `deploy-gating.md`.

---

## Step 1 — Ownership (do this before anything else)

### GitHub repo
- The frontend repo(s) must live under the **`Hivago` organization**, with **you as Owner**.
  - If it's on his personal account: **Settings → Danger Zone → Transfer ownership** to
    `Hivago`. (He initiates; you accept. Takes 2 minutes, keeps all history/issues/PRs.)
- Set the developer's role to **Write**, not Admin/Maintain:
  - Org/repo **Settings → Collaborators and teams**. Write = can push branches and open
    PRs, **cannot** change branch protection or merge protected branches. That's exactly
    what you want.

### Deploy account
- The Vercel/Netlify/Railway **project must be under a company account/team that you own**,
  connected to the GitHub repo via the platform's Git integration.
- The developer gets, at most, **Member/Developer** access to that project — enough to see
  logs and preview deploys, **not** to change the production branch or promote to prod.
- See `deploy-gating.md` for the platform-specific switches.

---

## Step 2 — Branch protection (only you can merge)

Full click-by-click steps: **`github-branch-protection.md`**.

Summary of what it enforces on the production branch:
- ❌ No direct pushes — everything goes through a PR.
- ✅ CI must pass (the `verify` check from `frontend-ci.yml`).
- ✅ Requires a review from **you** (via the `CODEOWNERS` file — you are the code owner).
- ✅ **Restrict who can push/merge to the branch → only you.**
- ✅ No force-pushes, no deleting the branch, no bypassing (applies even to admins).

Result: the developer can open as many PRs as he likes; the **Merge** button only works
for you.

---

## Step 3 — Continuous Integration

Drop **`frontend-ci.yml`** into each frontend repo at `.github/workflows/ci.yml`.
It runs on every PR and blocks the merge unless:
- `npm run typecheck` passes (no TS errors),
- `npm run lint` passes,
- `npm run build` succeeds (the app actually compiles).

These match the rules already in `.claude/rules/react-rules.md`. Once the workflow has run
once, add its `verify` job as a **required status check** in branch protection (Step 2).

---

## Step 4 — Deploy gating

Full steps per platform: **`deploy-gating.md`**. The universal rule:

> **Production branch = the protected branch, and remove the developer's ability to deploy
> manually (CLI / promote-to-prod).**

So the *only* path to prod is: merge a PR (which only you can do) → platform auto-deploys.
Previews on PR branches are fine and encouraged — they let him show you the change before
you merge.

---

## The new workflow (what changes day-to-day)

**Before:** developer pushes → site updates. You find out after.

**After:**
1. Developer branches off `production`, does the work.
2. He opens a **Pull Request**. CI runs automatically; a **preview URL** is generated.
3. You review the diff + click the preview link to see it live.
4. You **merge** (or request changes). Only you can merge.
5. Merge → platform deploys to production automatically.

Nothing is live until you merge. You have a full audit trail in the PR list.

---

## Rollout checklist

- [ ] Got answers to the Step 0 questions from the developer.
- [ ] Frontend repo(s) transferred to `Hivago` org; you are Owner.
- [ ] Developer's repo role set to **Write** (not Admin).
- [ ] Deploy account owned by company/you; developer downgraded to Member.
- [ ] Any deploy tokens the developer holds are **rotated** (old ones revoked).
- [ ] `frontend-ci.yml` added to each repo, first run green.
- [ ] Branch protection enabled on the production branch (`github-branch-protection.md`).
- [ ] `CODEOWNERS` added with you as owner.
- [ ] Deploy platform: production branch set to protected branch; manual deploy disabled.
- [ ] Sent the developer the handoff message (`handoff-to-frontend-dev.md`).
- [ ] Did one test PR end-to-end to confirm: he can't merge, you can, deploy fires on merge.

---

## Files in this package

| File | Use |
|------|-----|
| `README.md` | This overview / master plan. |
| `github-branch-protection.md` | Click-by-click GitHub settings. |
| `frontend-ci.yml` | Drop-in CI workflow for the frontend repo(s). |
| `CODEOWNERS` | Makes you the required reviewer. Goes in the repo root or `.github/`. |
| `deploy-gating.md` | Vercel / Netlify / Railway specific deploy lockdown. |
| `handoff-to-frontend-dev.md` | Message to send the developer + his to-do list. |
