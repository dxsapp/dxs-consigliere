using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Dxs.Common.Utils;
using Microsoft.Extensions.Logging;

namespace Dxs.Common.Extensions;

public static class LoggerExtensions
{
    public static IDisposable BeginScopeWith<T>(this ILogger logger, string name, T value) =>
        logger.BeginScope(new Dictionary<string, object> { { name, value } });

    public static IDisposable BeginScopeWith(this ILogger logger, object values) =>
        logger.BeginScope(PropertyHelper.ObjectToDictionary(values));

    [SuppressMessage("ReSharper", "TemplateIsNotCompileTimeConstantProblem", Justification = "Serilog not enumerating arguments in scope string")]
    public static IDisposable BeginMethodScope(this ILogger logger, string methodName, IEnumerable<object> args) =>
        logger.BeginScope($"{methodName}({ArgsToString(args)})", methodName, args);

    public static IDisposable BeginMethodScope(this ILogger logger, [CallerMemberName] string methodName = null, params object[] args) =>
        logger.BeginMethodScope(methodName, args.AsEnumerable());

    [SuppressMessage("ReSharper", "TemplateIsNotCompileTimeConstantProblem", Justification = "Serilog not enumerating arguments in scope string")]
    public static IDisposable BeginMethodScope<T>(this ILogger<T> logger, string methodName, IEnumerable<object> args) =>
        logger.BeginScope($"{typeof(T).Name}.{methodName}({ArgsToString(args)})", methodName, args);

    public static IDisposable BeginMethodScope<T>(this ILogger<T> logger, [CallerMemberName] string methodName = null, params object[] args) =>
        logger.BeginMethodScope(methodName, args.AsEnumerable());

    private static string ArgsToString(IEnumerable<object> args) => string.Join(", ", args.Select(arg => $"{arg}"));
}