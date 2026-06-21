namespace FtrIO.OneTwo;

internal enum MigrationStatus { ReadyToMigrate, NeedsReview, CannotMigrate, StaleFlag, DeletedFlag }

internal record MigrationEntry(
    string FlagKey,
    string NormalisedKey,
    string SdkMethod,
    string File,
    int Line,
    MigrationStatus Status,
    string? CurrentValue,
    string? Warning
);
