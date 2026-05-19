namespace Dxs.Common.BackgroundTasks;

public class BackgroundTasksConfig
{
    public HashSet<string> DisabledTasks { get; set; } = new();

    public HashSet<string> EnabledTasks { get; set; }

    // wave-A2 S2-audit L1: .NET configuration array binding merges
    // by index, so `EnabledTasks: []` in a layered Test json does
    // NOT clear an inherited base array. This boolean is the
    // unambiguous kill switch contract/integration suites use.
    public bool DisableAll { get; set; }
}
