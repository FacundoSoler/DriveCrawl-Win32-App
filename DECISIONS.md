# Decisions

A log of the important choices made for DriveCrawl, and why they were made.

**How to maintain it:**
- Add each new decision at the bottom with the next number.
- Keep each entry short: the decision, the reason, and the alternatives that were considered.
- If a decision came from a write-up, add a **Reference:** line linking to it: an artifact, a design page, or a PR. Mark private links as "(private)".
- Never rewrite or delete an old entry. When a decision changes, add a new entry that says `Supersedes D-NNN`, and change the old entry's status to `Superseded by D-NNN`.

---

## D-001 · Shared agent instructions live in AGENTS.md
**Date:** 2026-11-25 · **Status:** Accepted
**Decision:** AGENTS.md is the single source of truth for every coding agent. CLAUDE.md starts with `@AGENTS.md` and holds only Claude Code–specific notes.
**Why:**
- One file serves Claude, Copilot, Codex and others.
- The import is required: the parent `D:\DEV PROJECTS\CLAUDE.md` would otherwise stop Claude Code from loading AGENTS.md.

## D-002 · Unit tests use xUnit v3
**Date:** 2026-11-25 · **Status:** Accepted
**Decision:** The test stack is:
- xUnit v3 on Microsoft.Testing.Platform
- Xunit.StaFact for WPF tests
- Shouldly for assertions
- NSubstitute for mocks

**Why:** xUnit is the most widely used .NET test framework, and StaFact handles the WPF STA-thread requirement.
**Alternatives:** MSTest 4, NUnit 4, TUnit.

## D-003 · Only minimal changes to production code for testability
**Date:** 2026-11-25 · **Status:** Accepted
**Decision:** Production code gets only small, behaviour-preserving changes to make it testable:
- `InternalsVisibleTo` for the test project
- injected connection strings
- `IUserDialogs` instead of direct MessageBox calls
- extracted pure logic

There are no broad interface layers or rewrites.
**Why:** this gets coverage with a reviewable diff. Larger refactors happen deliberately, one feature at a time.

## D-004 · Tests pin current behaviour, even where it looks like a bug
**Date:** 2026-11-25 · **Status:** Accepted
**Decision:** Tests assert what the code does today. Suspected bugs are tagged `Behaviour=SuspectedBug` and fixed later in their own changes, with the test updated in the same commit.
**Why:** the suite stays green, and every behaviour change becomes an explicit, reviewable decision.

## D-005 · Database schema is versioned SQL scripts, not EF Core
**Date:** 2026-11-25 · **Status:** Accepted
**Decision:**
- The schema ships as embedded `Data/Migrations/NNNN_*.sql` scripts.
- The scripts are applied at startup and tracked with `PRAGMA user_version`.
- They contain schema only, never seed or personal data.

**Why:**
- The app is small and already uses Dapper on Microsoft.Data.Sqlite.
- Plain SQL adds no heavy dependency.
- An existing database is adopted cleanly, without a migrations-history table.

**Alternatives:** EF Core migrations. Reconsider if the Files and Tags tables grow into a real media index.

## D-006 · The user chooses where the library database lives
**Date:** 2026-11-25 · **Status:** Accepted
**Decision:**
- On first run the user chooses where the library is kept. The default is `<exe folder>\Database\`; the alternative is `%LocalAppData%\DriveCrawl\`.
- The choice is stored in `%LocalAppData%\DriveCrawl\settings.json`.
- The location can be changed later from the status-bar Library button.

**Why:**
- A hidden AppData folder is obscure for users.
- The exe folder is visible and portable, but read-only under Program Files. Offering both keeps the location transparent and still works when installed.

## D-007 · Search Locations are managed in a dedicated dialog
**Date:** 2026-11-25 · **Status:** Accepted
**Decision:** Build Option 1 now, a dedicated "Search Locations" dialog for adding, editing and removing locations. Move to Option 3, a Settings drawer, when more settings arrive.
**Why:**
- The dialog is the lowest-risk, quickest build, at about 1.5–2.5 days.
- It follows the existing Library-location dialog pattern.
- Batched Save/Cancel makes mistakes reversible.
- A drawer is the better long-term home once there are several settings, and the dialog's contents can move into it.

**Alternatives:** inline editing in the Drive Paths popup (fiddly WPF popup behaviour, cramped, easy to mis-click); Settings drawer now (more work before it's needed).
**Reference:** [options page](https://claude.ai/artifact/NP578UccjN1E23LmBLypPA) (private).

## D-008 · Git workflow: feature branches, maintainer merges
**Date:** 2026-11-25 · **Status:** Superseded by D-010
**Decision:** Agents work on `feature/<name>` branches and push them. The maintainer opens and merges PRs manually. PR descriptions are a few bullets of core changes; the detail lives in the commits.
**Why:** the maintainer keeps control of what reaches `main`.

## D-009 · Search Locations dialog safeguards
**Date:** 2026-11-25 · **Status:** Accepted
**Decision:** The Search Locations dialog (D-007) ships with three extras:
- It blocks folders that overlap an existing location (one inside another).
- The Drive Paths popup gets a "Manage locations…" link.
- The dialog opens automatically when there are no locations yet.

**Why:**
- Overlapping locations make each file look like its own duplicate, so "Delete selected" could permanently remove the only copy.
- The link and the automatic first-run opening make the feature easy to find, and give new users a guided first step.

## D-010 · Agents open PRs; merged branches delete themselves
**Date:** 2026-11-25 · **Status:** Accepted · Supersedes D-008
**Decision:**
- Agents branch from the latest `main`, push, and open the PR themselves (`gh pr create`) with a short description.
- The maintainer reviews and merges; agents never merge.
- GitHub's "Automatically delete head branches" is on, and `fetch.prune` is on locally, so merged branches disappear.

**Why:** with several branches active it was hard to see what needed attention. Now the Pull requests tab is the single to-do list, and merged work cleans itself up.
