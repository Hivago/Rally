# AI Workflow Setup Guide — Rally

> Step-by-step guide to wire up your AI-powered dev workflow.
> Customized for: .NET 8 + PostgreSQL + React + TypeScript + Railway

---

## What's in This Kit

```
rally-ai-setup/
├── CLAUDE.md                              # ⭐ Project brain — ALL AI tools read this
├── AGENTS.md                              # Agent behavior rules (cross-tool)
├── .claude/rules/
│   ├── dotnet-rules.md                    # C# / .NET / EF Core / MediatR rules
│   ├── react-rules.md                     # React / TypeScript / Tailwind / GSAP rules
│   ├── git-workflow.md                    # Commit format, branch strategy
│   └── testing.md                         # xUnit + Vitest + RTL testing rules
├── specs/
│   ├── _TEMPLATE.md                       # Feature spec template (CQRS-aware)
│   └── signalr-realtime-notifications.md  # ⭐ Real spec for your #1 priority
└── reviews/
    └── DAILY-REVIEW-TEMPLATE.md           # Daily improvement checklist
```

---

## Setup Steps

### Step 1: Copy to Your Project (5 min)

```bash
# Copy to your RallyAPI project root
cp CLAUDE.md /path/to/RallyAPI/
cp AGENTS.md /path/to/RallyAPI/

# Copy rules
mkdir -p /path/to/RallyAPI/.claude/rules
cp .claude/rules/* /path/to/RallyAPI/.claude/rules/

# Copy specs
cp -r specs/ /path/to/RallyAPI/specs/

# Copy reviews
cp -r reviews/ /path/to/RallyAPI/reviews/

# Commit immediately
cd /path/to/RallyAPI
git add CLAUDE.md AGENTS.md .claude/ specs/ reviews/
git commit -m "chore: add AI workflow configuration files"
```

### Step 2: Customize CLAUDE.md (15 min — MOST IMPORTANT)

Open `CLAUDE.md` and:
- [ ] Update the project structure to match your ACTUAL folder layout
  - Run `find src -type d -maxdepth 4` and paste the tree
- [ ] Verify module names match your actual modules
- [ ] Add any conventions I might have missed
- [ ] Add your Railway project URLs if relevant
- [ ] Update environment variable list

### Step 3: Install Claude Code (5 min)

```bash
npm install -g @anthropic-ai/claude-code
claude --version

# Navigate to your project
cd /path/to/RallyAPI
claude
```

### Step 4: Connect MCP Servers (5 min)

```bash
# GitHub — for PRs, issues, code search
claude mcp add --transport http github https://api.github.com/mcp

# Notion — for project docs, specs, roadmap
claude mcp add --transport http notion https://mcp.notion.com/mcp

# Verify
claude mcp list
```

### Step 5: Token Optimization (2 min)

Add to `~/.claude/settings.json`:
```json
{
  "model": "sonnet",
  "env": {
    "MAX_THINKING_TOKENS": "10000",
    "CLAUDE_AUTOCOMPACT_PCT_OVERRIDE": "50"
  }
}
```

### Step 6: Install ECC Plugin (Optional, 5 min)

```bash
# Inside Claude Code:
/plugin marketplace add affaan-m/everything-claude-code
/plugin install everything-claude-code@everything-claude-code
```

This gives you the full agent/skill/hook system on top of your custom configs.

---

## Your Immediate Priorities (Based on Project Status)

Given your web-first pivot and what's already built:

### Week 1: SignalR + Restaurant Dashboard Foundation

1. **Implement SignalR** using the spec at `specs/signalr-realtime-notifications.md`
   - Open Claude Code → "Implement the SignalR spec at specs/signalr-realtime-notifications.md"
   - It reads CLAUDE.md (knows your .NET/CQRS patterns) + the spec (knows exactly what to build)

2. **Scaffold Restaurant Dashboard** (React)
   - Write a spec first: `specs/restaurant-dashboard-scaffold.md`
   - Then: Claude Code → "Build the restaurant dashboard scaffold from the spec"

### Week 2: Restaurant Dashboard Features

3. **Incoming Orders Page** — live orders via SignalR
4. **Menu Management** — CRUD with image upload (Cloudflare R2)
5. **Restaurant Settings** — operating hours, delivery zone, open/close toggle

### Week 3: Admin Panel + Polish

6. **Admin Panel scaffold**
7. **Order monitoring dashboard**
8. **CI/CD with GitHub Actions**

---

## Daily Workflow

### Morning (5 min)
1. Copy `reviews/DAILY-REVIEW-TEMPLATE.md` → `reviews/2026-03-17.md`
2. List today's tasks
3. Check/create specs for today's features

### During Development

**New backend feature:**
```
1. Write spec (or ask Claude Chat to draft one)
2. cd RallyAPI && claude
3. "Implement the spec at specs/feature-name.md"
4. AI reads CLAUDE.md + rules + spec → generates CQRS-compliant code
5. /code-review when done
```

**New React component:**
```
1. claude (in frontend project directory)
2. "Create the OrderCard component for the restaurant dashboard.
    It shows incoming orders with accept/reject buttons.
    Use SignalR for real-time updates. Follow react-rules.md."
3. AI reads rules → generates Tailwind + React Query + SignalR code
```

**Architecture decision:**
```
1. Use Claude Chat (this interface) to discuss
2. Once decided, update CLAUDE.md
3. Commit the update
```

### Evening (10 min)
1. Fill out daily review
2. Update CLAUDE.md with learnings
3. Write specs for tomorrow
4. Commit .md changes

---

## Which Tool for What

| Task | Best Tool | Why |
|------|-----------|-----|
| New MediatR handler + endpoint | **Claude Code** | Reads CLAUDE.md, follows CQRS, runs dotnet build |
| React page with SignalR | **Claude Code** | Can create multiple files, test connection |
| Architecture decision | **Claude Chat** | Conversational, can visualize |
| Write feature spec | **Claude Chat** | Good at structured docs |
| PR review | **Codex** / Claude Code `/code-review` | Async, thorough |
| Quick UI tweaks | **Cursor/Antigravity** | IDE-integrated, inline |
| Debug EF Core query | **Claude Code** | Can inspect DB, check SQL |
| Debug SignalR connection | **Claude Code** | Can check logs, test connection |

---

## The Improvement Loop

Every day, your CLAUDE.md gets smarter. After one week:
- AI knows your exact module structure
- AI knows your naming conventions
- AI knows your common mistakes and avoids them
- AI knows your domain (orders, riders, restaurants)
- AI generates code that looks like YOUR code, not generic tutorial code

**This is the difference between teams using AI as a chatbot vs teams using AI as a team member.**
