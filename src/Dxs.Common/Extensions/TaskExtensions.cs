namespace Dxs.Common.Extensions;

public static class TaskExtensions
{
    public static AggregateException CombineExceptions(this IEnumerable<Task> tasks) => new(tasks
        .Where(t => t.Exception != null)
        .SelectMany(t => t.Exception.InnerExceptions)
    );
}