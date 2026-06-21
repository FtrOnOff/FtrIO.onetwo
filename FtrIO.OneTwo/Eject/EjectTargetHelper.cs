using System.Text;

namespace FtrIO.OneTwo.Eject;

internal static class EjectTargetHelper
{
    internal static EjectTarget Parse(string value) => value.ToLowerInvariant() switch
    {
        "launchdarkly"               => EjectTarget.LaunchDarkly,
        "flagsmith"                  => EjectTarget.Flagsmith,
        "microsoft.featuremanagement"=> EjectTarget.MicrosoftFeatureManagement,
        "unleash"                    => EjectTarget.Unleash,
        _ => throw new ArgumentException(
            $"Unknown target '{value}'. Valid: launchdarkly, flagsmith, microsoft.featuremanagement, unleash")
    };

    internal static string DisplayName(EjectTarget target) => target switch
    {
        EjectTarget.LaunchDarkly               => "LaunchDarkly",
        EjectTarget.Flagsmith                  => "Flagsmith",
        EjectTarget.MicrosoftFeatureManagement => "Microsoft.FeatureManagement",
        EjectTarget.Unleash                    => "Unleash",
        _                                      => target.ToString()
    };

    internal static string ConventionLabel(EjectTarget target) => target switch
    {
        EjectTarget.LaunchDarkly               => "PascalCase → kebab-case",
        EjectTarget.Flagsmith                  => "PascalCase → snake_case",
        EjectTarget.MicrosoftFeatureManagement => "PascalCase (unchanged)",
        EjectTarget.Unleash                    => "PascalCase → kebab-case",
        _                                      => string.Empty
    };

    internal static string NormaliseKey(string pascalKey, EjectTarget target) => target switch
    {
        EjectTarget.LaunchDarkly               => ToKebabCase(pascalKey),
        EjectTarget.Flagsmith                  => ToSnakeCase(pascalKey),
        EjectTarget.MicrosoftFeatureManagement => pascalKey,
        EjectTarget.Unleash                    => ToKebabCase(pascalKey),
        _                                      => pascalKey
    };

    internal static EjectStatus DetermineStatus(string? ftrioValue, EjectTarget target)
    {
        if (ftrioValue is null)
            return EjectStatus.Missing;

        var lower = ftrioValue.ToLowerInvariant();
        bool isPercentage = lower.EndsWith('%');
        bool isBlueGreen  = lower == "blue" || lower == "green";

        if (!isPercentage && !isBlueGreen)
            return EjectStatus.Clean;

        // Percentage support
        if (isPercentage)
        {
            return target switch
            {
                EjectTarget.MicrosoftFeatureManagement => EjectStatus.Clean,
                EjectTarget.LaunchDarkly               => EjectStatus.Clean,
                EjectTarget.Unleash                    => EjectStatus.Clean,
                _                                      => EjectStatus.Approximated  // Flagsmith
            };
        }

        // Blue/green support
        return target switch
        {
            EjectTarget.LaunchDarkly => EjectStatus.Approximated,
            _                        => EjectStatus.Approximated
        };
    }

    internal static string? DetermineWarning(string? ftrioValue, EjectTarget target)
    {
        if (ftrioValue is null)
            return "No value in appsettings.json — cannot create flag. Add a value and re-run, or create manually.";

        var lower = ftrioValue.ToLowerInvariant();

        if (lower.EndsWith('%') && target == EjectTarget.Flagsmith)
            return $"Percentage rollout is not natively supported by Flagsmith. Flag created as disabled.";

        if ((lower == "blue" || lower == "green") && target == EjectTarget.LaunchDarkly)
            return $"Blue/green has no native LaunchDarkly equivalent. Created as string flag with value \"{ftrioValue}\".";

        if ((lower == "blue" || lower == "green") && target != EjectTarget.LaunchDarkly)
            return $"Blue/green is not supported by {DisplayName(target)}. Manual setup required.";

        return null;
    }

    internal static string ToKebabCase(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var sb = new StringBuilder();
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (char.IsUpper(c) && i > 0 && (char.IsLower(s[i - 1]) || char.IsDigit(s[i - 1])))
                sb.Append('-');
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    internal static string ToSnakeCase(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var sb = new StringBuilder();
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (char.IsUpper(c) && i > 0 && (char.IsLower(s[i - 1]) || char.IsDigit(s[i - 1])))
                sb.Append('_');
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }
}
