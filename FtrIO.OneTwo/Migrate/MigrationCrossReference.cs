namespace FtrIO.OneTwo;

/// <summary>
/// Holds the API-side information about a flag needed for cross-referencing.
/// </summary>
internal record ApiFlagInfo(
    string Kind,        // "boolean", "string", "number", "json"
    bool HasTargeting,  // rules/targets/prerequisites present
    string? Value       // resolved value string (if available)
);

internal static class MigrationCrossReference
{
    /// <summary>
    /// Cross-references SDK call entries against API flag data to produce MigrationEntry list.
    /// </summary>
    internal static IReadOnlyList<MigrationEntry> CrossReference(
        IReadOnlyList<SdkScanner.SdkCallEntry> codeEntries,
        IReadOnlyDictionary<string, ApiFlagInfo>? apiFlagsByOriginalKey,
        HashSet<string>? excludeKeys)
    {
        var results = new List<MigrationEntry>();

        // Collect all API-only keys (stale flags)
        var seenCodeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in codeEntries)
            seenCodeKeys.Add(e.FlagKey);

        // Process code entries
        foreach (var e in codeEntries)
        {
            if (excludeKeys != null && excludeKeys.Contains(e.FlagKey))
                continue;

            var normKey = KeyNormaliser.ToPascalCase(e.FlagKey);

            if (apiFlagsByOriginalKey is null)
            {
                // No API key provided
                results.Add(new MigrationEntry(
                    e.FlagKey, normKey, e.SdkMethod, e.File, e.Line,
                    MigrationStatus.NeedsReview, null, "No API key provided"));
                continue;
            }

            if (!apiFlagsByOriginalKey.TryGetValue(e.FlagKey, out var apiFlag))
            {
                results.Add(new MigrationEntry(
                    e.FlagKey, normKey, e.SdkMethod, e.File, e.Line,
                    MigrationStatus.DeletedFlag, null, "Flag not found in API"));
                continue;
            }

            MigrationStatus status;
            string? warning = null;

            if (apiFlag.Kind == "json")
            {
                status = MigrationStatus.CannotMigrate;
                warning = "JSON flags cannot be mapped to a simple toggle value.";
            }
            else if (apiFlag.HasTargeting || apiFlag.Kind == "number")
            {
                status = MigrationStatus.NeedsReview;
                warning = apiFlag.HasTargeting
                    ? "Flag has targeting rules."
                    : "Flag is of kind 'number'.";
            }
            else
            {
                status = MigrationStatus.ReadyToMigrate;
            }

            results.Add(new MigrationEntry(
                e.FlagKey, normKey, e.SdkMethod, e.File, e.Line,
                status, apiFlag.Value, warning));
        }

        // Stale flags: in API but not in code
        if (apiFlagsByOriginalKey is not null)
        {
            foreach (var (apiKey, apiFlag) in apiFlagsByOriginalKey)
            {
                if (excludeKeys != null && excludeKeys.Contains(apiKey))
                    continue;
                if (seenCodeKeys.Contains(apiKey))
                    continue;

                var normKey = KeyNormaliser.ToPascalCase(apiKey);
                results.Add(new MigrationEntry(
                    apiKey, normKey, string.Empty, string.Empty, 0,
                    MigrationStatus.StaleFlag, apiFlag.Value, "Flag exists in API but not referenced in code"));
            }
        }

        return results;
    }
}
