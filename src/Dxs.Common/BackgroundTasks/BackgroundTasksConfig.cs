namespace Dxs.Common.BackgroundTasks;

public class BackgroundTasksConfig
{
    public HashSet<string> DisabledTasks { get; set; } = new();

    public HashSet<string> EnabledTasks { get; set; }
}
