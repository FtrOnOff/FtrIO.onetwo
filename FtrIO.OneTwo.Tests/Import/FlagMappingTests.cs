using System.Text.Json.Nodes;
using FluentAssertions;
using FtrIO.OneTwo;
using Xunit;

namespace FtrIO.OneTwo.Tests.Import;

public class FlagMappingTests
{
    private static JsonNode MakeBoolEnvNode(bool isOn, bool hasRules = false, bool hasTargets = false, bool hasPrereqs = false, int? offVariation = null)
    {
        var node = new JsonObject
        {
            ["fallthrough"] = new JsonObject { ["variation"] = isOn ? 0 : 1 },
            ["variations"] = new JsonArray { JsonValue.Create(true), JsonValue.Create(false) },
            ["rules"] = hasRules ? new JsonArray { new JsonObject() } : new JsonArray(),
            ["targets"] = hasTargets ? new JsonArray { new JsonObject() } : new JsonArray(),
            ["prerequisites"] = hasPrereqs ? new JsonArray { new JsonObject() } : new JsonArray(),
        };
        if (offVariation.HasValue)
            node["offVariation"] = offVariation.Value;
        return node;
    }

    private static JsonNode MakeRolloutEnvNode(int weight0, int weight1)
    {
        var rollout = new JsonObject
        {
            ["variations"] = new JsonArray
            {
                new JsonObject { ["variation"] = 0, ["weight"] = weight0 },
                new JsonObject { ["variation"] = 1, ["weight"] = weight1 },
            }
        };
        return new JsonObject
        {
            ["fallthrough"] = new JsonObject { ["rollout"] = rollout },
            ["variations"] = new JsonArray { JsonValue.Create(true), JsonValue.Create(false) },
            ["rules"] = new JsonArray(),
            ["targets"] = new JsonArray(),
            ["prerequisites"] = new JsonArray(),
        };
    }

    [Fact]
    public void BooleanFlag_On_NoRules_Returns_True_Direct()
    {
        var env = MakeBoolEnvNode(isOn: true);
        var result = LaunchDarklySource.MapFlag("my-flag", "MyFlag", "boolean", env);

        result.Value.Should().Be("true");
        result.Status.Should().Be(FlagStatus.Direct);
        result.Warning.Should().BeNull();
    }

    [Fact]
    public void BooleanFlag_Off_NoRules_Returns_False_Direct()
    {
        var env = MakeBoolEnvNode(isOn: false);
        var result = LaunchDarklySource.MapFlag("my-flag", "MyFlag", "boolean", env);

        result.Value.Should().Be("false");
        result.Status.Should().Be(FlagStatus.Direct);
        result.Warning.Should().BeNull();
    }

    [Fact]
    public void BooleanFlag_20PercentRollout_Returns_Percentage_Direct()
    {
        var env = MakeRolloutEnvNode(20000, 80000);
        var result = LaunchDarklySource.MapFlag("my-flag", "MyFlag", "boolean", env);

        result.Value.Should().Be("20%");
        result.Status.Should().Be(FlagStatus.Direct);
        result.Warning.Should().BeNull();
    }

    [Fact]
    public void BooleanFlag_WithRules_Returns_OffVariation_Approximated()
    {
        var env = MakeBoolEnvNode(isOn: true, hasRules: true, offVariation: 1);
        var result = LaunchDarklySource.MapFlag("my-flag", "MyFlag", "boolean", env);

        result.Value.Should().Be("false"); // off-variation index 1 = false
        result.Status.Should().Be(FlagStatus.Approximated);
        result.Warning.Should().NotBeNull();
    }

    [Fact]
    public void StringFlag_NoRules_Returns_RawString_Direct()
    {
        var env = new JsonObject
        {
            ["fallthrough"] = new JsonObject { ["variation"] = 0 },
            ["variations"] = new JsonArray { JsonValue.Create("red"), JsonValue.Create("blue") },
            ["rules"] = new JsonArray(),
            ["targets"] = new JsonArray(),
        };
        var result = LaunchDarklySource.MapFlag("theme", "Theme", "string", env);

        result.Value.Should().Be("red");
        result.Status.Should().Be(FlagStatus.Direct);
        result.Warning.Should().BeNull();
    }

    [Fact]
    public void NumberFlag_Returns_StringValue_Approximated()
    {
        var env = new JsonObject
        {
            ["fallthrough"] = new JsonObject { ["variation"] = 0 },
            ["variations"] = new JsonArray { JsonValue.Create(42) },
            ["rules"] = new JsonArray(),
            ["targets"] = new JsonArray(),
        };
        var result = LaunchDarklySource.MapFlag("num-flag", "NumFlag", "number", env);

        result.Status.Should().Be(FlagStatus.Approximated);
        result.Warning.Should().NotBeNull();
    }

    [Fact]
    public void JsonFlag_Returns_Unsupported()
    {
        var env = new JsonObject
        {
            ["fallthrough"] = new JsonObject { ["variation"] = 0 },
            ["variations"] = new JsonArray { new JsonObject() },
            ["rules"] = new JsonArray(),
            ["targets"] = new JsonArray(),
        };
        var result = LaunchDarklySource.MapFlag("json-flag", "JsonFlag", "json", env);

        result.Value.Should().BeNull();
        result.Status.Should().Be(FlagStatus.Unsupported);
        result.Warning.Should().NotBeNull();
    }
}
