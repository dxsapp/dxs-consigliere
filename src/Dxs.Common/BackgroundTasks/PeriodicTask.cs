using System.Collections.Concurrent;
using Dxs.Common.Extensions;
using Dxs.Common.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Dxs.Common.BackgroundTasks;

public abstract class PeriodicTask: IHostedService, IBackgroundTask
{
    protected virtual TimeSpan StartDelay => TimeSpan.Zero;
    protected abstract TimeSpan Period { get; }
    protected abstract TimeSpan WaitTimeOnError { get; }

    private readonly ILogger _logger;
    private readonly BackgroundTasksConfig _config;

    private readonly CancellationTokenSource _serviceCancellationSource = new();
    private readonly object _taskLock = new();

    private readonly TimedTaskSource _pauseSource;
    protected Task PauseTask => _pauseSource.GetTask();

    protected CancellationToken ServiceCancellationToken => _serviceCancellationSource.Token;

    private Task _task;

    private static readonly ConcurrentDictionary<string, int> InitializedTasks = new();

    protected PeriodicTask(BackgroundTasksConfig config, ILogger logger)
    {
        _logger = logger;
        _config = config;
        _pauseSource = new TimedTaskSource(ServiceCancellationToken);

        // ReSharper disable once VirtualMemberCallInConstructor
        if (!InitializedTasks.TryAdd(Name, 1))
        {
            throw new Exception($"Attempt to create more than one task instance: {Name}");
        }
    }

    public abstract string Name { get; }

    Task IHostedService.StartAsync(CancellationToken cancellationToken)
    {
        if (_config.EnabledTasks == null)
        {
            if (_config.DisabledTasks.Contains(Name))
            {
                return Task.CompletedTask;
            }
        }
        else
        {
            if (!_config.EnabledTasks.Contains(Name))
            {
                return Task.CompletedTask;
            }
        }

        lock (_taskLock)
        {
            if (_task != null)
                throw new Exception($"Task {Name} has already started.");

            _task = Task.Run(async () =>
            {
                await Task.Delay(StartDelay, ServiceCancellationToken).HandleCancellation(OnCanceled, cancellationToken);

                while (!ServiceCancellationToken.IsCancellationRequested)
                {
                    await PauseTask;
                    await SafeRunAsync(ServiceCancellationToken);
                }

                _logger.LogDebug("Task {TaskName} stopped", Name);
            }, ServiceCancellationToken);

            _logger.LogDebug("Task {TaskName} started", Name);
        }

        return Task.CompletedTask;
    }

    Task IHostedService.StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Task {TaskName} is stopping", Name);
        _serviceCancellationSource.Cancel();
        return _task;
    }

    protected abstract Task RunAsync(CancellationToken cancellationToken);

    public void Pause(TimeSpan? timeout = null)
    {
        if (timeout is { Ticks: <= 0 })
            return;

        _pauseSource.SetTaskTimeout(timeout ?? TimeSpan.MaxValue);
        _logger.LogDebug("{TaskName} paused for {WaitTime}", Name, timeout);
    }

    public void Continue()
    {
        if (PauseTask.IsCompleted)
            return;

        _pauseSource.SetTaskTimeout(TimeSpan.Zero);
        _logger.LogDebug("{TaskName} continued", Name);
    }

    private async Task SafeRunAsync(CancellationToken cancellationToken)
    {
        var taskName = GetType().Name;
        using var scope = _logger.BeginMethodScope($"{taskName}.{nameof(RunAsync)}", DateTime.UtcNow);

        try
        {
            await RunAsync(cancellationToken).HandleCancellation(OnCanceled, cancellationToken);

            await Task.Delay(Period, cancellationToken).HandleCancellation(OnCanceled, cancellationToken);
        }
        catch (Exception exception)
        {
            var waitTime = WaitTimeOnError;
            _logger.LogError(exception, "{TaskName} failed, retrying in {WaitTime}", Name, waitTime);
            await Task.Delay(WaitTimeOnError, cancellationToken).HandleCancellation(OnCanceled, cancellationToken);
        }
    }

    private void OnCanceled()
    {
        _logger.LogDebug("Cancelled {TaskName}", Name);
    }
}