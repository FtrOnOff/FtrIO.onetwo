namespace FtrIO.OneTwo;

internal enum FlagStatus { Direct, Approximated, Unsupported }

internal record ImportedFlag(
    string NormalisedKey,
    string OriginalKey,
    string? Value,
    FlagStatus Status,
    string? Warning
);
