using System.Text.Json.Nodes;

namespace FtrIO.OneTwo;

internal sealed class FlagdSource : IFlagSource
{
    private readonly string _filePath;

    public FlagdSource(string filePath)
    {
        _filePath = filePath;
    }

    public Task<IReadOnlyList<ImportedFlag>> FetchAsync(CancellationToken cancellationToken = default)
    {
        var text = File.ReadAllText(_filePath);
        var doc = JsonNode.Parse(text);
        var flags = doc?["flags"]?.AsObject();

        var results = new List<ImportedFlag>();
        if (flags is null)
            return Task.FromResult<IReadOnlyList<ImportedFlag>>(results);

        foreach (var (key, flagNode) in flags)
        {
            if (flagNode is null) continue;
            var state = flagNode["state"]?.GetValue<string>();
            var defaultVariant = flagNode["defaultVariant"]?.GetValue<string>();
            var variants = flagNode["variants"]?.AsObject();
            var normKey = KeyNormaliser.ToPascalCase(key);

            if (state != "ENABLED" || defaultVariant is null || variants is null)
            {
                results.Add(new ImportedFlag(normKey, key, "false", FlagStatus.Direct, null));
                continue;
            }

            var variantNode = variants[defaultVariant];
            string value = "false";
            if (variantNode is not null)
            {
                try
                {
                    value = variantNode.GetValue<bool>() ? "true" : "false";
                }
                catch
                {
                    value = variantNode.ToString();
                }
            }

            results.Add(new ImportedFlag(normKey, key, value, FlagStatus.Direct, null));
        }

        return Task.FromResult<IReadOnlyList<ImportedFlag>>(results);
    }
}
