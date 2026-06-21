using System.Text.Json.Nodes;

namespace FtrIO.OneTwo;

internal sealed class FlagsmithSource : IFlagSource
{
    private readonly string _apiKey;
    private readonly string _env;

    public FlagsmithSource(string apiKey, string env)
    {
        _apiKey = apiKey;
        _env = env;
    }

    public async Task<IReadOnlyList<ImportedFlag>> FetchAsync(CancellationToken cancellationToken = default)
    {
        using var client = new System.Net.Http.HttpClient();
        client.DefaultRequestHeaders.Add("X-Environment-Key", _apiKey);

        var url = $"https://api.flagsmith.com/api/v1/features/?environment={_env}";
        var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Flagsmith API returned {(int)response.StatusCode}: {response.ReasonPhrase}");

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var doc = JsonNode.Parse(body);
        var results2 = doc?["results"]?.AsArray();

        if (results2 is null)
            return new List<ImportedFlag>();

        var results = new List<ImportedFlag>();
        foreach (var item in results2)
        {
            if (item is null) continue;
            var name = item["feature"]?["name"]?.GetValue<string>() ?? string.Empty;
            var enabled = item["enabled"]?.GetValue<bool>() ?? false;
            var stateValue = item["feature_state_value"];
            var normKey = KeyNormaliser.ToPascalCase(name);

            string value;
            if (stateValue is not null && stateValue.ToJsonString() != "null")
                value = stateValue.GetValue<object>().ToString() ?? string.Empty;
            else
                value = enabled ? "true" : "false";

            results.Add(new ImportedFlag(normKey, name, value, FlagStatus.Direct, null));
        }

        return results;
    }
}
