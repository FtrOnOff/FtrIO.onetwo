using FluentAssertions;
using FtrIO.OneTwo;
using Xunit;

namespace FtrIO.OneTwo.Tests.Migrate;

public class SdkScannerTests
{
    [Fact]
    public void Detects_BoolVariation_Call()
    {
        var source = """
            public class MyClass {
                void Method() {
                    var result = client.BoolVariation("my-flag", user, false);
                }
            }
            """;

        var entries = SdkScanner.ScanSource(source, "MyClass.cs");

        entries.Should().HaveCount(1);
        entries[0].FlagKey.Should().Be("my-flag");
        entries[0].SdkMethod.Should().Be("BoolVariation");
    }

    [Fact]
    public void Detects_StringVariation_Call()
    {
        var source = """
            public class MyClass {
                void Method() {
                    var result = client.StringVariation("str-flag", user, "x");
                }
            }
            """;

        var entries = SdkScanner.ScanSource(source, "MyClass.cs");

        entries.Should().HaveCount(1);
        entries[0].FlagKey.Should().Be("str-flag");
        entries[0].SdkMethod.Should().Be("StringVariation");
    }

    [Fact]
    public void Ignores_Calls_WithoutStringLiteralFirstArg()
    {
        var source = """
            public class MyClass {
                void Method(string flagKey) {
                    var result = client.BoolVariation(flagKey, user, false);
                }
            }
            """;

        var entries = SdkScanner.ScanSource(source, "MyClass.cs");

        entries.Should().BeEmpty();
    }

    [Fact]
    public void Detects_Multiple_Calls_InSameFile()
    {
        var source = """
            public class MyClass {
                void Method() {
                    var a = client.BoolVariation("flag-one", user, false);
                    var b = client.StringVariation("flag-two", user, "default");
                    var c = client.IntVariation("flag-three", user, 0);
                }
            }
            """;

        var entries = SdkScanner.ScanSource(source, "MyClass.cs");

        entries.Should().HaveCount(3);
        entries.Select(e => e.FlagKey).Should().Contain(new[] { "flag-one", "flag-two", "flag-three" });
    }

    [Fact]
    public void Detects_Flagsmith_HasFeatureFlagAsync()
    {
        var source = """
            public class MyClass {
                async Task Method() {
                    var result = await flagsmithClient.HasFeatureFlagAsync("flag-key");
                }
            }
            """;

        var entries = SdkScanner.ScanSource(source, "MyClass.cs");

        entries.Should().HaveCount(1);
        entries[0].FlagKey.Should().Be("flag-key");
        entries[0].SdkMethod.Should().Be("HasFeatureFlagAsync");
    }
}
