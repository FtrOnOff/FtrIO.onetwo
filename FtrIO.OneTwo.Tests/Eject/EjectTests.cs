using FluentAssertions;
using FtrIO.OneTwo.Eject;
using Xunit;

namespace FtrIO.OneTwo.Tests.Eject;

public class EjectTests
{
    // ── Key normalisation ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("SendWelcomeEmail", EjectTarget.LaunchDarkly,               "send-welcome-email")]
    [InlineData("NewCheckoutFlow",  EjectTarget.LaunchDarkly,               "new-checkout-flow")]
    [InlineData("PaymentV2",        EjectTarget.LaunchDarkly,               "payment-v2")]
    [InlineData("SendWelcomeEmail", EjectTarget.Flagsmith,                  "send_welcome_email")]
    [InlineData("NewCheckoutFlow",  EjectTarget.Flagsmith,                  "new_checkout_flow")]
    [InlineData("SendWelcomeEmail", EjectTarget.MicrosoftFeatureManagement, "SendWelcomeEmail")]
    [InlineData("NewCheckoutFlow",  EjectTarget.Unleash,                    "new-checkout-flow")]
    public void NormaliseKey_AppliesCorrectConvention(string input, EjectTarget target, string expected)
    {
        EjectTargetHelper.NormaliseKey(input, target).Should().Be(expected);
    }

    // ── Status determination ──────────────────────────────────────────────────

    [Theory]
    [InlineData("true",   EjectTarget.LaunchDarkly,               EjectStatus.Clean)]
    [InlineData("false",  EjectTarget.LaunchDarkly,               EjectStatus.Clean)]
    [InlineData("20%",    EjectTarget.LaunchDarkly,               EjectStatus.Clean)]
    [InlineData("20%",    EjectTarget.Flagsmith,                  EjectStatus.Approximated)]
    [InlineData("20%",    EjectTarget.MicrosoftFeatureManagement, EjectStatus.Clean)]
    [InlineData("20%",    EjectTarget.Unleash,                    EjectStatus.Clean)]
    [InlineData("blue",   EjectTarget.LaunchDarkly,               EjectStatus.Approximated)]
    [InlineData("green",  EjectTarget.Flagsmith,                  EjectStatus.Approximated)]
    [InlineData(null,     EjectTarget.LaunchDarkly,               EjectStatus.Missing)]
    [InlineData(null,     EjectTarget.MicrosoftFeatureManagement, EjectStatus.Missing)]
    public void DetermineStatus_ReturnsCorrectStatus(string? value, EjectTarget target, EjectStatus expected)
    {
        EjectTargetHelper.DetermineStatus(value, target).Should().Be(expected);
    }

    // ── Warning generation ────────────────────────────────────────────────────

    [Fact]
    public void DetermineWarning_NullValue_ReturnsCannotCreateMessage()
    {
        var warning = EjectTargetHelper.DetermineWarning(null, EjectTarget.LaunchDarkly);
        warning.Should().Contain("cannot create");
    }

    [Fact]
    public void DetermineWarning_PercentageOnFlagsmith_ReturnsWarning()
    {
        var warning = EjectTargetHelper.DetermineWarning("50%", EjectTarget.Flagsmith);
        warning.Should().NotBeNull();
        warning.Should().Contain("Flagsmith");
    }

    [Fact]
    public void DetermineWarning_BlueGreen_LaunchDarkly_ReturnsStringFlagWarning()
    {
        var warning = EjectTargetHelper.DetermineWarning("blue", EjectTarget.LaunchDarkly);
        warning.Should().Contain("string flag");
    }

    [Fact]
    public void DetermineWarning_BooleanValue_ReturnsNull()
    {
        EjectTargetHelper.DetermineWarning("true", EjectTarget.LaunchDarkly).Should().BeNull();
        EjectTargetHelper.DetermineWarning("false", EjectTarget.Flagsmith).Should().BeNull();
    }

    // ── Code suggestions ──────────────────────────────────────────────────────

    [Fact]
    public void CodeSuggestion_LaunchDarkly_ContainsBoolVariation()
    {
        var entry = MakeEntry("SendWelcomeEmail", "send-welcome-email", "true", EjectStatus.Clean);
        CodeSuggestion.ForConsole(entry, EjectTarget.LaunchDarkly).Should().Contain("BoolVariation");
    }

    [Fact]
    public void CodeSuggestion_LaunchDarkly_BlueGreen_ContainsStringVariation()
    {
        var entry = MakeEntry("PaymentV2", "payment-v2", "blue", EjectStatus.Approximated);
        CodeSuggestion.ForConsole(entry, EjectTarget.LaunchDarkly).Should().Contain("StringVariation");
    }

    [Fact]
    public void CodeSuggestion_Flagsmith_ContainsHasFeatureFlag()
    {
        var entry = MakeEntry("SendWelcomeEmail", "send_welcome_email", "true", EjectStatus.Clean);
        CodeSuggestion.ForConsole(entry, EjectTarget.Flagsmith).Should().Contain("HasFeatureFlagAsync");
    }

    [Fact]
    public void CodeSuggestion_Microsoft_ContainsFeatureGate()
    {
        var entry = MakeEntry("SendWelcomeEmail", "SendWelcomeEmail", "true", EjectStatus.Clean);
        CodeSuggestion.ForConsole(entry, EjectTarget.MicrosoftFeatureManagement).Should().Contain("[FeatureGate");
    }

    [Fact]
    public void CodeSuggestion_Microsoft_Percentage_ContainsPercentageJson()
    {
        var entry = MakeEntry("NewCheckoutFlow", "NewCheckoutFlow", "20%", EjectStatus.Clean);
        var suggestion = CodeSuggestion.ForMarkdown(entry, EjectTarget.MicrosoftFeatureManagement);
        suggestion.Should().Contain("Percentage");
        suggestion.Should().Contain("20");
    }

    [Fact]
    public void CodeSuggestion_Unleash_ContainsIsEnabled()
    {
        var entry = MakeEntry("SendWelcomeEmail", "send-welcome-email", "true", EjectStatus.Clean);
        CodeSuggestion.ForConsole(entry, EjectTarget.Unleash).Should().Contain("IsEnabled");
    }

    // ── EjectCommand dry run ──────────────────────────────────────────────────

    [Fact]
    public void EjectCommand_DryRun_MissingTo_Returns2()
    {
        var result = EjectCommand.Run(new[] { "--source", "." });
        result.Should().Be(2);
    }

    [Fact]
    public void EjectCommand_DryRun_InvalidTarget_Returns2()
    {
        var result = EjectCommand.Run(new[] { "--to", "unknown" });
        result.Should().Be(2);
    }

    [Fact]
    public void EjectCommand_DryRun_NoToggles_Returns0()
    {
        var dir = Directory.CreateTempSubdirectory("ftrio_eject_test_").FullName;
        File.WriteAllText(Path.Combine(dir, "Empty.cs"), "public class Empty {}");
        try
        {
            var result = EjectCommand.Run(new[] { "--to", "launchdarkly", "--source", dir, "--dry-run" });
            result.Should().Be(0);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void EjectCommand_DryRun_WithMissingToggles_Returns1()
    {
        var dir = Directory.CreateTempSubdirectory("ftrio_eject_test_").FullName;
        File.WriteAllText(Path.Combine(dir, "Service.cs"),
            "public class S { [Toggle] public void SendWelcomeEmail() {} }");
        // No appsettings.json → MISSING
        try
        {
            var result = EjectCommand.Run(new[] { "--to", "launchdarkly", "--source", dir, "--dry-run" });
            result.Should().Be(1);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void EjectCommand_DryRun_AllPresent_Returns0()
    {
        var dir = Directory.CreateTempSubdirectory("ftrio_eject_test_").FullName;
        File.WriteAllText(Path.Combine(dir, "Service.cs"),
            "public class S { [Toggle] public void SendWelcomeEmail() {} }");
        File.WriteAllText(Path.Combine(dir, "appsettings.json"),
            "{\"Toggles\":{\"SendWelcomeEmail\":\"true\"}}");
        try
        {
            var result = EjectCommand.Run(new[] { "--to", "launchdarkly", "--source", dir, "--dry-run" });
            result.Should().Be(0);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void EjectCommand_DryRun_WritesMarkdownFile()
    {
        var dir = Directory.CreateTempSubdirectory("ftrio_eject_test_").FullName;
        File.WriteAllText(Path.Combine(dir, "Service.cs"),
            "public class S { [Toggle] public void SendWelcomeEmail() {} }");
        File.WriteAllText(Path.Combine(dir, "appsettings.json"),
            "{\"Toggles\":{\"SendWelcomeEmail\":\"true\"}}");
        var mdPath = Path.Combine(dir, "eject-report.md");
        try
        {
            EjectCommand.Run(new[] { "--to", "microsoft.featuremanagement", "--source", dir, "--dry-run", "--markdown", mdPath });
            File.Exists(mdPath).Should().BeTrue();
            File.ReadAllText(mdPath).Should().Contain("FtrIO Eject Report");
            File.ReadAllText(mdPath).Should().Contain("SendWelcomeEmail");
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void EjectCommand_DryRun_ExcludeKey_OmitsItFromReport()
    {
        var dir = Directory.CreateTempSubdirectory("ftrio_eject_test_").FullName;
        File.WriteAllText(Path.Combine(dir, "Service.cs"),
            "public class S { [Toggle] public void SendWelcomeEmail() {} [Toggle] public void PaymentV2() {} }");
        File.WriteAllText(Path.Combine(dir, "appsettings.json"),
            "{\"Toggles\":{\"SendWelcomeEmail\":\"true\",\"PaymentV2\":\"false\"}}");
        var mdPath = Path.Combine(dir, "report.md");
        try
        {
            EjectCommand.Run(new[] { "--to", "launchdarkly", "--source", dir, "--dry-run", "--exclude", "PaymentV2", "--markdown", mdPath });
            File.ReadAllText(mdPath).Should().NotContain("PaymentV2");
        }
        finally { Directory.Delete(dir, true); }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static EjectEntry MakeEntry(string ftrioKey, string targetKey, string? value, EjectStatus status) =>
        new EjectEntry(ftrioKey, targetKey, "Service.cs", 10, value, status, null, null);
}
