namespace Dxs.Consigliere.Services;

public enum TransactionQueryErrorKind
{
    BadRequest,
    NotFound,
    InternalError,
    NotStas,
}

public sealed class TransactionQueryException(
    TransactionQueryErrorKind kind,
    string message
) : Exception(message)
{
    public TransactionQueryErrorKind Kind { get; } = kind;
}
