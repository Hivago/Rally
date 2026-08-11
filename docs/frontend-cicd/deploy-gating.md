# Deploy Gating — Lock Prod to the Protected Branch

Branch protection stops bad code from being *merged*. Deploy gating stops the developer
from *deploying without merging*. You need both.

**Universal rule for every platform:**
1. The deploy project is owned by a **company account you control** (not his personal login).
2. **Production branch = the protected branch** (the one only you can merge to).
3. **Manual / CLI deploys are removed** for the developer.
4. **Rotate any deploy tokens** he currently holds after migration.

Below: the specific switches for the three likely hosts. Do the one that matches; if you
don't know yet, the Step 0 questions in `README.md` will tell you.

---

## Vercel

1. **Ownership:** the project must live under a **Vercel Team** you own. If it's under his
   personal account: Project → **Settings → Advanced → Transfer Project** to your team.
2. **Connect Git:** Project → **Settings → Git** → connected to the GitHub repo.
3. **Production Branch:** Settings → Git → **Production Branch** = your protected branch
   (e.g. `main`/`production`). Only pushes to this branch deploy to prod; every other
   branch/PR gets a **Preview** deployment (great — that's your review link).
4. **Kill manual promotion:** the risky bypass is `vercel --prod` from a laptop or clicking
   "Promote to Production" in the dashboard.
   - Set his Team role to **Member** (or **Viewer**), not Owner/Admin. Members can't change
     the production branch or manage domains.
   - Under Team → **Settings → Security / Deployment Protection**, restrict who can promote
     deployments to production.
   - **Revoke his Vercel access tokens** (Account → Tokens are personal, but any *shared*
     project/deploy tokens or CI tokens he holds must be rotated).
5. **Result:** prod updates only when you merge to the production branch. PRs still get
   preview URLs for your review.

## Netlify

1. **Ownership:** site must be under a **Netlify Team** you own. Transfer if needed
   (Site → Site configuration → Transfer, or move via team settings).
2. **Connect Git:** Site → **Build & deploy → Continuous Deployment** linked to the repo.
3. **Production branch:** Build & deploy → **Branches and deploy contexts → Production
   branch** = your protected branch. Enable **Deploy Previews** for PRs (your review link).
4. **Kill manual deploys:**
   - Set his role to **Member**, not Owner. Only Owners change build settings / production
     branch.
   - The bypass here is `netlify deploy --prod` via a **Personal Access Token** or a
     **build hook** URL. Go to Site → **Build & deploy → Build hooks** and delete any he
     has; rotate the site's deploy tokens; ensure he has no Owner-level PAT.
5. **Result:** prod deploys only on push to the production branch (i.e., your merge).

## Railway (same as your backend)

1. **Ownership:** the frontend service must be in a **Railway project you own** (ideally the
   same workspace as the backend).
2. **Connect Git:** Service → **Settings → Source** → connected GitHub repo, with
   **Deploy branch** = your protected branch. Railway auto-deploys when that branch changes.
3. **Kill manual deploys:** the bypass is `railway up` / `railway deploy` from the CLI using
   a **project token** or his membership.
   - Give him **Member** access, not Admin, or remove him from the Railway project entirely
     and let deploys come purely from the GitHub integration.
   - **Rotate/delete any Railway project tokens** he holds (Project → Settings → Tokens).
4. **Result:** prod deploys only on merge to the deploy branch, exactly like the backend.

## Cloudflare Pages / other

Same three ideas: own the account, set the production branch to the protected branch, and
remove his ability to run a direct `wrangler pages deploy` (rotate API tokens, downgrade his
role to a non-deploying one).

## Manual FTP / server upload (worst case)

If there's no git-connected platform and he uploads a build to a server:
- Move to a git-connected host (Vercel/Netlify are free for this and take ~15 min to set up).
- Until then, control means changing the **server/hosting credentials** so only your CI can
  publish. A pipeline can't gate a manual FTP upload — the credential is the control point.

---

## The bypass test (do this after setup)

Ask yourself, for your actual host: *"If the developer wanted to push a change to prod right
now without my merge, could he?"* Walk through each path:

- Push to production branch directly → **blocked** by branch protection ✔
- `vercel --prod` / `netlify deploy --prod` / `railway up` → **blocked** because he's a
  Member without a valid deploy token ✔
- Dashboard "Promote to production" → **blocked** by his reduced role ✔
- Old token still lying around → **rotated** ✔

If all four are blocked, you have real control.
