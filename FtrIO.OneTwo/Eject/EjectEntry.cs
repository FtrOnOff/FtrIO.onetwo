namespace FtrIO.OneTwo.Eject;

public enum EjectStatus { Clean, Approximated, Missing, ApiError }
public enum EjectTarget { LaunchDarkly, Flagsmith, MicrosoftFeatureManagement, Unleash }

internal record EjectEntry(
    string FtrioKey,       // original PascalCase key
    string TargetKey,      // normalised for the target system
    string File,
    int Line,
    string? FtrioValue,    // from appsettings.json; null = MISSING
    EjectStatus Status,
    string? Warning,
    string? ApiResult      // "Created", "AlreadyExists", null (dry run / no api key), "Failed: ..."
);
