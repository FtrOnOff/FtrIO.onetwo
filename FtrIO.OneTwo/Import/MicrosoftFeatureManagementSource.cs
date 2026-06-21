using System.Text.Json.Nodes;

namespace FtrIO.OneTwo;

/// <summary>
/// Reads flag state from the FeatureManagement section of a local appsettings.json.
/// Handles simple boolean values, percentage rollout (EnabledFor: Percentage), and
/// approximates complex filter configurations.
/// </summary>
internal sealed class MicrosoftFeatureManagementSource : IFlagSource
{
    private readonly string _filePath;

    public MicrosoftFeatureManagementSource(string filePath)
    {
        _filePath = filePath;
    }

    public Task<IReadOnlyList<ImportedFlag>> FetchAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
            throw new FileNotFoundException($"Config file not found: {_filePath}");

        var text = File.ReadAllText(_filePath);
        var doc = JsonNode.Parse(text);
        var section = doc?["FeatureManagement"]?.AsObject();

        if (section is null)
            return Task.FromResult<IReadOnlyList<ImportedFlag>>(new List<ImportedFlag>());

        var results = new List<ImportedFlag>();

        foreach (var (key, value) in section)
        {
            if (value is null) continue;

            var mapped = MapEntry(key, value);
            results.Add(mapped);
        }

        return Task.FromResult<IReadOnlyList<ImportedFlag>>(results);
    }

    internal static ImportedFlag MapEntry(string key, JsonNode value)
    {
        // Simple boolean: "SendWelcomeEmail": true
        if (value is JsonValue jsonVal)
        {
            var raw = jsonVal.GetValue<object>().ToString() ?? "false";
            return new ImportedFlag(key, key, raw.ToLowerInvariant(), FlagStatus.Direct, null);
        }

        // Object form: { "EnabledFor": [ ... ] }
        if (value is JsonObject obj)
        {
            var enabledFor = obj["EnabledFor"]?.AsArray();

            if (enabledFor is null || enabledFor.Count == 0)
                return new ImportedFlag(key, key, "false", FlagStatus.Approximated,
                    $"Flag '{key}' has no EnabledFor filters — treating as disabled.");

            // Check for Percentage filter
            foreach (var filter in enabledFor)
            {
                var name = filter?["Name"]?.GetValue<string>() ?? string.Empty;
                if (name.Equals("Percentage", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Microsoft.Percentage", StringComparison.OrdinalIgnoreCase))
                {
                    var pctNode = filter?["Parameters"]?["Value"];
                    if (pctNode is not null)
                    {
                        double pct = 0;
                        try { pct = pctNode.GetValue<double>(); } catch { }
                        return new ImportedFlag(key, key, $"{(int)pct}%", FlagStatus.Direct, null);
                    }
                }
            }

            // Other filter types — approximate as enabled with warning
            var filterNames = string.Join(", ", enabledFor
                .Select(f => f?["Name"]?.GetValue<string>() ?? "unknown")
                .Where(n => !string.IsNullOrEmpty(n)));

            return new ImportedFlag(key, key, "true", FlagStatus.Approximated,
                $"Flag '{key}' uses feature filter(s): {filterNames}. Approximated as enabled.");
        }

        return new ImportedFlag(key, key, "false", FlagStatus.Unsupported,
            $"Flag '{key}' has an unrecognised config shape.");
    }
}
