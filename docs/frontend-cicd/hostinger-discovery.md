# Hostinger Discovery — Simple Questions for the Frontend Dev

The frontend is hosted on **Hostinger**, which does not auto-deploy from GitHub the way
Vercel/Netlify do. So we first need to learn exactly how he puts the site live, then we give
him step-by-step instructions. Ask these in plain language and **ask for screenshots** — a
picture removes all guesswork.

---

## Copy-paste message to send him

> Quick few questions so I can set up the deploy properly on Hostinger — no rush, just want
> to understand the current setup. Screenshots are perfect, you don't have to explain in
> words. **Please don't send any passwords in chat** — I just need to know *what* you use,
> not the credentials.
>
> 1. Which Hostinger plan is it — shared/web hosting, Cloud, or a VPS? (A screenshot of your
>    Hostinger hPanel home page shows this.)
> 2. When you make a change and want it live, **what do you actually click/do?** For example:
>    - upload files in Hostinger's **File Manager**, or
>    - use an **FTP program** like FileZilla, or
>    - the **Git** section inside Hostinger, or
>    - type commands over **SSH**.
>    A screenshot of the screen you use to deploy would be ideal.
> 3. Is the website's **source code on GitHub**? If yes, send me the repo link(s). If not,
>    where does it live — just on your laptop?
> 4. Do you run `npm run build` and upload the **`dist`** (or `build`) folder, or do you edit
>    files directly on the server?
> 5. Is it **one website or two** (separate for Customer and Rider)? What are the domains /
>    subdomains?
> 6. Whose email is the **Hostinger account** under, and do I already have the login?

---

## What each answer tells us (for your reference)

| Question | Why we ask | What it decides |
|----------|-----------|-----------------|
| 1. Plan (shared/Cloud/VPS) | Shared = static file upload only. VPS = full control via SSH. | Which deploy method is even possible. |
| 2. How he deploys | Reveals the real control point (FTP creds? hPanel login? Git tab?). | Where we insert the gate. |
| 3. Source on GitHub? | If not on GitHub, there's nothing to protect yet — Step 1 is *get it into GitHub*. | Whether branch protection is possible at all. |
| 4. Build step? | React must be built (`npm run build`) → static files. Confirms CI can produce the artifact. | What GitHub Actions needs to build + upload. |
| 5. One site or two | Customer + Rider may be two repos / two FTP targets. | How many pipelines to set up. |
| 6. Account owner | **The most important one.** Whoever owns the Hostinger login controls prod. | Whether you already have control or need a handover. |

---

## The likely end-state (so you know where this is going)

Once we have the answers, the target setup for Hostinger is almost certainly:

1. **Source code lives in a GitHub repo under the `Hivago` org** (you own it), with branch
   protection so only you can merge — same as the backend.
2. **GitHub Actions** builds the app on every merge to the production branch and **uploads
   the built files to Hostinger over FTP** (using the `SamKirkland/FTP-Deploy-Action`).
3. The **FTP credentials are stored as GitHub Secrets that only you can see/change.** The
   developer no longer uploads anything manually — he opens PRs, you merge, the Action
   deploys.
4. **You hold the Hostinger account login;** his FTP access is removed or its password
   rotated, so he can't bypass the pipeline by uploading directly.

Result: identical control to the backend — nothing goes live without your merge — even
though Hostinger itself isn't a modern git-deploy host.

> We'll write the exact `deploy.yml` (with the FTP step) and the precise Hostinger clicks
> once his six answers come back. Don't build the Action yet — the FTP host/path/plan
> details from his screenshots determine the config.
