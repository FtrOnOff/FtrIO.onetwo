using System.Text.Json;
using System.Text.Json.Nodes;

namespace FtrIO.OneTwo;

internal static class AppSettingsReader
{
    internal static Dictionary<string, bool> ReadToggles(string projectRoot)
    {
        var results = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in Directory.EnumerateFiles(projectRoot, "appsettings*.json", SearchOption.AllDirectories))
        {
            try
            {
                var text = File.ReadAllText(file);
                var doc = JsonNode.Parse(text);
                var toggles = doc?["Toggles"]?.AsObject();
                if (toggles is null) continue;

                foreach (var (key, value) in toggles)
                {
                    if (value is JsonValue jv && jv.TryGetValue<bool>(out var b))
                        results.TryAdd(key, b);
                }
            }
            catch (JsonException) { }
        }

        return results;
    }
}
