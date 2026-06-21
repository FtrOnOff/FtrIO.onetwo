using FluentAssertions;
using FtrIO.OneTwo;
using Xunit;

namespace FtrIO.OneTwo.Tests.Import;

public class MicrosoftFeatureManagementSourceTests
{
    private static string WriteAppsettings(string dir, string json)
    {
        var path = Path.Combine(dir, "appsettings.json");
        File.WriteAllText(path, json);
        return path;
    }

    [Fact]
    public void ReadsSimpleBooleanFlags()
    {
        var dir = Directory.CreateTempSubdirectory("ftrio_msft_test_").FullName;
        var path = WriteAppsettings(dir, """
            {
              "FeatureManagement": {
                "SendWelcomeEmail": true,
                "PaymentV2": false
              }
            }
            """);
        try
        {
            var source = new MicrosoftFeatureManagementSource(path);
            var flags = source.FetchAsync().GetAwaiter().GetResult();

            flags.Count.Should().Be(2);
            flags.Should().Contain(f => f.OriginalKey == "SendWelcomeEmail" && f.Value == "true" && f.Status == FlagStatus.Direct);
            flags.Should().Contain(f => f.OriginalKey == "PaymentV2" && f.Value == "false" && f.Status == FlagStatus.Direct);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void ReadsPercentageRollout()
    {
        var dir = Directory.CreateTempSubdirectory("ftrio_msft_test_").FullName;
        var path = WriteAppsettings(dir, """
            {
              "FeatureManagement": {
                "NewCheckoutFlow": {
                  "EnabledFor": [
                    { "Name": "Percentage", "Parameters": { "Value": 20 } }
                  ]
                }
              }
            }
            """);
        try
        {
            var source = new MicrosoftFeatureManagementSource(path);
            var flags = source.FetchAsync().GetAwaiter().GetResult();

            flags.Count.Should().Be(1);
            flags[0].OriginalKey.Should().Be("NewCheckoutFlow");
            flags[0].Value.Should().Be("20%");
            flags[0].Status.Should().Be(FlagStatus.Direct);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void ApproximatesComplexFilter()
    {
        var dir = Directory.CreateTempSubdirectory("ftrio_msft_test_").FullName;
        var path = WriteAppsettings(dir, """
            {
              "FeatureManagement": {
                "BetaSearch": {
                  "EnabledFor": [
                    { "Name": "BrowserFilter", "Parameters": { "AllowedBrowsers": ["Chrome"] } }
                  ]
                }
              }
            }
            """);
        try
        {
            var source = new MicrosoftFeatureManagementSource(path);
            var flags = source.FetchAsync().GetAwaiter().GetResult();

            flags.Count.Should().Be(1);
            flags[0].Status.Should().Be(FlagStatus.Approximated);
            flags[0].Warning.Should().Contain("BrowserFilter");
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void ReturnsEmptyWhenNoFeatureManagementSection()
    {
        var dir = Directory.CreateTempSubdirectory("ftrio_msft_test_").FullName;
        var path = WriteAppsettings(dir, """{ "ConnectionStrings": {} }""");
        try
        {
            var source = new MicrosoftFeatureManagementSource(path);
            var flags = source.FetchAsync().GetAwaiter().GetResult();
            flags.Should().BeEmpty();
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void ThrowsWhenFileNotFound()
    {
        var source = new MicrosoftFeatureManagementSource("/nonexistent/appsettings.json");
        var act = () => source.FetchAsync().GetAwaiter().GetResult();
        act.Should().Throw<FileNotFoundException>();
    }

    // ── MapEntry unit tests ────────────────────────────────────────────────────

    [Theory]
    [InlineData("true",  "true")]
    [InlineData("false", "false")]
    public void MapEntry_SimpleValue_ReturnsDirect(string input, string expected)
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse($"\"{input}\"")!;
        var result = MicrosoftFeatureManagementSource.MapEntry("MyFlag", node);
        result.Value.Should().Be(expected);
        result.Status.Should().Be(FlagStatus.Direct);
    }
}
