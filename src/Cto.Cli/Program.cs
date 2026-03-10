using Cto.Core;
using Cto.Core.Common;
using Cto.Core.Planning;

return await ProgramEntry.RunAsync(args);

internal static class ProgramEntry
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || HasFlag(args, "--help") || HasFlag(args, "-h"))
        {
            PrintHelp();
            return 0;
        }

        var engineRoot = ResolveEngineRoot(Environment.CurrentDirectory);
        var app = new CtoEngineApp(engineRoot);

        var command = args[0].Trim().ToLowerInvariant();
        var result = command switch
        {
            "init" => await RunInitAsync(app, args),
            "snapshot" => await RunSnapshotAsync(app, args),
            "reality-check" => await RunRealityCheckAsync(app, args),
            "validate" => await RunValidateAsync(app, args),
            "plan" => await RunPlanAsync(app, args),
            "approve" => await RunApproveAsync(app, args),
            "execute" => await RunExecuteAsync(app, args),
            _ => OperationResult.Fail($"Unknown command: {command}", "Run 'cto-engine --help' for usage."),
        };

        foreach (var message in result.Messages)
        {
            Console.WriteLine(message);
        }

        return result.Success ? 0 : 1;
    }

    private static Task<OperationResult> RunInitAsync(CtoEngineApp app, string[] args)
    {
        var path = GetOptionValue(args, "--path");
        if (string.IsNullOrWhiteSpace(path))
        {
            return Task.FromResult(OperationResult.Fail("Missing required option --path for init."));
        }

        var force = HasFlag(args, "--force");
        return Task.FromResult(app.Init(path!, force));
    }

    private static Task<OperationResult> RunSnapshotAsync(CtoEngineApp app, string[] args)
    {
        var projectRoot = GetOptionValue(args, "--project") ?? Environment.CurrentDirectory;
        return app.SnapshotAsync(projectRoot);
    }

    private static Task<OperationResult> RunRealityCheckAsync(CtoEngineApp app, string[] args)
    {
        var projectRoot = GetOptionValue(args, "--project") ?? Environment.CurrentDirectory;
        return app.RealityCheckAsync(projectRoot);
    }

    private static Task<OperationResult> RunValidateAsync(CtoEngineApp app, string[] args)
    {
        var projectRoot = GetOptionValue(args, "--project") ?? Environment.CurrentDirectory;
        var target = GetOptionValue(args, "--target") ?? "all";
        return app.ValidateAsync(projectRoot, target);
    }

    private static Task<OperationResult> RunPlanAsync(CtoEngineApp app, string[] args)
    {
        var projectRoot = GetOptionValue(args, "--project") ?? Environment.CurrentDirectory;

        if (HasFlag(args, "--list-candidates"))
        {
            return app.ListPlanCandidatesAsync(projectRoot);
        }

        var selectCandidate = GetOptionValue(args, "--select");
        if (!string.IsNullOrWhiteSpace(selectCandidate))
        {
            if (!int.TryParse(selectCandidate, out var selected) || selected < 1)
            {
                return Task.FromResult(OperationResult.Fail("Invalid --select value. Expected a positive integer."));
            }

            return app.SelectPlanCandidateAsync(projectRoot, selected);
        }

        if (HasFlag(args, "--llm"))
        {
            if (!TryGetIntOption(args, "--candidates", out var candidates, out var candidatesError))
            {
                return Task.FromResult(OperationResult.Fail(candidatesError!));
            }

            if (!TryGetIntOption(args, "--max-input-tokens", out var maxInputTokens, out var inputError))
            {
                return Task.FromResult(OperationResult.Fail(inputError!));
            }

            if (!TryGetIntOption(args, "--max-output-tokens", out var maxOutputTokens, out var outputError))
            {
                return Task.FromResult(OperationResult.Fail(outputError!));
            }

            if (!TryGetDoubleOption(args, "--budget-usd", out var budgetUsd, out var budgetError))
            {
                return Task.FromResult(OperationResult.Fail(budgetError!));
            }

            var options = new PlanLlmOptions
            {
                Provider = GetOptionValue(args, "--provider"),
                Candidates = candidates,
                VisionFile = GetOptionValue(args, "--vision-file"),
                MaxInputTokens = maxInputTokens,
                MaxOutputTokensPerCandidate = maxOutputTokens,
                BudgetUsd = budgetUsd,
            };

            return app.PlanWithLlmAsync(projectRoot, options);
        }

        var interactive = HasFlag(args, "--interactive");
        return app.PlanInteractiveAsync(projectRoot, interactive);
    }

    private static Task<OperationResult> RunApproveAsync(CtoEngineApp app, string[] args)
    {
        var projectRoot = GetOptionValue(args, "--project") ?? Environment.CurrentDirectory;
        return app.ApproveAsync(projectRoot);
    }

    private static Task<OperationResult> RunExecuteAsync(CtoEngineApp app, string[] args)
    {
        var projectRoot = GetOptionValue(args, "--project") ?? Environment.CurrentDirectory;
        var dryRun = HasFlag(args, "--dry-run");
        return app.ExecuteAsync(projectRoot, dryRun);
    }

    private static void PrintHelp()
    {
        Console.WriteLine("CTO Engine CLI");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  cto-engine init --path <target> [--force]");
        Console.WriteLine("  cto-engine snapshot --project <project-root>");
        Console.WriteLine("  cto-engine reality-check --project <project-root>");
        Console.WriteLine("  cto-engine validate --project <project-root> --target <all|plan|context|weeklylog|jira-config|llm-config>");
        Console.WriteLine("  cto-engine plan --interactive --project <project-root>");
        Console.WriteLine("  cto-engine plan --llm [--provider gemini] [--candidates 3] [--vision-file <path>] [--max-input-tokens 12000] [--max-output-tokens 3500] [--budget-usd 3.0] --project <project-root>");
        Console.WriteLine("  cto-engine plan --list-candidates --project <project-root>");
        Console.WriteLine("  cto-engine plan --select <n> --project <project-root>");
        Console.WriteLine("  cto-engine approve --project <project-root>");
        Console.WriteLine("  cto-engine execute --project <project-root> [--dry-run]");
    }

    private static string? GetOptionValue(string[] args, string option)
    {
        for (var i = 1; i < args.Length; i++)
        {
            if (!string.Equals(args[i], option, StringComparison.Ordinal))
            {
                continue;
            }

            if (i + 1 < args.Length)
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static bool HasFlag(string[] args, string flag)
        => args.Any(arg => string.Equals(arg, flag, StringComparison.Ordinal));

    private static bool TryGetIntOption(string[] args, string option, out int? value, out string? error)
    {
        error = null;
        value = null;
        var raw = GetOptionValue(args, option);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        if (!int.TryParse(raw, out var parsed))
        {
            error = $"Invalid {option} value '{raw}'. Expected integer.";
            return false;
        }

        value = parsed;
        return true;
    }

    private static bool TryGetDoubleOption(string[] args, string option, out double? value, out string? error)
    {
        error = null;
        value = null;
        var raw = GetOptionValue(args, option);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        if (!double.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
        {
            error = $"Invalid {option} value '{raw}'. Expected decimal number (for example 2.5).";
            return false;
        }

        value = parsed;
        return true;
    }

    private static string ResolveEngineRoot(string startDirectory)
    {
        var current = new DirectoryInfo(startDirectory);
        while (current is not null)
        {
            var hasSchemas = Directory.Exists(Path.Combine(current.FullName, "schemas"));
            var hasTemplates = Directory.Exists(Path.Combine(current.FullName, "templates"));
            var hasPrompts = Directory.Exists(Path.Combine(current.FullName, "prompts"));

            if (hasSchemas && hasTemplates && hasPrompts)
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return startDirectory;
    }
}
