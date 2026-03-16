# Git Workflow Rules — Rally

## Commit Format

`type(scope): description`

Types: feat, fix, refactor, docs, test, chore, perf, ci
Scopes: users, orders, delivery, payments, restaurants, notifications, admin, dashboard, auth, signalr, menu, riders, infra, db

Examples:
- `feat(orders): add SignalR hub for real-time order status`
- `fix(payments): verify PayU webhook hash before processing`
- `test(delivery): add rider assignment handler unit tests`
- `refactor(auth): extract JWT validation to shared middleware`
- `feat(dashboard): add incoming orders page with live updates`

## Branch Strategy

- `main` — production (deployed to Railway)
- `develop` — integration
- `feature/*` — new features
- `fix/*` — bug fixes
- `hotfix/*` — urgent production fixes from main

## Before Committing (.NET)

1. `dotnet build` — must succeed with zero warnings
2. `dotnet test` — affected tests must pass
3. No hardcoded secrets, connection strings, or API keys
4. No `Console.WriteLine` debugging left in code
5. No commented-out code without a TODO ticket reference

## Before Committing (React)

1. `npm run typecheck` — must pass
2. `npm run lint` — must pass
3. No `console.log` left in code
4. No `any` types
5. No inline styles — Tailwind only

## Pull Requests

- Title matches commit format
- Description: what changed, why, how to test
- Tag affected modules
- Request review from team
