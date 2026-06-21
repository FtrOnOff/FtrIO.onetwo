using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FtrIO.OneTwo;

internal sealed class LaunchDarklySource : IFlagSource
{
    private readonly string _apiKey;
    private readonly string _projectKey;
    private readonly string _env;

    public LaunchDarklySource(string apiKey, string projectKey, string env)
    {
        _apiKey = apiKey;
        _projectKey = projectKey;
        _env = env;
    }

    public async Task<IReadOnlyList<ImportedFlag>> FetchAsync(CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Authorization", _apiKey);

        var url = $"https://app.launchdarkly.com/api/v2/flags/{_projectKey}?env={_env}";
        var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"LaunchDarkly API returned {(int)response.StatusCode}: {response.ReasonPhrase}");

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var doc = JsonNode.Parse(body);
        var items = doc?["items"]?.AsArray();

        if (items is null)
            return new List<ImportedFlag>();

        var results = new List<ImportedFlag>();
        foreach (var item in items)
        {
            if (item is null) continue;
            var key = item["key"]?.GetValue<string>() ?? string.Empty;
            var kind = item["kind"]?.GetValue<string>() ?? string.Empty;
            var normKey = KeyNormaliser.ToPascalCase(key);

            var envNode = item["environments"]?[_env];
            if (envNode is null) continue;

            var mapped = MapFlag(key, normKey, kind, envNode);
            results.Add(mapped);
        }

        return results;
    }

    internal static ImportedFlag MapFlag(string originalKey, string normKey, string kind, JsonNode envNode)
    {
        if (kind == "json")
        {
            return new ImportedFlag(normKey, originalKey, null, FlagStatus.Unsupported,
                $"Flag '{originalKey}' is of kind 'json' and cannot be mapped to a toggle value.");
        }

        if (kind == "number")
        {
            // Get off-variation value or fallthrough
            var fallthroughVar = envNode["fallthrough"]?["variation"]?.GetValue<int>();
            var variations = envNode["variations"]?.AsArray();
            string? numVal = null;
            if (fallthroughVar.HasValue && variations != null && fallthroughVar.Value < variations.Count)
                numVal = variations[fallthroughVar.Value]?.ToString();
            return new ImportedFlag(normKey, originalKey, numVal ?? "0", FlagStatus.Approximated,
                $"Flag '{originalKey}' is of kind 'number'. Using string representation.");
        }

        if (kind == "string")
        {
            var rules = envNode["rules"]?.AsArray();
            var targets = envNode["targets"]?.AsArray();
            bool hasComplexity = (rules?.Count ?? 0) > 0 || (targets?.Count ?? 0) > 0;

            var fallthroughVar = envNode["fallthrough"]?["variation"]?.GetValue<int>();
            var variations = envNode["variations"]?.AsArray();
            string? strVal = null;
            if (fallthroughVar.HasValue && variations != null && fallthroughVar.Value < variations.Count)
                strVal = variations[fallthroughVar.Value]?.GetValue<string>();

            if (hasComplexity)
                return new ImportedFlag(normKey, originalKey, strVal ?? string.Empty, FlagStatus.Approximated,
                    $"Flag '{originalKey}' has targeting rules. Using fallthrough value.");

            return new ImportedFlag(normKey, originalKey, strVal ?? string.Empty, FlagStatus.Direct, null);
        }

        // boolean
        {
            var rules = envNode["rules"]?.AsArray();
            var targets = envNode["targets"]?.AsArray();
            var prerequisites = envNode["prerequisites"]?.AsArray();
            bool hasComplexity = (rules?.Count ?? 0) > 0
                || (targets?.Count ?? 0) > 0
                || (prerequisites?.Count ?? 0) > 0;

            if (hasComplexity)
            {
                // Use off-variation
                var offVar = envNode["offVariation"]?.GetValue<int>();
                var variations = envNode["variations"]?.AsArray();
                string offVal = "false";
                if (offVar.HasValue && variations != null && offVar.Value < variations.Count)
                {
                    var v = variations[offVar.Value];
                    offVal = v?.GetValue<bool>() == true ? "true" : "false";
                }
                return new ImportedFlag(normKey, originalKey, offVal, FlagStatus.Approximated,
                    $"Flag '{originalKey}' has targeting rules/targets/prerequisites. Using off-variation value.");
            }

            // Check for weighted rollout
            var fallthroughNode = envNode["fallthrough"];
            var rolloutNode = fallthroughNode?["rollout"];
            if (rolloutNode is not null)
            {
                var weightVariations = rolloutNode["variations"]?.AsArray();
                if (weightVariations != null && weightVariations.Count == 2)
                {
                    // Check weights sum to 100000
                    int w0 = weightVariations[0]?["weight"]?.GetValue<int>() ?? 0;
                    int w1 = weightVariations[1]?["weight"]?.GetValue<int>() ?? 0;
                    if (w0 + w1 == 100000)
                    {
                        // Return percentage for first variant (on)
                        string pct = $"{w0 / 1000}%";
                        return new ImportedFlag(normKey, originalKey, pct, FlagStatus.Direct, null);
                    }
                }
            }

            // Simple fallthrough to variation
            var ftVar = fallthroughNode?["variation"]?.GetValue<int>();
            var vars = envNode["variations"]?.AsArray();
            bool isOn = false;
            if (ftVar.HasValue && vars != null && ftVar.Value < vars.Count)
            {
                var varNode = vars[ftVar.Value];
                // variations can be bool or object wrapping bool
                try { isOn = varNode?.GetValue<bool>() ?? false; } catch { }
            }

            return new ImportedFlag(normKey, originalKey, isOn ? "true" : "false", FlagStatus.Direct, null);
        }
    }
}
