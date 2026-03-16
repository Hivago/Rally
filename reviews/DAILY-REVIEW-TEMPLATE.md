# Daily AI Workflow Review — Rally

> 10-15 min every day. Save as `reviews/YYYY-MM-DD.md`

---

## Date: ____________________

## 1. What did AI build today?

| Task | Tool Used | Module | Time Saved (est.) | Quality (1-5) |
|------|-----------|--------|-------------------|----------------|
| | Claude Code / Codex / Claude Chat / Cursor | | | |
| | | | | |

## 2. Where did AI go wrong?

- [ ] Violated module boundaries (imported from another module's internals)
- [ ] Didn't follow CQRS (put logic in endpoint instead of handler)
- [ ] Created anemic domain model (public setters, no behavior)
- [ ] Used DateTime instead of DateTimeOffset
- [ ] Generated Next.js/Node code instead of .NET (wrong stack!)
- [ ] Didn't follow FluentValidation pattern
- [ ] Frontend: used CSS modules or inline styles instead of Tailwind
- [ ] Lost context mid-session
- [ ] Other: _______________

**What to add to CLAUDE.md to prevent this:**
> 

## 3. Context Engineering Updates

### CLAUDE.md
- [ ] New architecture decision to add
- [ ] New pattern discovered during coding
- [ ] Warning about common AI mistake to add

### New Spec Needed?
- [ ] Feature built ad-hoc that should have had a spec

### New Rule Needed?
- [ ] .NET pattern that should always be followed
- [ ] React pattern that should always be followed

## 4. Tool Check

### Claude Code
- [ ] MCP servers connected? (GitHub, Notion)
- [ ] Token cost check: `/cost`
- [ ] Using `/compact` between tasks?
- [ ] Using `/model opus` only for complex architecture?

### Codex
- [ ] Feeding it AGENTS.md?
- [ ] Using for PR reviews?

### Claude Chat
- [ ] Using for architecture decisions?
- [ ] Using for spec writing?

## 5. Weekly Metrics (fill on Fridays)

| Metric | This Week | Last Week |
|--------|-----------|-----------|
| Backend endpoints shipped | | |
| Frontend pages/components shipped | | |
| Bugs caught by AI review | | |
| Hours saved (estimate) | | |
| Token cost ($) | | |
| CLAUDE.md updates made | | |

## 6. Tomorrow

### Priority tasks:
1. 
2. 
3. 

### Specs to write first:
- [ ] 

### Tool assignments:
- Claude Code: 
- Claude Chat: 
- Codex: 
