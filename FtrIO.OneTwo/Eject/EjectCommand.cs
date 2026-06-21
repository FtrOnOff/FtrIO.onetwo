using System.Text;
using FtrIO.OneTwo;
using Spectre.Console;

namespace FtrIO.OneTwo.Eject;

internal static class EjectCommand
{
    internal static int Run(string[] args)
    {
        string? to = null;
        string? source = null;
        string? config = null;
        string? apiKey = null;
        string? project = null;
        string? env = null;
        string? markdownPath = null;
        string? exclude = null;
        bool dryRun = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--to"       when i + 1 < args.Length: to         = args[++i]; break;
                case "--source"   when i + 1 < args.Length: source     = args[++i]; break;
                case "--config"   when i + 1 < args.Length: config     = args[++i]; break;
                case "--api-key"  when i + 1 < args.Length: apiKey     = args[++i]; break;
                case "--project"  when i + 1 < args.Length: project    = args[++i]; break;
                case "--env"      when i + 1 < args.Length: env        = args[++i]; break;
                case "--markdown" when i + 1 < args.Length: markdownPath = args[++i]; break;
                case "--exclude"  when i + 1 < args.Length: exclude    = args[++i]; break;
                case "--dry-run":                            dryRun     = true; break;
            }
        }

        if (to is null)
        {
            AnsiConsole.MarkupLine("[red]Error:[/] --to is required.");
            AnsiConsole.MarkupLine("  Valid targets: launchdarkly, flagsmith, microsoft.featuremanagement, unleash");
            return 2;
        }

        EjectTarget target;
        try
        {
            target = EjectTargetHelper.Parse(to);
        }
        catch (ArgumentException ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {Markup.Escape(ex.Message)}");
            return 2;
        }

        source ??= Directory.GetCurrentDirectory();

        if (!Directory.Exists(source))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Source directory not found: {Markup.Escape(source)}");
            return 2;
        }

        // Resolve config path — default to appsettings.json in source dir
        if (config is null)
        {
            var candidate = Path.Combine(source, "appsettings.json");
            config = File.Exists(candidate) ? candidate : source;
        }

        var excludeKeys = exclude is not null
            ? new HashSet<string>(exclude.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var targetLabel = apiKey is not null
            ? $"{EjectTargetHelper.DisplayName(target)}{(project is not null ? $" / {project}" : string.Empty)}{(env is not null ? $" / {env}" : string.Empty)}"
            : EjectTargetHelper.DisplayName(target);

        AnsiConsole.MarkupLine($"[bold]FtrIO eject:[/] {Markup.Escape(EjectTargetHelper.DisplayName(target))}");
        AnsiConsole.MarkupLine($"[grey]Source:[/]  [yellow]{Markup.Escape(source)}[/]");
        AnsiConsole.MarkupLine($"[grey]Config:[/]  [yellow]{Markup.Escape(config)}[/]");
        AnsiConsole.MarkupLine($"[grey]Target:[/]  {Markup.Escape(targetLabel)}");
        if (dryRun) AnsiConsole.MarkupLine("[grey]Mode:    dry run — no API calls will be made.[/]");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[grey]Scanning source tree...[/]");
        var codeEntries = ToggleScanner.Scan(source);

        if (codeEntries.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No [[Toggle]]-decorated methods found.[/]");
            return 0;
        }

        AnsiConsole.MarkupLine($"[grey]Found {codeEntries.Count} [[Toggle]] reference(s).[/]\n");

        // Read config toggles
        Dictionary<string, string> configToggles;
        if (File.Exists(config))
        {
            configToggles = AppSettingsReader.ReadFile(config, out _);
        }
        else if (Directory.Exists(config))
        {
            var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var env2 in AppSettingsReader.ReadAll(config))
                foreach (var kv in env2.Toggles)
                    merged.TryAdd(kv.Key, kv.Value);
            configToggles = merged;
        }
        else
        {
            AnsiConsole.MarkupLine($"[yellow]Warning:[/] Config not found at {Markup.Escape(config)} — all toggles will show as MISSING.");
            configToggles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        // Validate API args when key is provided
        if (apiKey is not null && !dryRun)
        {
            if (target == EjectTarget.LaunchDarkly && (project is null || env is null))
            {
                AnsiConsole.MarkupLine("[red]Error:[/] --project and --env are required for LaunchDarkly when --api-key is provided.");
                return 3;
            }
            if (target == EjectTarget.Flagsmith && project is null)
            {
                AnsiConsole.MarkupLine("[red]Error:[/] --project is required for Flagsmith when --api-key is provided.");
                return 3;
            }
        }

        // Build eject entries
        var entries = new List<EjectEntry>();
        foreach (var code in codeEntries)
        {
            if (excludeKeys.Contains(code.ToggleKey)) continue;

            configToggles.TryGetValue(code.ToggleKey, out var ftrioValue);
            var targetKey = EjectTargetHelper.NormaliseKey(code.ToggleKey, target);
            var status    = EjectTargetHelper.DetermineStatus(ftrioValue, target);
            var warning   = EjectTargetHelper.DetermineWarning(ftrioValue, target);

            string? apiResult = null;
            if (apiKey is not null && !dryRun && status != EjectStatus.Missing)
            {
                try
                {
                    apiResult = EjectApiClient.CreateFlag(target, apiKey, project, env,
                        new EjectEntry(code.ToggleKey, targetKey, code.File, code.Line, ftrioValue, status, warning, null));
                }
                catch (Exception ex)
                {
                    apiResult = $"Failed: {ex.Message}";
                    status    = EjectStatus.ApiError;
                }
            }

            entries.Add(new EjectEntry(code.ToggleKey, targetKey, code.File, code.Line, ftrioValue, status, warning, apiResult));
        }

        // Print per-flag output
        foreach (var e in entries)
        {
            var statusIcon = e.Status switch
            {
                EjectStatus.Clean       => "[green]✅[/]",
                EjectStatus.Approximated => "[yellow]⚠️ [/]",
                EjectStatus.Missing     => "[red]❌[/]",
                EjectStatus.ApiError    => "[red]❌[/]",
                _                       => "   "
            };

            AnsiConsole.MarkupLine($"── [bold]{Markup.Escape(e.FtrioKey)}[/]    [grey]{Markup.Escape(e.File)}:{e.Line}[/]");
            AnsiConsole.MarkupLine($"   Current value:  {(e.FtrioValue is null ? "[yellow]MISSING[/]" : Markup.Escape(e.FtrioValue))}");

            if (e.FtrioKey != e.TargetKey)
                AnsiConsole.MarkupLine($"   Target key:     [cyan]{Markup.Escape(e.TargetKey)}[/]  [grey]({Markup.Escape(EjectTargetHelper.ConventionLabel(target))})[/]");
            else
                AnsiConsole.MarkupLine($"   Target key:     [cyan]{Markup.Escape(e.TargetKey)}[/]  [grey](unchanged)[/]");

            if (e.ApiResult is not null)
            {
                if (e.ApiResult == "Created")
                    AnsiConsole.MarkupLine($"   {statusIcon} Created in {Markup.Escape(EjectTargetHelper.DisplayName(target))}");
                else if (e.ApiResult == "AlreadyExists")
                    AnsiConsole.MarkupLine($"   [grey]Already exists in {Markup.Escape(EjectTargetHelper.DisplayName(target))}[/]");
                else if (e.ApiResult == "NoApiRequired")
                    AnsiConsole.MarkupLine($"   [grey]No API required for {Markup.Escape(EjectTargetHelper.DisplayName(target))}[/]");
                else
                    AnsiConsole.MarkupLine($"   [red]❌ {Markup.Escape(e.ApiResult)}[/]");
            }

            if (e.Warning is not null)
                AnsiConsole.MarkupLine($"   [yellow]⚠️  {Markup.Escape(e.Warning)}[/]");

            if (e.Status != EjectStatus.Missing && e.Status != EjectStatus.ApiError)
                Console.Write(CodeSuggestion.ForConsole(e, target));

            AnsiConsole.WriteLine();
        }

        // Cleanup steps
        PrintCleanupSteps(target);

        // Summary
        int clean        = entries.Count(x => x.Status == EjectStatus.Clean);
        int approximated = entries.Count(x => x.Status == EjectStatus.Approximated);
        int missing      = entries.Count(x => x.Status == EjectStatus.Missing);
        int apiErrors    = entries.Count(x => x.Status == EjectStatus.ApiError);

        AnsiConsole.MarkupLine("── [bold]Summary[/] " + new string('─', 60));
        AnsiConsole.MarkupLine($"{entries.Count} flag(s) found.");
        if (clean > 0)        AnsiConsole.MarkupLine($"[green]✅ {clean} created cleanly[/]");
        if (approximated > 0) AnsiConsole.MarkupLine($"[yellow]⚠️  {approximated} created with approximation[/]");
        if (missing > 0)      AnsiConsole.MarkupLine($"[red]❌ {missing} could not be created (MISSING value)[/]");
        if (apiErrors > 0)    AnsiConsole.MarkupLine($"[red]❌ {apiErrors} API error(s)[/]");
        AnsiConsole.MarkupLine($"{entries.Count} code change(s) required.");
        AnsiConsole.WriteLine();

        if (markdownPath is not null)
        {
            try
            {
                var md = BuildMarkdown(entries, target, source, config, env, project, dryRun);
                File.WriteAllText(markdownPath, md);
                AnsiConsole.MarkupLine($"[grey]Markdown report written to[/] [yellow]{Markup.Escape(markdownPath)}[/]");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[yellow]Markdown write failed:[/] {Markup.Escape(ex.Message)}");
            }
        }

        if (missing > 0 || apiErrors > 0) return 1;
        return 0;
    }

    private static void PrintCleanupSteps(EjectTarget target)
    {
        AnsiConsole.MarkupLine("── [bold]Cleanup steps[/] " + new string('─', 55));
        AnsiConsole.MarkupLine("Once all code changes are applied:");
        AnsiConsole.MarkupLine("  1. Remove AspectInjector from your .csproj:");
        AnsiConsole.MarkupLine("     [grey]<PackageReference Include=\"AspectInjector\" .../>[/]  ← remove");
        AnsiConsole.MarkupLine("  2. Remove FtrIO:");
        AnsiConsole.MarkupLine("     [grey]dotnet remove package FtrIO[/]");
        AnsiConsole.MarkupLine("  3. Remove provider packages if used (FtrIO.Providers.Http, etc.)");
        AnsiConsole.MarkupLine("  4. Remove the Toggles section from appsettings.json");
        AnsiConsole.MarkupLine("  5. Remove AdditionalFiles entry from .csproj if present");

        if (target == EjectTarget.MicrosoftFeatureManagement)
            AnsiConsole.MarkupLine("  6. Ensure Microsoft.FeatureManagement.AspNetCore is installed in each project");

        AnsiConsole.MarkupLine("  6. Verify your app builds and tests pass.");
        AnsiConsole.WriteLine();
    }

    private static string BuildMarkdown(
        IReadOnlyList<EjectEntry> entries,
        EjectTarget target,
        string source,
        string config,
        string? env,
        string? project,
        bool dryRun)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# FtrIO Eject Report: {EjectTargetHelper.DisplayName(target)}");
        sb.AppendLine();
        sb.AppendLine($"**Date:** {DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}  ");
        sb.AppendLine($"**Source:** `{source}`  ");
        sb.AppendLine($"**Config:** `{config}`  ");
        if (project is not null) sb.AppendLine($"**Project:** {project}  ");
        if (env is not null)     sb.AppendLine($"**Environment:** {env}  ");
        if (dryRun)              sb.AppendLine("**Mode:** dry run  ");
        sb.AppendLine();

        int clean        = entries.Count(x => x.Status == EjectStatus.Clean);
        int approximated = entries.Count(x => x.Status == EjectStatus.Approximated);
        int missing      = entries.Count(x => x.Status == EjectStatus.Missing);
        int apiErrors    = entries.Count(x => x.Status == EjectStatus.ApiError);

        sb.AppendLine("## Summary");
        sb.AppendLine();
        if (clean > 0)        sb.AppendLine($"- ✅ {clean} flag(s) created cleanly");
        if (approximated > 0) sb.AppendLine($"- ⚠️ {approximated} flag(s) created with approximation");
        if (missing > 0)      sb.AppendLine($"- ❌ {missing} flag(s) could not be created (MISSING value)");
        if (apiErrors > 0)    sb.AppendLine($"- ❌ {apiErrors} API error(s)");
        sb.AppendLine($"- {entries.Count} code change(s) required");
        sb.AppendLine();

        sb.AppendLine("## Flags");
        sb.AppendLine();

        foreach (var e in entries)
        {
            var statusIcon = e.Status switch
            {
                EjectStatus.Clean        => "✅",
                EjectStatus.Approximated => "⚠️",
                EjectStatus.Missing      => "❌",
                EjectStatus.ApiError     => "❌",
                _                        => "-"
            };

            sb.AppendLine($"### {statusIcon} {e.FtrioKey}");
            sb.AppendLine();
            sb.AppendLine("| | |");
            sb.AppendLine("|---|---|");
            sb.AppendLine($"| **File** | `{e.File}:{e.Line}` |");
            sb.AppendLine($"| **Current value** | `{e.FtrioValue ?? "MISSING"}` |");
            sb.AppendLine($"| **Target key** | `{e.TargetKey}` |");

            if (e.ApiResult is not null)
                sb.AppendLine($"| **{EjectTargetHelper.DisplayName(target)}** | {e.ApiResult} |");

            sb.AppendLine();

            if (e.Warning is not null)
            {
                sb.AppendLine($"> ⚠️ {e.Warning}");
                sb.AppendLine();
            }

            if (e.Status != EjectStatus.Missing && e.Status != EjectStatus.ApiError)
                sb.AppendLine(CodeSuggestion.ForMarkdown(e, target));
        }

        sb.AppendLine("## Cleanup steps");
        sb.AppendLine();
        sb.AppendLine("1. Remove `AspectInjector` from your `.csproj`");
        sb.AppendLine("2. `dotnet remove package FtrIO`");
        sb.AppendLine("3. Remove provider packages if used (`FtrIO.Providers.Http`, etc.)");
        sb.AppendLine("4. Remove the `Toggles` section from `appsettings.json`");
        sb.AppendLine("5. Remove `AdditionalFiles` entry from `.csproj` if present");
        if (target == EjectTarget.MicrosoftFeatureManagement)
            sb.AppendLine("6. Ensure `Microsoft.FeatureManagement.AspNetCore` is installed in each project");
        sb.AppendLine("6. Verify your app builds and tests pass.");

        return sb.ToString();
    }
}
