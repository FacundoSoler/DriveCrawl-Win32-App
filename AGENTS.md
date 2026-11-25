# AGENTS.md

DriveCrawl is a Windows-only WPF desktop app (.NET 10) that searches files across a set of
"watched folders" (local and external drives). It finds duplicates and filters videos by
resolution, bitrate and duration. It also shows Shell thumbnails and can move, delete and open
the files it finds. The watched folders come from a local SQLite database.

## Commands

Run from the repo root. The project file name contains spaces, so always quote it.

- Build: `dotnet build "DriveCrawl Win32 App.slnx"`
- Run: `dotnet run --project "DriveCrawl Win32 App.csproj"`
- Build to a custom dir: `dotnet build "DriveCrawl Win32 App.csproj" -o <dir>`
  - `-o` on the `.slnx` raises NETSDK1194.
- Publish: `dotnet publish "DriveCrawl Win32 App.csproj" -c Release -r win-x64 --self-contained false -o publish`
- Test: `dotnet test --solution "DriveCrawl Win32 App.slnx"`
- Single class: `dotnet test --solution "DriveCrawl Win32 App.slnx" -- --filter-class "*SearchResultFilterTests"`
- Pinned suspected bugs only: `dotnet test --solution "DriveCrawl Win32 App.slnx" -- --filter-trait "Behaviour=SuspectedBug"`

Requirements:
- .NET SDK 10.0.x. `global.json` does not pin the SDK.
  It only opts `dotnet test` into Microsoft.Testing.Platform, which xUnit v3 requires on .NET 10.
- Windows. The project targets `net10.0-windows` and uses WPF, COM and the registry.
  It does not build or run on Linux or macOS.

The baseline build has 0 errors. It has known warnings: NU1903 (transitive
`SQLitePCLRaw.lib.e_sqlite3` vulnerability) and six nullable warnings (CS8618, CS8603, CS8625).
Do not add new warnings.

## Code style

These rules also live in `.github/copilot-instructions.md`. Keep both in sync.

- **Principles:** follow SOLID strictly.
  - Prefer clear, readable code over clever one-liners.
  - If a method does more than one thing, or is long, extract it.
- **Member order in every file:**
  1. Fields and properties.
  2. Constructors.
  3. Public methods.
  4. Private logic methods.
  5. Event handlers (`*_Click`, `*_KeyDown`, ...), always last.
  - Follow the step-down rule: order methods by where they first appear in their callers.
- **Naming:** method names must explain themselves. If a name needs a comment to be understood, rename it.
- **Comments:** explain *why*, never *what*. Do not write one-line narration comments.
  - Some existing comments are in Spanish. Write new comments in English.
- **Placement:**
  - Keep `MainWindow.xaml.cs` thin. Business logic goes in services or view models.
  - Put complex or interop logic (thumbnails, COM, Win32, file operations) in dedicated service classes under `Services/`.
- **Language features:** nullable reference types are enabled. Fix nullability properly; do not use `!` to silence warnings.
- **Packages:** do not add a new NuGet dependency without a clear reason.
  - Data access uses Dapper on Microsoft.Data.Sqlite. Do not introduce a second data-access style.

## Testing

- Project: `Tests/DriveCrawl.Tests`. Stack:
  - xUnit v3.
  - Xunit.StaFact: use `[WpfFact]` for anything that touches WPF controls.
  - Shouldly for assertions.
  - NSubstitute for mocks.
- The app csproj sits at the repo root and excludes `Tests\**` from its globs. Keep that exclusion when you add folders.
- The test project reaches internals through `InternalsVisibleTo("DriveCrawl.Tests")` (see `AssemblyInfo.cs`).
  Prefer `internal` over reflection to test helpers.
- Never touch real user files or the real DB in tests.
  - Use `TestSupport/TempDirectory` for the file system.
  - Use a temp SQLite file with `Pooling=False`, through `new DatabaseService(connectionString)`.
- Substitute `IUserDialogs` in any code that would show a MessageBox. A real MessageBox blocks the test run.
- Sizes are formatted with the current culture. Wrap assertions on formatted sizes in `CultureScope`.
- Name tests `Member_Scenario_ExpectedResult`. Write them as Arrange / Act / Assert, with one behaviour per test.
- Tests tagged `[Trait("Behaviour", "SuspectedBug")]` pin today's behaviour where it looks wrong.
  When you fix one of those bugs, update or invert its test in the same commit.
- Every new public behaviour needs tests. `dotnet test` must stay green before you finish.

## Architecture: what isn't obvious from the tree

- It is code-behind, not MVVM.
  - `MainWindow` builds every collaborator with `new`. There is no DI container and there are no interfaces.
  - The "Manager" classes (`SearchResultsManager`, `SearchTimerManager`, `ThumbnailManager`) take WPF controls in their constructors.
  - New logic should not take controls. Return data and let the window bind it.
- Search pipeline: `MainWindow.StartSearch` → `SearchCoordinator.ExecuteSearchAsync` → `SearchEngine.Search`.
  - `SearchEngine.Search` walks the file system live with `Directory.EnumerateFiles`.
  - Video metadata (TagLib) loads afterwards in the background through the returned `hydrationTask`.
  - Thumbnails load separately through `ThumbnailManager` → `ShellThumbnailService` (COM).
- `WatchedFolders` rows are called **Search Locations** in the UI.
  - `DatabaseService.GetFolders()` reads them for searching.
  - `SearchLocationRepository` adds, edits and removes them, all in one batch.
  - Users manage them in `Views/SearchLocationsWindow`, opened from the gear next to Drive Paths.
- Search locations must never overlap. One folder inside another makes each file appear twice,
  so duplicate mode would mark the only copy for deletion.
  `SearchLocationRules` enforces this and also compares paths case-insensitively. Keep those rules in place.
  - Search does **not** use the DB index. The `Files`, `Tags` and `FileTags` tables exist in the schema but nothing uses them.
- File location and namespace don't always match. Check the namespace before you add `using`s:
  - `SearchEngine.cs` is at the root but in namespace `DriveCrawl_Win32_App.Services`.
  - `DatabaseService.cs` is at the root but in namespace `DriveCrawl_Win32_App.Data`.
- `RootNamespace` is `DriveCrawl_Win32_App`. The assembly name is the default, `DriveCrawl Win32 App` (with spaces).
  This matters for `InternalsVisibleTo`.
- `MainWindow.Operator` (a nested enum) is used by `SearchResultsManager`.
- Search matching uses the OS wildcard `*{query}*` through `Directory.EnumerateFiles`.
  - It is case-insensitive and also matches the extension.
  - `*` and `?` in the query act as wildcards.
  - "Exact phrase" is a second pass with the regex `\b{query}\b`, run against the name without the extension.
- Duplicate rule: two files are duplicates when they have the same `FileName` and `FileSize`.
  - `FileName` is compared case-sensitively.
  - Within a group, the file whose watched folder has the **highest** `Priority` number is kept.
    All the others are auto-selected for deletion.
  - The UI shows Priority 1 as the green, most important colour, so this may be inverted.
    Confirm with the maintainer before you change it.
- Post-search filters (name, bitrate, resolution) always start again from the unfiltered result list. They never stack.

## Database

- SQLite, accessed only through Dapper on Microsoft.Data.Sqlite. The `.db` file is never in the repo.
  It holds the user's local folder paths, so never commit it.
- **Schema as code:** versioned scripts in `Data/Migrations/NNNN_description.sql` are embedded in the assembly.
  - At startup, `App.OnStartup` calls `DatabaseInitializer`, which runs `SchemaMigrator`.
  - The migrator applies every script above `PRAGMA user_version`, one transaction each, then stamps the new version.
- **To change the schema:** add the next `NNNN_*.sql`. Never edit a script that has already shipped.
  - Versions must be sequential starting at 1.
  - SQLite can't alter constraints in place, so a column or constraint change needs a table rebuild script.
  - Scripts contain schema only. No seed or personal data.
- **Foreign keys:** `DatabaseLocation.BuildConnectionString` turns them on for every connection.
  Build connection strings through it so the `ON DELETE CASCADE` rules keep working.
- **Location:** the user chooses where the library lives.
  - Default: `<exe dir>\Database\MediaLibrary.db`.
  - Alternative: `%LocalAppData%\DriveCrawl\MediaLibrary.db`.
  - The choice is saved in `%LocalAppData%\DriveCrawl\settings.json`. Change it from the status-bar **Library** button.
  - All path logic lives in `DataLocationService` and `DataLocationPaths`. Never hard-code a DB path elsewhere.
- The app refuses a database whose `user_version` is newer than its own scripts.
- Search does not use `Files`, `Tags` or `FileTags` yet.
- A fresh library starts with no search locations, so the Search Locations dialog opens on first launch.

## Gotchas

- **Permanent delete:** Delete calls `File.Delete`, so files skip the Recycle Bin.
  Tests that touch file actions must use temp directories only.
- **Deleted rows come back:** deleting removes rows from the grid but not from `SearchResultsManager`'s
  original list, so the deleted rows reappear the next time a filter is applied.
- **Dialogs in logic:** `FileSystemOperations` still calls `MessageBox` directly.
  `FileActionsManager` goes through `IUserDialogs`. Use the same abstraction for any new dialog.
- **Single-core crash:** `SearchEngine.HydrateVideoMetadataAsync` uses `Environment.ProcessorCount / 2` as the max
  degree of parallelism. That is 0 on a single-core machine, and 0 throws.
- **Thumbnail threading:** `ShellThumbnailService` swallows every exception. `ThumbnailManager` silently does
  nothing when `Application.Current` is null, for example in tests.
- **Culture-dependent output:** `FileSizeConverter` formats with the current culture (`1,5 MB` on es-ES).
- **Missing change notification:** `FileViewModel.VideoResolution` is computed but never raised when
  `VideoWidth` or `VideoHeight` change. `Duration` and `BitrateKbps` don't notify either.
- **Inconsistent filters:** the resolution filter compares inclusively (`>=`, `<=`), while the bitrate filter is strict (`>`, `<`).
- **Opening files:** VLC is preferred when it is installed (found through the registry). Otherwise the shell default handler is used.

## Keeping these instructions current

- If your change makes anything in AGENTS.md or CLAUDE.md inaccurate, fix that line in the same commit.
  This covers a command, convention, file location, schema, or a gotcha you just fixed.
  Examples: you fixed a suspected bug listed under Gotchas, or you moved the database.
- Add a line only for something an agent couldn't learn by reading the code: a non-obvious rule,
  a pitfall, or a correction the maintainer gave more than once.
- Don't add change logs, history, or descriptions of code that is self-explanatory.
  Keep AGENTS.md short (under 200 lines, Anthropic's recommended ceiling). When something becomes obsolete, delete it rather than adding to it.
- Keep `.github/copilot-instructions.md` in sync when you change the Code style section.
- **DECISIONS.md** records important decisions and the reasons behind them.
  - Read it before proposing an alternative to a choice that's already settled.
  - When the maintainer makes a decision, or you make one together, append a short numbered entry.
  - Never rewrite an old entry. Add a new entry that supersedes it.

## Commits and PRs

- Never push to `main`, and never merge a PR. Merging is the maintainer's call.
- Branch from the latest `main` as `feature/<name>`, `fix/<name>` or `docs/<name>`. Use one branch per PR, and avoid stacking branches.
- When a chunk of work is pushed and ready, open its PR with `gh pr create --base main`.
  The description is a few bullets of core changes plus a one-line testing note.
- GitHub deletes merged branches automatically, and `fetch.prune` is on. Don't keep merged branches around.
- Keep commits focused. Write messages in the imperative mood ("Add bitrate filter").
- Before you finish, run `dotnet build "DriveCrawl Win32 App.slnx"` and `dotnet test --solution "DriveCrawl Win32 App.slnx"`.
