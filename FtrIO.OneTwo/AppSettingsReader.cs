using System.Text.Json;
using System.Text.Json.Nodes;

namespace FtrIO.OneTwo;

internal record EnvironmentResult(string DisplayName, string FilePath, Dictionary<string, string> Toggles);

internal static class AppSettingsReader
{
    /// <summary>
    /// Returns one resolved toggle set per unique environment name found across the tree.
    /// When the same environment name appears in multiple directories (e.g. src and bin),
    /// the first occurrence wins. DisplayName is the environment name (e.g. "Staging")
    /// or "appsettings.json" for the base file.
    /// </summary>
    internal static IReadOnlyList<EnvironmentResult> ReadAll(string projectRoot)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<EnvironmentResult>();

        foreach (var file in FindAppSettingsFiles(projectRoot))
        {
            var name = DeriveName(file);
            if (!seen.Add(name)) continue;

            var toggles = ReadFile(file, out _);
            results.Add(new EnvironmentResult(name, file, toggles));
        }

        return results;
    }

    /// <summary>
    /// Returns a single resolved toggle set using FtrIO's overlay model:
    /// env-specific values win, base fills the gaps.
    /// </summary>
    internal static EnvironmentResult ReadForEnv(string projectRoot, string envName)
    {
        var resolved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? foundOverlayPath = null;

        foreach (var baseFile in FindAppSettingsFiles(projectRoot)
                     .Where(f => Path.GetFileName(f).Equals("appsettings.json", StringComparison.OrdinalIgnoreCase)))
        {
            var baseToggles = ReadFile(baseFile, out _);
            var dir = Path.GetDirectoryName(baseFile)!;
            var overlayFile = Path.Combine(dir, $"appsettings.{envName}.json");
            var overlayToggles = File.Exists(overlayFile) ? ReadFile(overlayFile, out _) : [];

            if (File.Exists(overlayFile)) foundOverlayPath ??= overlayFile;

            foreach (var (k, v) in overlayToggles) resolved.TryAdd(k, v);
            foreach (var (k, v) in baseToggles) resolved.TryAdd(k, v);
        }

        var filePath = foundOverlayPath ?? $"appsettings.{envName}.json (not found)";
        return new EnvironmentResult(envName, filePath, resolved);
    }

    /// <summary>
    /// Auto-detects the active environment from FtrIO:Environment in the base file,
    /// then applies the overlay. Falls back to all-environments mode if none is set.
    /// </summary>
    internal static (EnvironmentResult result, bool overlayApplied) ReadAutoDetected(string projectRoot)
    {
        // Look for FtrIO:Environment in any base appsettings.json
        foreach (var baseFile in FindAppSettingsFiles(projectRoot)
                     .Where(f => Path.GetFileName(f).Equals("appsettings.json", StringComparison.OrdinalIgnoreCase)))
        {
            ReadFile(baseFile, out var detectedEnv);
            if (detectedEnv is not null)
                return (ReadForEnv(projectRoot, detectedEnv), true);
        }

        // No environment configured — return base-only merged across all base files
        var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var baseFile in FindAppSettingsFiles(projectRoot)
                     .Where(f => Path.GetFileName(f).Equals("appsettings.json", StringComparison.OrdinalIgnoreCase)))
        {
            foreach (var (k, v) in ReadFile(baseFile, out _))
                merged.TryAdd(k, v);
        }

        return (new EnvironmentResult("appsettings.json", "appsettings.json", merged), false);
    }

    internal static string DeriveName(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        if (fileName.Equals("appsettings.json", StringComparison.OrdinalIgnoreCase))
            return "appsettings.json";

        // appsettings.Development.json → Development
        var withoutJson = Path.GetFileNameWithoutExtension(fileName); // appsettings.Development
        var prefix = "appsettings.";
        return withoutJson.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? withoutJson[prefix.Length..]
            : fileName;
    }

    private static IEnumerable<string> FindAppSettingsFiles(string projectRoot) =>
        Directory.EnumerateFiles(projectRoot, "appsettings*.json", SearchOption.AllDirectories);

    internal static Dictionary<string, string> ReadFile(string path, out string? ftrioEnvironment)
    {
        ftrioEnvironment = null;
        var results = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var text = File.ReadAllText(path);
            var doc = JsonNode.Parse(text);

            ftrioEnvironment = doc?["FtrIO"]?["Environment"]?.GetValue<string>();

            var toggles = doc?["Toggles"]?.AsObject();
            if (toggles is null) return results;

            foreach (var (key, value) in toggles)
            {
                if (value is null) continue;
                var raw = value.GetValue<object>().ToString();
                if (raw is not null)
                    results[key] = raw;
            }
        }
        catch (JsonException) { }

        return results;
    }
}
