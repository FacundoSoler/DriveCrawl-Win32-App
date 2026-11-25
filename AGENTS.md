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
- Tests: there is no test project yet. When one is added, run it with `dotnet test "DriveCrawl Win32 App.slnx"`.

Requirements:
- .NET SDK 10.0.x. There is no `global.json`.
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

## Architecture: what isn't obvious from the tree

- It is code-behind, not MVVM.
  - `MainWindow` builds every collaborator with `new`. There is no DI container and there are no interfaces.
  - The "Manager" classes (`SearchResultsManager`, `SearchTimerManager`, `ThumbnailManager`) take WPF controls in their constructors.
  - New logic should not take controls. Return data and let the window bind it.
- Search pipeline: `MainWindow.StartSearch` → `SearchCoordinator.ExecuteSearchAsync` → `SearchEngine.Search`.
  - `SearchEngine.Search` walks the file system live with `Directory.EnumerateFiles`.
  - Video metadata (TagLib) loads afterwards in the background through the returned `hydrationTask`.
  - Thumbnails load separately through `ThumbnailManager` → `ShellThumbnailService` (COM).
- The database is read in one place only: `DatabaseService.GetFolders()` reads `WatchedFolders`.
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

- File: `Database/MediaLibrary.db` (SQLite). It is **gitignored and not in the repo**. A fresh clone has no DB.
- Runtime path: `<exe dir>\Database\MediaLibrary.db`. The csproj copies it to the output with `CopyToOutputDirectory=Always`,
  so every build overwrites the runtime copy.
- There is no code that creates or migrates the schema. If the file is missing, `Microsoft.Data.Sqlite` creates an empty
  DB and the app fails with `no such table: WatchedFolders`.
- Minimal schema the app needs:
  ```sql
  CREATE TABLE WatchedFolders (
    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    Path TEXT NOT NULL UNIQUE,
    IsExternal INTEGER DEFAULT 0,
    Priority INTEGER DEFAULT 1);
  ```
- Never commit the `.db` file. It contains the user's local folder paths.
- Do not change the schema by hand without also adding a versioned migration or script.
  The migration strategy (EF Core migrations or versioned SQL scripts) has not been decided yet.

## Gotchas

- **Permanent delete:** Delete calls `File.Delete`, so files skip the Recycle Bin.
  Tests that touch file actions must use temp directories only.
- **Deleted rows come back:** deleting removes rows from the grid but not from `SearchResultsManager`'s
  original list, so the deleted rows reappear the next time a filter is applied.
- **Unsafe dialogs:** `MessageBox` is called inside logic (`FileActionsManager`, `FileSystemOperations`).
  Do not call these from tests or background threads. Extract a dialog abstraction first.
- **Single-core crash:** `SearchEngine.HydrateVideoMetadataAsync` uses `Environment.ProcessorCount / 2` as the max
  degree of parallelism. That is 0 on a single-core machine, and 0 throws.
- **Thumbnail threading:** `ShellThumbnailService` swallows every exception. `ThumbnailManager` silently does
  nothing when `Application.Current` is null, for example in tests.
- **Culture-dependent output:** `FileSizeConverter` formats with the current culture (`1,5 MB` on es-ES).
- **Missing change notification:** `FileViewModel.VideoResolution` is computed but never raised when
  `VideoWidth` or `VideoHeight` change. `Duration` and `BitrateKbps` don't notify either.
- **Inconsistent filters:** the resolution filter compares inclusively (`>=`, `<=`), while the bitrate filter is strict (`>`, `<`).
- **Opening files:** VLC is preferred when it is installed (found through the registry). Otherwise the shell default handler is used.

## Commits and PRs

- Never push to `main`. Work on `feature/<short-name>` branches. The maintainer opens and merges PRs.
- Keep commits focused. Write messages in the imperative mood ("Add bitrate filter").
- Before you finish, run `dotnet build "DriveCrawl Win32 App.slnx"`. Once a test project exists, also run `dotnet test`.
