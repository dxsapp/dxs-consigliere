using System.Reflection;

namespace Dxs.Common.Utils;

public static class PathHelper
{
    public static readonly string RootPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

    public static string GetPath(params string[] segments)
    {
        var items = new List<string>(segments ?? Array.Empty<string>());
        items.Insert(0, RootPath);

        return Path.Combine(items.ToArray());
    }
}
