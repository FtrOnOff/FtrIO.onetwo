namespace FtrIO.OneTwo;

internal sealed class EnvSource : IFlagSource
{
    private readonly string _prefix;

    public EnvSource(string prefix)
    {
        _prefix = prefix;
    }

    public Task<IReadOnlyList<ImportedFlag>> FetchAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<ImportedFlag>();
        var envVars = System.Environment.GetEnvironmentVariables();

        foreach (System.Collections.DictionaryEntry entry in envVars)
        {
            var envKey = entry.Key?.ToString() ?? string.Empty;
            if (!envKey.StartsWith(_prefix, StringComparison.OrdinalIgnoreCase))
                continue;

            var stripped = envKey.Substring(_prefix.Length);
            var normKey = KeyNormaliser.ToPascalCase(stripped);
            var rawValue = entry.Value?.ToString() ?? string.Empty;

            string value;
            if (rawValue.Equals("true", StringComparison.OrdinalIgnoreCase) || rawValue == "1")
                value = "true";
            else if (rawValue.Equals("false", StringComparison.OrdinalIgnoreCase) || rawValue == "0")
                value = "false";
            else
                value = rawValue;

            results.Add(new ImportedFlag(normKey, envKey, value, FlagStatus.Direct, null));
        }

        return Task.FromResult<IReadOnlyList<ImportedFlag>>(results);
    }
}
