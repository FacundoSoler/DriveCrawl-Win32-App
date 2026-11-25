-- Baseline: mirrors the hand-made schema that existing MediaLibrary.db files already have.
-- IF NOT EXISTS lets those databases adopt version 1 without being altered.

CREATE TABLE IF NOT EXISTS WatchedFolders (
    Id          INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    Path        TEXT    NOT NULL UNIQUE,
    IsExternal  INTEGER DEFAULT 0,
    Priority    INTEGER DEFAULT 1
);

CREATE TABLE IF NOT EXISTS Files (
    Id            INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    FullPath      TEXT    NOT NULL UNIQUE,
    FileName      TEXT    NOT NULL,
    Extension     TEXT,
    FileSize      INTEGER,
    CreatedAt     DATETIME,
    LastAccessed  DATETIME,
    ThumbnailPath TEXT,
    DriveLabel    TEXT
);

CREATE TABLE IF NOT EXISTS Tags (
    Id      INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
    TagName TEXT    NOT NULL UNIQUE
);

CREATE TABLE IF NOT EXISTS FileTags (
    FileId INTEGER NOT NULL,
    TagId  INTEGER NOT NULL,
    PRIMARY KEY (FileId, TagId),
    FOREIGN KEY (FileId) REFERENCES Files (Id) ON DELETE CASCADE,
    FOREIGN KEY (TagId)  REFERENCES Tags (Id)  ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_file_name ON Files (FileName);
