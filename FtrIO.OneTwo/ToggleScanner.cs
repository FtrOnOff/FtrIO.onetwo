using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FtrIO.OneTwo;

internal static class ToggleScanner
{
    private static readonly HashSet<string> AttributeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Toggle", "ToggleAttribute", "ToggleAsync", "ToggleAsyncAttribute"
    };

    private static readonly HashSet<string> ManualCallNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "ExecuteMethodIfToggleOn", "ExecuteMethodIfToggleOnAsync"
    };

    internal static IReadOnlyList<ToggleEntry> Scan(
        string projectRoot,
        Dictionary<string, bool> toggleStates)
    {
        var entries = new List<ToggleEntry>();

        var skipDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "bin", "obj", ".git", "node_modules" };

        foreach (var csFile in Directory.EnumerateFiles(projectRoot, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                         .Any(seg => skipDirs.Contains(seg))))
        {
            var text = File.ReadAllText(csFile);
            var tree = CSharpSyntaxTree.ParseText(text, path: csFile);
            var root = tree.GetRoot();
            var relPath = Path.GetRelativePath(projectRoot, csFile);

            // [Toggle] / [ToggleAsync] on method declarations
            foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                foreach (var attrList in method.AttributeLists)
                {
                    foreach (var attr in attrList.Attributes)
                    {
                        var name = attr.Name.ToString().Split('.').Last();
                        if (!AttributeNames.Contains(name)) continue;

                        var line = tree.GetLineSpan(method.Span).StartLinePosition.Line + 1;
                        var key = method.Identifier.Text;
                        entries.Add(new ToggleEntry(
                            key, key, relPath, line,
                            ToggleSource.Attribute,
                            toggleStates.TryGetValue(key, out var s) ? s : null));
                    }
                }
            }

            // ExecuteMethodIfToggleOn(..., "key") call sites
            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                var methodName = invocation.Expression switch
                {
                    MemberAccessExpressionSyntax m => m.Name.Identifier.Text,
                    IdentifierNameSyntax i => i.Identifier.Text,
                    _ => null
                };

                if (methodName is null || !ManualCallNames.Contains(methodName)) continue;

                var args = invocation.ArgumentList.Arguments;
                // Signature: ExecuteMethodIfToggleOn(action, key) — key is the last string literal arg
                var keyArg = args
                    .Select(a => a.Expression)
                    .OfType<LiteralExpressionSyntax>()
                    .FirstOrDefault(l => l.IsKind(SyntaxKind.StringLiteralExpression));

                if (keyArg is null) continue;

                var key = keyArg.Token.ValueText;
                var line = tree.GetLineSpan(invocation.Span).StartLinePosition.Line + 1;
                entries.Add(new ToggleEntry(
                    key, methodName, relPath, line,
                    ToggleSource.ManualCall,
                    toggleStates.TryGetValue(key, out var s2) ? s2 : null));
            }
        }

        return entries
            .OrderBy(e => e.ToggleKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.File)
            .ToList();
    }
}
