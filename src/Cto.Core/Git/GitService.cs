using Cto.Core.Common;

namespace Cto.Core.Git;

public sealed class GitService
{
    public async Task<bool> IsGitRepositoryAsync(string repoRoot, CancellationToken cancellationToken = default)
    {
        var result = await RunGitAsync(repoRoot, ["rev-parse", "--is-inside-work-tree"], cancellationToken);
        return result.Success && result.StdOut.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<(bool Success, IReadOnlyList<string> ChangedPaths, string Error)> GetChangedPathsAsync(string repoRoot, CancellationToken cancellationToken = default)
    {
        var result = await RunGitAsync(repoRoot, ["status", "--porcelain"], cancellationToken);
        if (!result.Success)
        {
            return (false, [], result.StdErr.Trim());
        }

        var paths = new List<string>();
        foreach (var rawLine in result.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (rawLine.Length < 4)
            {
                continue;
            }

            var pathSection = rawLine[3..].Trim();
            var path = pathSection.Contains(" -> ", StringComparison.Ordinal)
                ? pathSection.Split(" -> ", StringSplitOptions.TrimEntries).Last()
                : pathSection;

            path = path.Replace('\\', '/');
            paths.Add(path);
        }

        return (true, paths, string.Empty);
    }

    public async Task<(bool Success, string Sha, string Error)> CommitAsync(
        string repoRoot,
        IReadOnlyList<string> paths,
        string message,
        CancellationToken cancellationToken = default)
    {
        if (paths.Count == 0)
        {
            return (false, string.Empty, "No files provided to commit.");
        }

        var addArgs = new List<string> { "add" };
        addArgs.AddRange(paths);
        var addResult = await RunGitAsync(repoRoot, addArgs, cancellationToken);
        if (!addResult.Success)
        {
            return (false, string.Empty, addResult.StdErr.Trim());
        }

        var commitResult = await RunGitAsync(repoRoot, ["commit", "-m", message], cancellationToken);
        if (!commitResult.Success)
        {
            return (false, string.Empty, commitResult.StdErr.Trim());
        }

        var shaResult = await RunGitAsync(repoRoot, ["rev-parse", "HEAD"], cancellationToken);
        if (!shaResult.Success)
        {
            return (false, string.Empty, shaResult.StdErr.Trim());
        }

        return (true, shaResult.StdOut.Trim(), string.Empty);
    }

    public async Task<(bool Success, string Message, string Error)> GetHeadCommitMessageAsync(string repoRoot, CancellationToken cancellationToken = default)
    {
        var result = await RunGitAsync(repoRoot, ["log", "-1", "--pretty=%B"], cancellationToken);
        return result.Success
            ? (true, result.StdOut.Trim(), string.Empty)
            : (false, string.Empty, result.StdErr.Trim());
    }

    public async Task<(bool Success, string Sha, string Error)> GetHeadCommitShaAsync(string repoRoot, CancellationToken cancellationToken = default)
    {
        var result = await RunGitAsync(repoRoot, ["rev-parse", "HEAD"], cancellationToken);
        return result.Success
            ? (true, result.StdOut.Trim(), string.Empty)
            : (false, string.Empty, result.StdErr.Trim());
    }

    private static Task<ProcessResult> RunGitAsync(string repoRoot, IReadOnlyList<string> args, CancellationToken cancellationToken)
    {
        var allArgs = new List<string> { "-C", repoRoot };
        allArgs.AddRange(args);
        return ProcessRunner.RunAsync("git", allArgs, cancellationToken: cancellationToken);
    }
}
