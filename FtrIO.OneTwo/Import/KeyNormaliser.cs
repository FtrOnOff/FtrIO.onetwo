namespace FtrIO.OneTwo;

internal static class KeyNormaliser
{
    /// <summary>
    /// Converts kebab-case keys to PascalCase. Only hyphens are used as split points.
    /// e.g. "new-checkout-flow" -> "NewCheckoutFlow"
    /// </summary>
    internal static string ToPascalCase(string key)
    {
        if (string.IsNullOrEmpty(key))
            return key;

        var parts = key.Split('-');
        var sb = new System.Text.StringBuilder();
        foreach (var part in parts)
        {
            if (part.Length == 0)
                continue;
            sb.Append(char.ToUpperInvariant(part[0]));
            if (part.Length > 1)
                sb.Append(part.Substring(1));
        }
        return sb.ToString();
    }
}
