namespace Dxs.Tests.Shared;

public static class RepoPathResolver
{
    public static string ResolveFromRepoRoot(params string[] relativeSegments)
    {
        ArgumentNullException.ThrowIfNull(relativeSegments);

        var root = FindRepoRoot()
            ?? throw new InvalidOperationException("Unable to locate repository root from the current test runtime.");

        return Path.Combine([root, .. relativeSegments]);
    }

    private static string? FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Dxs.Consigliere.sln")))
                return current.FullName;

            current = current.Parent;
        }

        return null;
    }
}
