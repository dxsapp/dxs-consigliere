using TrustMargin.Common.Extensions;

namespace Dxs.Common.Extensions
{
    public static class TaskEx
    {
        private static readonly Action EmptyAction = () => { };

        public static Task Then(this Task task, Action<Task> onSuccess = null, Action<Task> onFailure = null) =>
            task.ContinueWith(t =>
            {
                if (t.IsCompletedSuccessfully)
                    onSuccess?.Invoke(t);
                else
                    onFailure?.Invoke(t);
            });

        public static Task Then<T>(this Task<T> task, Action<Task<T>> onSuccess = null, Action<Task<T>> onFailure = null) =>
            task.ContinueWith(t =>
            {
                if (t.IsCompletedSuccessfully)
                    onSuccess?.Invoke(t);
                else
                    onFailure?.Invoke(t);
            });

        public static Task HandleCancellation(this Task task, Action onCancel = null, CancellationToken? cancellationToken = null) =>
            task.ContinueWith(t =>
            {
                try
                {
                    t.GetAwaiter().GetResult();
                }
                catch (OperationCanceledException exception) when (
                    t.IsCanceled
                    && exception.CancellationToken.IsCancellationRequested // Additional check as any OperationCanceledException can cancel task
                    && (cancellationToken == null || cancellationToken == exception.CancellationToken)
                )
                {
                    (onCancel ?? EmptyAction)?.Invoke();
                }
            });

        public static Task IgnoreAsync(this Task task) => Task.CompletedTask;

        public static async Task<(T1, T2)> WhenAll<T1, T2>(Task<T1> task1, Task<T2> task2)
        {
            await Task.WhenAll(task1, task2);
            return (task1.Result, task2.Result);
        }

        public static async Task<(T1, T2, T3)> WhenAll<T1, T2, T3>(Task<T1> task1, Task<T2> task2, Task<T3> task3)
        {
            await Task.WhenAll(task1, task2, task3);
            return (task1.Result, task2.Result, task3.Result);
        }

        public static async Task<(T1, T2, T3, T4, T5, T6, T7)> WhenAll<T1, T2, T3, T4, T5, T6, T7>(
            Task<T1> task1,
            Task<T2> task2,
            Task<T3> task3,
            Task<T4> task4,
            Task<T5> task5,
            Task<T6> task6,
            Task<T7> task7
        )
        {
            await Task.WhenAll(task1, task2, task3, task4, task5, task6, task7);
            return (task1.Result, task2.Result, task3.Result, task4.Result, task5.Result, task6.Result, task7.Result);
        }

        public static async Task<(T1, T2, T3, T4, T5, T6, T7, T8, T9)> WhenAll<T1, T2, T3, T4, T5, T6, T7, T8, T9>(
            Task<T1> task1,
            Task<T2> task2,
            Task<T3> task3,
            Task<T4> task4,
            Task<T5> task5,
            Task<T6> task6,
            Task<T7> task7,
            Task<T8> task8,
            Task<T9> task9
        )
        {
            await Task.WhenAll(task1, task2, task3, task4, task5, task6, task7, task8, task9);
            return (task1.Result, task2.Result, task3.Result, task4.Result, task5.Result, task6.Result, task7.Result, task8.Result, task9.Result);
        }

        public static async Task<(T1, T2, T3, T4, T5, T6, T7, T8, T9, T10)> WhenAll<T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(
            Task<T1> task1,
            Task<T2> task2,
            Task<T3> task3,
            Task<T4> task4,
            Task<T5> task5,
            Task<T6> task6,
            Task<T7> task7,
            Task<T8> task8,
            Task<T9> task9,
            Task<T10> task10
        )
        {
            await Task.WhenAll(task1, task2, task3, task4, task5, task6, task7, task8, task9, task10);
            return (task1.Result, task2.Result, task3.Result, task4.Result, task5.Result, task6.Result, task7.Result, task8.Result, task9.Result, task10.Result);
        }

        public static Task DeferAsync(Func<Task> action, TimeSpan delay, CancellationToken token)
            => Task.Run(async () =>
            {
                await Task.Delay(delay, token);

                await action();
            }, token);

        /// <summary>
        /// Returns first completed task matching the <paramref name="filter"/> condition
        /// or <c>null</c> if all tasks completed but none matched.
        /// </summary>
        public static async Task<Task<T>> WhenAnyOrNull<T>(IEnumerable<Task<T>> tasks, Func<Task<T>, bool> filter)
        {
            var taskList = tasks.AsIList();
            var tcs = new TaskCompletionSource<int>();
            var remainingTasks = taskList.Count;

            foreach (var (task, index) in taskList.Enumerate())
            {
                _ = task.ContinueWith(t =>
                {
                    if (filter(t))
                        tcs.TrySetResult(index);
                    else if (Interlocked.Decrement(ref remainingTasks) == 0)
                        tcs.TrySetResult(-1);
                });
            }

            var succeedIndex = await tcs.Task;
            return succeedIndex < 0 ? null : taskList[succeedIndex];
        }

        /// <summary>
        /// <para>Waits for the first successfully completed task.</para>
        /// <para>If any task succeeds, all other task results or exceptions are ignored.</para>
        /// <para>If all tasks failed, <see cref="AggregateException"/> is thrown with all combined exceptions.</para>
        /// </summary>
        /// <returns>First successfully completed task.</returns>
        public static async Task<Task<T>> WhenAnySucceedOrThrow<T>(IEnumerable<Task<T>> tasks)
        {
            tasks = tasks.AsIList();
            var task = await WhenAnyOrNull(tasks, t => t.Status == TaskStatus.RanToCompletion);
            return task ?? throw tasks.CombineExceptions();
        }
    }
}
