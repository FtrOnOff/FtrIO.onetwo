using FluentAssertions;
using FtrIO.OneTwo;
using Xunit;

namespace FtrIO.OneTwo.Tests.Migrate;

public class MigrationCrossReferenceTests
{
    private static SdkScanner.SdkCallEntry CodeEntry(string key, string method = "BoolVariation") =>
        new SdkScanner.SdkCallEntry(key, method, "MyClass.cs", 10);

    private static Dictionary<string, ApiFlagInfo> ApiFlags(params (string key, string kind, bool hasTargeting)[] flags)
    {
        var dict = new Dictionary<string, ApiFlagInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, kind, hasTargeting) in flags)
            dict[key] = new ApiFlagInfo(kind, hasTargeting, kind == "boolean" ? "true" : "value");
        return dict;
    }

    [Fact]
    public void Flag_InBoth_Boolean_NoTargeting_IsReadyToMigrate()
    {
        var code = new List<SdkScanner.SdkCallEntry> { CodeEntry("my-flag") };
        var api = ApiFlags(("my-flag", "boolean", false));

        var result = MigrationCrossReference.CrossReference(code, api, null);

        result.Should().HaveCount(1);
        result[0].Status.Should().Be(MigrationStatus.ReadyToMigrate);
    }

    [Fact]
    public void Flag_InBoth_Boolean_WithTargeting_IsNeedsReview()
    {
        var code = new List<SdkScanner.SdkCallEntry> { CodeEntry("my-flag") };
        var api = ApiFlags(("my-flag", "boolean", true));

        var result = MigrationCrossReference.CrossReference(code, api, null);

        result.Should().HaveCount(1);
        result[0].Status.Should().Be(MigrationStatus.NeedsReview);
    }

    [Fact]
    public void Flag_InApiOnly_IsStaleFlag()
    {
        var code = new List<SdkScanner.SdkCallEntry>(); // empty — not in code
        var api = ApiFlags(("stale-flag", "boolean", false));

        var result = MigrationCrossReference.CrossReference(code, api, null);

        result.Should().HaveCount(1);
        result[0].FlagKey.Should().Be("stale-flag");
        result[0].Status.Should().Be(MigrationStatus.StaleFlag);
    }

    [Fact]
    public void Flag_InCodeOnly_IsDeletedFlag()
    {
        var code = new List<SdkScanner.SdkCallEntry> { CodeEntry("deleted-flag") };
        var api = new Dictionary<string, ApiFlagInfo>(); // empty — not in API

        var result = MigrationCrossReference.CrossReference(code, api, null);

        result.Should().HaveCount(1);
        result[0].FlagKey.Should().Be("deleted-flag");
        result[0].Status.Should().Be(MigrationStatus.DeletedFlag);
    }

    [Fact]
    public void Flag_InBoth_Json_IsCannotMigrate()
    {
        var code = new List<SdkScanner.SdkCallEntry> { CodeEntry("json-flag", "JsonVariation") };
        var api = ApiFlags(("json-flag", "json", false));

        var result = MigrationCrossReference.CrossReference(code, api, null);

        result.Should().HaveCount(1);
        result[0].Status.Should().Be(MigrationStatus.CannotMigrate);
    }
}
