using System.Runtime.ExceptionServices;

namespace Dxs.Common.Extensions;

public static class ExceptionExtensions
{
    /// <summary>
    /// Rethrows <paramref name="exception"/> maintaining original stack trace.
    /// </summary>
    public static Exception Dispatch(this Exception exception)
    {
        ExceptionDispatchInfo.Throw(exception);
        return exception;
    }

    public static Exception Unwrap(this AggregateException aggregateException)
    {
        Exception exception = aggregateException;
        while (exception is AggregateException && exception.InnerException != null)
            exception = exception.InnerException;

        return exception;
    }

    public static Exception EnsureUnwrapped(this Exception exception) =>
        exception is AggregateException aggregateException ? aggregateException.Unwrap() : exception;
}
