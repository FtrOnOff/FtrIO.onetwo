using System.Text.Json.Nodes;

namespace FtrIO.OneTwo;

internal sealed class HttpSource : IFlagSource
{
    private readonly string _url;

    public HttpSource(string url)
    {
        _url = url;
    }

    public async Task<IReadOnlyList<ImportedFlag>> FetchAsync(CancellationToken cancellationToken = default)
    {
        using var client = new System.Net.Http.HttpClient();
        var response = await client.GetAsync(_url, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"HTTP source returned {(int)response.StatusCode}: {response.ReasonPhrase}");

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var doc = JsonNode.Parse(body);
        var toggles = doc?["Toggles"]?.AsObject();

        var results = new List<ImportedFlag>();
        if (toggles is null)
            return results;

        foreach (var (key, value) in toggles)
        {
            var strVal = value?.GetValue<object>().ToString() ?? string.Empty;
            results.Add(new ImportedFlag(key, key, strVal, FlagStatus.Direct, null));
        }

        return results;
    }
}
