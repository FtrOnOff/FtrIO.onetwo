using FluentAssertions;
using FtrIO.OneTwo;
using Xunit;

namespace FtrIO.OneTwo.Tests.Migrate;

public class SdkScannerMicrosoftTests
{
    [Fact]
    public void DetectsFeatureGateAttribute()
    {
        var source = """
            using Microsoft.FeatureManagement.Mvc;
            public class EmailController
            {
                [FeatureGate("SendWelcomeEmail")]
                public IActionResult SendEmail() => Ok();
            }
            """;

        var results = SdkScanner.ScanSource(source, "EmailController.cs");

        results.Count.Should().Be(1);
        results[0].FlagKey.Should().Be("SendWelcomeEmail");
        results[0].SdkMethod.Should().Be("FeatureGate");
    }

    [Fact]
    public void DetectsIsEnabledAsync()
    {
        var source = """
            public class OrderService
            {
                public async Task PlaceOrder()
                {
                    if (await _featureManager.IsEnabledAsync("NewCheckoutFlow"))
                        UseNewFlow();
                }
            }
            """;

        var results = SdkScanner.ScanSource(source, "OrderService.cs");

        results.Count.Should().Be(1);
        results[0].FlagKey.Should().Be("NewCheckoutFlow");
        results[0].SdkMethod.Should().Be("IsEnabledAsync");
    }

    [Fact]
    public void DetectsIsEnabled()
    {
        var source = """
            public class PaymentService
            {
                public void Process()
                {
                    if (_featureManager.IsEnabled("PaymentV2"))
                        UseV2();
                }
            }
            """;

        var results = SdkScanner.ScanSource(source, "PaymentService.cs");

        results.Count.Should().Be(1);
        results[0].FlagKey.Should().Be("PaymentV2");
        results[0].SdkMethod.Should().Be("IsEnabled");
    }

    [Fact]
    public void DetectsMixedMicrosoftPatterns()
    {
        var source = """
            public class Mixed
            {
                [FeatureGate("SendWelcomeEmail")]
                public void Welcome() {}

                public async Task Process()
                {
                    if (await _fm.IsEnabledAsync("NewCheckoutFlow")) {}
                    if (_fm.IsEnabled("PaymentV2")) {}
                }
            }
            """;

        var results = SdkScanner.ScanSource(source, "Mixed.cs");

        results.Count.Should().Be(3);
        results.Should().Contain(r => r.FlagKey == "SendWelcomeEmail" && r.SdkMethod == "FeatureGate");
        results.Should().Contain(r => r.FlagKey == "NewCheckoutFlow"  && r.SdkMethod == "IsEnabledAsync");
        results.Should().Contain(r => r.FlagKey == "PaymentV2"        && r.SdkMethod == "IsEnabled");
    }

    [Fact]
    public void DoesNotDetectIsEnabledWithoutStringLiteral()
    {
        var source = """
            public class Service
            {
                public void Run()
                {
                    var key = "DynamicKey";
                    if (_fm.IsEnabled(key)) {}
                }
            }
            """;

        var results = SdkScanner.ScanSource(source, "Service.cs");
        results.Should().BeEmpty();
    }

    [Fact]
    public void DetectsAlongsideExistingLaunchDarklyPatterns()
    {
        var source = """
            public class Service
            {
                public void Run()
                {
                    if (_ld.BoolVariation("ld-flag", ctx, false)) {}
                    if (_fm.IsEnabled("msft-flag")) {}
                }
            }
            """;

        var results = SdkScanner.ScanSource(source, "Service.cs");

        results.Count.Should().Be(2);
        results.Should().Contain(r => r.SdkMethod == "BoolVariation");
        results.Should().Contain(r => r.SdkMethod == "IsEnabled");
    }
}
