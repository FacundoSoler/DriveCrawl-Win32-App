@AGENTS.md

# Claude Code notes

AGENTS.md above is the shared source of truth for every coding agent. Put project facts there.
Use this file only for Claude Code–specific workflow.

- `D:\DEV PROJECTS\CLAUDE.md` (the parent directory) also loads and holds the same coding standards.
  If they ever conflict, this repo's AGENTS.md wins.
- Claude Code reads AGENTS.md only through the `@AGENTS.md` import above. A parent CLAUDE.md exists,
  so Claude Code would otherwise skip AGENTS.md. Keep the import on the first line.
- The shell is Windows. Use PowerShell or Git Bash syntax, and always quote paths that contain
  `DriveCrawl Win32 App`.
- When you verify a change, build with `dotnet build "DriveCrawl Win32 App.slnx"` and run
  `dotnet test --solution "DriveCrawl Win32 App.slnx"`. The task isn't done if the build adds new
  warnings or any test fails.
- Do not launch the app unless asked. On first run it opens a dialog on the user's desktop and writes
  `%LocalAppData%\DriveCrawl\settings.json` into their real profile.
- Never modify the user's real library (`MediaLibrary.db` in either location) or `settings.json`.
  To inspect the library, copy it to a temp directory first. Never stage or commit any `.db` file.
- Git: work in a worktree, branch off the latest `main`, push, then open the PR with `gh pr create`.
  Never merge. End the report with a table of open PRs and the suggested merge order.
- Branches created in a worktree also appear in the maintainer's local repo and stay there after the worktree
  is removed. At the start of each task, run `git fetch --prune`, then delete the local branches you created.
  Only delete a branch whose upstream is `[gone]` and which `git merge-base --is-ancestor <branch> origin/main` confirms is merged.
  Never delete the branch the maintainer has checked out, or a branch you didn't create.
- Local-only branches (`local/<name>`, see AGENTS.md and D-012): build them in a worktree as usual, but hand the
  branch back so the maintainer can test it from their normal folder:
  1. Commit, then run `git switch --detach` in the worktree. Git checks a branch out in only one folder, so this frees it.
  2. Tell the maintainer to switch their normal folder to the branch and rebuild. In GitHub Desktop they must select
     the *main* worktree first, then the branch, or the switch lands in your worktree instead.
  - A worktree-isolated session can't run git against the main checkout, and neither can `!` commands the maintainer
    types into it. Never hand them `git -C "<main repo>"` commands from inside a worktree.
  - Never call `ExitWorktree` with `remove` on a local-only branch. It deletes the branch, which exists nowhere else.
  - If `.git/hooks/pre-push` is missing, tell the maintainer before committing. A worktree session can't write `.git/hooks`.
- Personal or machine-specific notes go in `CLAUDE.local.md`, which is gitignored. Don't put them here.
