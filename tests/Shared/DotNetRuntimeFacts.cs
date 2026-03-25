using System.Diagnostics;

namespace Dxs.Tests.Shared;

public static class DotNetRuntimeFacts
{
    public static bool HasRuntimeMajor(int major)
    {
        var dotnetPath = GetDotNetHostPath();

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = dotnetPath,
                Arguments = "--list-runtimes",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        return output.Contains($"Microsoft.NETCore.App {major}.", StringComparison.Ordinal);
    }

    public static string GetDotNetHostPath()
    {
        var dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");

        if (!string.IsNullOrWhiteSpace(dotnetRoot))
        {
            var rootedPath = Path.Combine(dotnetRoot, "dotnet");
            if (File.Exists(rootedPath))
                return rootedPath;
        }

        var hostPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (!string.IsNullOrWhiteSpace(hostPath))
            return hostPath;

        return "dotnet";
    }
}
