namespace Dxs.Common.Extensions;

/// <summary>
/// Provides access to <see cref="Task"/> with configurable and resettable timeout.
/// </summary>
public class TimedTaskSource
{
    private readonly CancellationToken _cancellationToken;

    private TaskCompletionSource _timeoutChangeEvent;
    private volatile Task _timeoutTask = Task.CompletedTask;
    private volatile Lazy<Task> _taskLazy;

    public TimedTaskSource(CancellationToken cancellationToken = default)
    {
        _taskLazy = new(Await);
        _cancellationToken = cancellationToken;
    }

    public void SetTaskTimeout(TimeSpan timeout)
    {
        var oldEvent = _timeoutChangeEvent;

        _timeoutTask = Task.Delay(timeout, _cancellationToken);
        _taskLazy = new(Await);
        _timeoutChangeEvent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        
        oldEvent?.SetResult();
    }

    public Task GetTask() => _taskLazy.Value;

    private async Task Await()
    {
        while (!_timeoutTask.IsCompleted && !_cancellationToken.IsCancellationRequested)
        {
            await Task.WhenAny(
                _timeoutTask,
                _timeoutChangeEvent?.Task ?? Task.CompletedTask
            );
        }
    }
}