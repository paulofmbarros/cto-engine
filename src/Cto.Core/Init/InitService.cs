using Cto.Core.Common;

namespace Cto.Core.Init;

public sealed class InitService
{
    private readonly string _engineRoot;

    public InitService(string engineRoot)
    {
        _engineRoot = engineRoot;
    }

    public OperationResult InitializeProjectPack(string targetPath, bool force)
    {
        var templateRoot = Path.Combine(_engineRoot, "templates", "project-pack");
        if (!Directory.Exists(templateRoot))
        {
            return OperationResult.Fail($"Template directory not found: {templateRoot}");
        }

        var target = Path.GetFullPath(targetPath);
        if (Directory.Exists(target))
        {
            var hasFiles = Directory.EnumerateFileSystemEntries(target).Any();
            if (hasFiles && !force)
            {
                return OperationResult.Fail($"Target directory '{target}' is not empty. Use --force to overwrite.");
            }
        }

        Directory.CreateDirectory(target);
        CopyDirectory(templateRoot, target, overwrite: force);

        return OperationResult.Ok($"Project pack initialized at {target}");
    }

    private static void CopyDirectory(string sourceDir, string destinationDir, bool overwrite)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDir, file);
            var destination = Path.Combine(destinationDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite);
        }
    }
}
