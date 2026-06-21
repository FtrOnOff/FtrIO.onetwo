using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;

namespace FtrIO.OneTwo.Eject;

internal static class EjectApiClient
{
    // Returns "Created", "AlreadyExists", or throws on failure.
    internal static string CreateFlag(
        EjectTarget target,
        string apiKey,
        string? project,
        string? env,
        EjectEntry entry)
    {
        return target switch
        {
            EjectTarget.LaunchDarkly               => CreateLaunchDarkly(apiKey, project!, env!, entry),
            EjectTarget.Flagsmith                  => CreateFlagsmith(apiKey, project!, entry),
            EjectTarget.MicrosoftFeatureManagement => "NoApiRequired",
            EjectTarget.Unleash                    => CreateUnleash(apiKey, project, entry),
            _                                      => throw new ArgumentException($"Unknown target {target}")
        };
    }

    private static string CreateLaunchDarkly(string apiKey, string project, string env, EjectEntry entry)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Authorization", apiKey);

        var lower = (entry.FtrioValue ?? string.Empty).ToLowerInvariant();
        bool isBlueGreen = lower == "blue" || lower == "green";
        string kind = isBlueGreen ? "string" : "boolean";

        JsonObject body;
        if (isBlueGreen)
        {
            body = new JsonObject
            {
                ["key"]  = entry.TargetKey,
                ["name"] = entry.FtrioKey,
                ["kind"] = "string",
                ["variations"] = new JsonArray(
                    new JsonObject { ["value"] = "blue" },
                    new JsonObject { ["value"] = "green" }
                ),
                ["defaults"] = new JsonObject { ["onVariation"] = 0, ["offVariation"] = 1 }
            };
        }
        else
        {
            bool isOn = IsOn(entry.FtrioValue);
            body = new JsonObject
            {
                ["key"]      = entry.TargetKey,
                ["name"]     = entry.FtrioKey,
                ["kind"]     = "boolean",
                ["defaults"] = new JsonObject
                {
                    ["onVariation"]  = isOn ? 0 : 1,
                    ["offVariation"] = isOn ? 1 : 0
                }
            };
        }

        var content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        var response = client.PostAsync($"https://app.launchdarkly.com/api/v2/flags/{project}", content)
            .GetAwaiter().GetResult();

        if (response.StatusCode == System.Net.HttpStatusCode.Conflict ||
            response.StatusCode == System.Net.HttpStatusCode.UnprocessableEntity)
            return "AlreadyExists";

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"LaunchDarkly API returned {(int)response.StatusCode}: {response.ReasonPhrase}");

        // For percentage rollout, the flag is created as boolean — rollout is configured in dashboard
        return "Created";
    }

    private static string CreateFlagsmith(string apiKey, string project, EjectEntry entry)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Token {apiKey}");

        var lower = (entry.FtrioValue ?? string.Empty).ToLowerInvariant();
        bool enabled = lower == "true" || lower == "1";

        var body = new JsonObject
        {
            ["name"]            = entry.TargetKey,
            ["default_enabled"] = enabled,
            ["initial_value"]   = (JsonNode?)null
        };

        var content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        var response = client.PostAsync(
                $"https://api.flagsmith.com/api/v1/projects/{project}/features/", content)
            .GetAwaiter().GetResult();

        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            return "AlreadyExists";

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Flagsmith API returned {(int)response.StatusCode}: {response.ReasonPhrase}");

        return "Created";
    }

    private static string CreateUnleash(string apiKey, string? project, EjectEntry entry)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Authorization", apiKey);

        var lower = (entry.FtrioValue ?? string.Empty).ToLowerInvariant();
        bool isPercentage = lower.EndsWith('%');
        bool enabled = lower == "true" || lower == "1";

        JsonArray strategies;
        if (isPercentage && int.TryParse(lower.TrimEnd('%'), out int pct))
        {
            strategies = new JsonArray(
                new JsonObject
                {
                    ["name"] = "gradualRolloutRandom",
                    ["parameters"] = new JsonObject { ["percentage"] = pct.ToString() }
                });
        }
        else
        {
            strategies = new JsonArray(new JsonObject { ["name"] = "default" });
        }

        var body = new JsonObject
        {
            ["name"]       = entry.TargetKey,
            ["type"]       = "release",
            ["enabled"]    = enabled,
            ["strategies"] = strategies
        };

        if (!string.IsNullOrEmpty(project))
            body["project"] = project;

        var content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        var response = client.PostAsync("https://unleash.example.com/api/admin/features", content)
            .GetAwaiter().GetResult();

        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            return "AlreadyExists";

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Unleash API returned {(int)response.StatusCode}: {response.ReasonPhrase}");

        return "Created";
    }

    private static bool IsOn(string? value) =>
        value is not null &&
        (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1");
}
