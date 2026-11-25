-- Legacy databases carry idx_full_path, which duplicates the automatic index behind Files.FullPath UNIQUE.
DROP INDEX IF EXISTS idx_full_path;
