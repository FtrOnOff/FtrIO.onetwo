using System.Text;

namespace FtrIO.OneTwo.Eject;

internal static class CodeSuggestion
{
    internal static string ForConsole(EjectEntry entry, EjectTarget target)
    {
        var lower = (entry.FtrioValue ?? string.Empty).ToLowerInvariant();
        bool isPercentage = lower.EndsWith('%');
        bool isBlueGreen  = lower == "blue" || lower == "green";

        return target switch
        {
            EjectTarget.LaunchDarkly               => LaunchDarklyConsole(entry, isPercentage, isBlueGreen),
            EjectTarget.Flagsmith                  => FlagsmithConsole(entry),
            EjectTarget.MicrosoftFeatureManagement => MicrosoftConsole(entry, isPercentage),
            EjectTarget.Unleash                    => UnleashConsole(entry),
            _                                      => string.Empty
        };
    }

    internal static string ForMarkdown(EjectEntry entry, EjectTarget target)
    {
        var lower = (entry.FtrioValue ?? string.Empty).ToLowerInvariant();
        bool isPercentage = lower.EndsWith('%');
        bool isBlueGreen  = lower == "blue" || lower == "green";

        return target switch
        {
            EjectTarget.LaunchDarkly               => LaunchDarklyMarkdown(entry, isPercentage, isBlueGreen),
            EjectTarget.Flagsmith                  => FlagsmithMarkdown(entry),
            EjectTarget.MicrosoftFeatureManagement => MicrosoftMarkdown(entry, isPercentage),
            EjectTarget.Unleash                    => UnleashMarkdown(entry),
            _                                      => string.Empty
        };
    }

    // ── LaunchDarkly ─────────────────────────────────────────────────────────

    private static string LaunchDarklyConsole(EjectEntry e, bool isPercentage, bool isBlueGreen)
    {
        if (isBlueGreen)
            return
                $"   Remove [Toggle] from {e.FtrioKey}() declaration.\n" +
                $"   Use StringVariation at the call site:\n" +
                $"     var slot = _ldClient.StringVariation(\"{e.TargetKey}\", context, \"{e.FtrioValue}\");\n" +
                $"     if (slot == _currentDeploymentSlot) {{ {e.FtrioKey}(); }}\n";

        if (isPercentage)
            return
                $"   Remove [Toggle] from {e.FtrioKey}() declaration.\n" +
                $"   Configure {e.FtrioValue} rollout in the LaunchDarkly dashboard.\n" +
                $"   Wrap the call site with BoolVariation:\n" +
                $"     if (_ldClient.BoolVariation(\"{e.TargetKey}\", context, false)) {{ {e.FtrioKey}(); }}\n";

        return
            $"   Remove [Toggle] from {e.FtrioKey}() declaration.\n" +
            $"   Wrap the call site:\n" +
            $"     if (_ldClient.BoolVariation(\"{e.TargetKey}\", context, false)) {{ {e.FtrioKey}(); }}\n";
    }

    private static string LaunchDarklyMarkdown(EjectEntry e, bool isPercentage, bool isBlueGreen)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Remove `[Toggle]` from `{e.FtrioKey}()` declaration.");
        sb.AppendLine();

        if (isBlueGreen)
        {
            sb.AppendLine("**Add at call site (string variation):**");
            sb.AppendLine("```csharp");
            sb.AppendLine($"var slot = _ldClient.StringVariation(\"{e.TargetKey}\", context, \"{e.FtrioValue}\");");
            sb.AppendLine($"if (slot == _currentDeploymentSlot) {{ {e.FtrioKey}(); }}");
            sb.AppendLine("```");
        }
        else if (isPercentage)
        {
            sb.AppendLine($"Configure {e.FtrioValue} rollout in the LaunchDarkly dashboard.");
            sb.AppendLine();
            sb.AppendLine("**Add at call site:**");
            sb.AppendLine("```csharp");
            sb.AppendLine($"if (_ldClient.BoolVariation(\"{e.TargetKey}\", context, false))");
            sb.AppendLine($"    {e.FtrioKey}();");
            sb.AppendLine("```");
        }
        else
        {
            sb.AppendLine("**Add at call site:**");
            sb.AppendLine("```csharp");
            sb.AppendLine($"if (_ldClient.BoolVariation(\"{e.TargetKey}\", context, false))");
            sb.AppendLine($"    {e.FtrioKey}();");
            sb.AppendLine("```");
        }

        sb.AppendLine();
        sb.AppendLine("**Remove from `appsettings.json`:**");
        sb.AppendLine("```json");
        sb.AppendLine($"// Remove: \"{e.FtrioKey}\": \"{e.FtrioValue}\"");
        sb.AppendLine("```");
        return sb.ToString();
    }

    // ── Flagsmith ─────────────────────────────────────────────────────────────

    private static string FlagsmithConsole(EjectEntry e) =>
        $"   Remove [Toggle] from {e.FtrioKey}() declaration.\n" +
        $"   Wrap the call site:\n" +
        $"     if (await _flagsmithClient.HasFeatureFlagAsync(\"{e.TargetKey}\")) {{ {e.FtrioKey}(); }}\n";

    private static string FlagsmithMarkdown(EjectEntry e)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Remove `[Toggle]` from `{e.FtrioKey}()` declaration.");
        sb.AppendLine();
        sb.AppendLine("**Add at call site:**");
        sb.AppendLine("```csharp");
        sb.AppendLine($"if (await _flagsmithClient.HasFeatureFlagAsync(\"{e.TargetKey}\"))");
        sb.AppendLine($"    {e.FtrioKey}();");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("**Remove from `appsettings.json`:**");
        sb.AppendLine("```json");
        sb.AppendLine($"// Remove: \"{e.FtrioKey}\": \"{e.FtrioValue}\"");
        sb.AppendLine("```");
        return sb.ToString();
    }

    // ── Microsoft.FeatureManagement ───────────────────────────────────────────

    private static string MicrosoftConsole(EjectEntry e, bool isPercentage)
    {
        if (isPercentage && int.TryParse((e.FtrioValue ?? string.Empty).TrimEnd('%'), out int pct))
            return
                $"   Replace [Toggle] with [FeatureGate(\"{e.TargetKey}\")] on {e.FtrioKey}().\n" +
                $"   Update appsettings.json — replace Toggles entry with FeatureManagement percentage rollout:\n" +
                $"     \"FeatureManagement\": {{ \"{e.TargetKey}\": {{ \"EnabledFor\": [{{ \"Name\": \"Percentage\", \"Parameters\": {{ \"Value\": {pct} }} }}] }} }}\n";

        return
            $"   Replace [Toggle] with [FeatureGate(\"{e.TargetKey}\")] on {e.FtrioKey}().\n" +
            $"   Update appsettings.json — move from Toggles to FeatureManagement:\n" +
            $"     \"FeatureManagement\": {{ \"{e.TargetKey}\": {(e.FtrioValue ?? "false").ToLowerInvariant()} }}\n" +
            $"   dotnet add package Microsoft.FeatureManagement.AspNetCore\n";
    }

    private static string MicrosoftMarkdown(EjectEntry e, bool isPercentage)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Replace `[Toggle]` with `[FeatureGate(\"{e.TargetKey}\")]` on `{e.FtrioKey}()`.");
        sb.AppendLine();
        sb.AppendLine("**Update `appsettings.json`:**");
        sb.AppendLine("```json");
        sb.AppendLine($"// Remove from Toggles: \"{e.FtrioKey}\": \"{e.FtrioValue}\"");

        if (isPercentage && int.TryParse((e.FtrioValue ?? string.Empty).TrimEnd('%'), out int pct))
        {
            sb.AppendLine($"// Add to FeatureManagement:");
            sb.AppendLine($"\"FeatureManagement\": {{");
            sb.AppendLine($"  \"{e.TargetKey}\": {{");
            sb.AppendLine($"    \"EnabledFor\": [{{ \"Name\": \"Percentage\", \"Parameters\": {{ \"Value\": {pct} }} }}]");
            sb.AppendLine($"  }}");
            sb.AppendLine($"}}");
        }
        else
        {
            sb.AppendLine($"// Add to FeatureManagement:");
            sb.AppendLine($"\"FeatureManagement\": {{ \"{e.TargetKey}\": {(e.FtrioValue ?? "false").ToLowerInvariant()} }}");
        }

        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("**Install NuGet package:**");
        sb.AppendLine("```bash");
        sb.AppendLine("dotnet add package Microsoft.FeatureManagement.AspNetCore");
        sb.AppendLine("```");
        return sb.ToString();
    }

    // ── Unleash ───────────────────────────────────────────────────────────────

    private static string UnleashConsole(EjectEntry e) =>
        $"   Remove [Toggle] from {e.FtrioKey}() declaration.\n" +
        $"   Wrap the call site:\n" +
        $"     if (_unleashClient.IsEnabled(\"{e.TargetKey}\")) {{ {e.FtrioKey}(); }}\n";

    private static string UnleashMarkdown(EjectEntry e)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Remove `[Toggle]` from `{e.FtrioKey}()` declaration.");
        sb.AppendLine();
        sb.AppendLine("**Add at call site:**");
        sb.AppendLine("```csharp");
        sb.AppendLine($"if (_unleashClient.IsEnabled(\"{e.TargetKey}\"))");
        sb.AppendLine($"    {e.FtrioKey}();");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("**Remove from `appsettings.json`:**");
        sb.AppendLine("```json");
        sb.AppendLine($"// Remove: \"{e.FtrioKey}\": \"{e.FtrioValue}\"");
        sb.AppendLine("```");
        return sb.ToString();
    }
}
