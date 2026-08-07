# GitHub Branch Protection — Click-by-Click

Do this on **each** frontend repo, on the **production branch** (whatever the deploy
platform actually deploys — usually `main` or `production`). Confirm the branch name first.

You must be a repo **Admin / org Owner** to see these settings.

---

## A. Add the CI workflow first

1. In the frontend repo, create `.github/workflows/ci.yml` using the contents of
   `frontend-ci.yml` from this package.
2. Open one throwaway PR (or push a branch) so the workflow runs at least once. GitHub only
   lets you mark a check "required" after it has run once and it knows the check's name
   (the job is called **`verify`**).

## B. Add the CODEOWNERS file

1. Add `CODEOWNERS` (from this package) to the repo root or `.github/` folder on the
   production branch. Set the username to yours.
2. GitHub will now treat you as the owner of all files → you become a required reviewer.

---

## C. Protect the production branch

**Settings → Branches → Add branch protection rule** (or **Settings → Rules → Rulesets →
New branch ruleset** — newer UI, same effect).

**Branch name pattern:** the production branch, e.g. `main` (or `production`).

Enable these:

- [x] **Require a pull request before merging**
  - [x] Require approvals → **1**
  - [x] **Require review from Code Owners** ← this is what forces *your* approval
  - [x] Dismiss stale approvals when new commits are pushed
- [x] **Require status checks to pass before merging**
  - [x] Require branches to be up to date before merging
  - Search and select the **`verify`** check (from `frontend-ci.yml`)
- [x] **Require conversation resolution before merging** (optional, tidy)
- [x] **Do not allow bypassing the above settings** (applies rules to admins too — turn on
      once your own workflow is smooth; you can leave it off at first so you can hotfix)
- [x] **Restrict who can push to matching branches**
  - Add **only yourself**. This is the switch that makes you the *only* person who can
    land code on the production branch. The developer keeps Write access to the repo so he
    can push *feature branches* and open PRs, but he cannot push to or merge into
    production.
- [x] Do not allow force pushes
- [x] Do not allow deletions

Save.

---

## D. Verify it works (one test PR)

1. Have the developer (or you, from a second account/branch) open a PR into production.
2. Confirm:
   - CI (`verify`) runs on the PR.
   - The **Merge** button is disabled for the developer ("Review required" / "Only users
     with push access can merge").
   - It becomes available for **you** only after you approve.
3. Merge it yourself → confirm the deploy platform fires a production deploy.

If all three hold, control is in place.

---

## Common gotchas

- **"He can still merge!"** → He has Admin/Maintain role, or "Restrict who can push" wasn't
  set, or "Do not allow bypassing" is off. Re-check Step C.
- **CI check not selectable as required** → the workflow hasn't run yet. Push a branch once
  (Step A.2), then it appears in the list.
- **Deploy still happens from his laptop** → that's not a GitHub problem; it's deploy
  gating. See `deploy-gating.md`.
- **Wrong branch protected** → you protected `main` but the platform deploys `master` (or
  vice-versa). Match the protected branch to the platform's production branch exactly.
