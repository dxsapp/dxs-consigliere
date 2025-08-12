using System.Threading.Tasks.Dataflow;

namespace Dxs.Common.Extensions;

public static class DataflowExtensions
{
    private static readonly DataflowLinkOptions PropagateCompletion = new() { PropagateCompletion = true };

    public static IDisposable LinkToWithCompletion<T>(this ISourceBlock<T> source, ITargetBlock<T> target) =>
        source.LinkTo(target, PropagateCompletion);
}