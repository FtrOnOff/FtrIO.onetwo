using System.Text.Json;
using System.Text.Json.Nodes;

namespace FtrIO.OneTwo;

internal static class AppSettingsWriter
{
    /// <summary>
    /// Merges or overwrites the Toggles section in the specified appsettings.json file.
    /// Writes atomically via a temp file + rename.
    /// </summary>
    internal static void Write(
        string filePath,
        IReadOnlyList<ImportedFlag> flags,
        bool overwrite)
    {
        // Load existing document or create empty
        JsonNode? doc = null;
        if (File.Exists(filePath))
        {
            try
            {
                var text = File.ReadAllText(filePath);
                doc = JsonNode.Parse(text);
            }
            catch (JsonException) { }
        }

        if (doc is null)
            doc = new JsonObject();

        var root = doc.AsObject();

        if (overwrite)
        {
            root.Remove("Toggles");
        }

        if (!root.ContainsKey("Toggles"))
            root["Toggles"] = new JsonObject();

        var toggles = root["Toggles"]!.AsObject();

        foreach (var flag in flags)
        {
            if (flag.Status == FlagStatus.Unsupported || flag.Value is null)
                continue;

            toggles[flag.NormalisedKey] = JsonValue.Create(flag.Value);
        }

        var json = doc.ToJsonString(new JsonSerializerOptions { WriteIndented = true });

        var dir = Path.GetDirectoryName(filePath) ?? Directory.GetCurrentDirectory();
        var tempFile = Path.Combine(dir, Path.GetRandomFileName());
        try
        {
            File.WriteAllText(tempFile, json);
            File.Move(tempFile, filePath, overwrite: true);
        }
        catch
        {
            // Clean up temp file if move failed
            if (File.Exists(tempFile))
                File.Delete(tempFile);
            throw;
        }
    }
}
