using System.Text.Json.Nodes;
using FluentAssertions;
using FtrIO.OneTwo;
using Xunit;

namespace FtrIO.OneTwo.Tests.Import;

public class AppSettingsWriterTests : IDisposable
{
    private readonly string _tempDir;

    public AppSettingsWriterTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string TempFile(string name = "appsettings.json") => Path.Combine(_tempDir, name);

    private static IReadOnlyList<ImportedFlag> MakeFlags(params (string key, string value)[] pairs)
    {
        var list = new List<ImportedFlag>();
        foreach (var (k, v) in pairs)
            list.Add(new ImportedFlag(k, k, v, FlagStatus.Direct, null));
        return list;
    }

    [Fact]
    public void Merge_PreservesExistingKey_AddsNewKey_UpdatesExistingKey()
    {
        // Arrange: existing file with one toggle
        var filePath = TempFile();
        File.WriteAllText(filePath, """
            {
              "Toggles": {
                "OldFeature": "true",
                "ExistingFeature": "false"
              }
            }
            """);

        var flags = MakeFlags(("ExistingFeature", "true"), ("NewFeature", "false"));

        // Act
        AppSettingsWriter.Write(filePath, flags, overwrite: false);

        // Assert
        var text = File.ReadAllText(filePath);
        var doc = JsonNode.Parse(text)!;
        var toggles = doc["Toggles"]!.AsObject();

        toggles["OldFeature"]!.GetValue<string>().Should().Be("true");   // preserved
        toggles["ExistingFeature"]!.GetValue<string>().Should().Be("true"); // updated
        toggles["NewFeature"]!.GetValue<string>().Should().Be("false");   // new key added
    }

    [Fact]
    public void Overwrite_ReplacesEntireTogglesSection()
    {
        var filePath = TempFile();
        File.WriteAllText(filePath, """
            {
              "Toggles": {
                "OldKey": "true"
              },
              "ConnectionStrings": {
                "Default": "server=localhost"
              }
            }
            """);

        var flags = MakeFlags(("NewKey", "false"));

        AppSettingsWriter.Write(filePath, flags, overwrite: true);

        var text = File.ReadAllText(filePath);
        var doc = JsonNode.Parse(text)!;
        var toggles = doc["Toggles"]!.AsObject();

        toggles.ContainsKey("OldKey").Should().BeFalse();
        toggles["NewKey"]!.GetValue<string>().Should().Be("false");

        // Non-toggles keys are untouched
        doc["ConnectionStrings"]!["Default"]!.GetValue<string>().Should().Be("server=localhost");
    }

    [Fact]
    public void CreatesFile_IfMissing()
    {
        var filePath = TempFile("new-settings.json");
        File.Exists(filePath).Should().BeFalse();

        var flags = MakeFlags(("MyFlag", "true"));
        AppSettingsWriter.Write(filePath, flags, overwrite: false);

        File.Exists(filePath).Should().BeTrue();
        var text = File.ReadAllText(filePath);
        var doc = JsonNode.Parse(text)!;
        doc["Toggles"]!["MyFlag"]!.GetValue<string>().Should().Be("true");
    }

    [Fact]
    public void DoesNotTouch_NonTogglesKeys()
    {
        var filePath = TempFile();
        File.WriteAllText(filePath, """
            {
              "Logging": { "Level": "Warning" },
              "Toggles": {}
            }
            """);

        var flags = MakeFlags(("AFlag", "true"));
        AppSettingsWriter.Write(filePath, flags, overwrite: false);

        var text = File.ReadAllText(filePath);
        var doc = JsonNode.Parse(text)!;
        doc["Logging"]!["Level"]!.GetValue<string>().Should().Be("Warning");
    }
}
