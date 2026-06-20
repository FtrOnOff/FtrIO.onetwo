namespace FtrIO.OneTwo;

internal static class MarkdownRenderer
{
    internal static string FormatState(string? state) => state switch
    {
        null                                                                               => "MISSING",
        _ when state.Equals("true", StringComparison.OrdinalIgnoreCase) || state == "1"  => "ON",
        _ when state.Equals("false", StringComparison.OrdinalIgnoreCase) || state == "0" => "OFF",
        _ when state.EndsWith('%')                                                         => state,
        _ when state.Equals("blue", StringComparison.OrdinalIgnoreCase) ||
               state.Equals("green", StringComparison.OrdinalIgnoreCase)                  => state.ToUpperInvariant(),
        _                                                                                  => state
    };
}
