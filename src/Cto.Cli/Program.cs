using Cto.Core;
using Cto.Core.Common;

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
        Console.WriteLine("  cto-engine validate --project <project-root> --target <all|plan|context|weeklylog|jira-config>");
        Console.WriteLine("  cto-engine plan --interactive --project <project-root>");
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
