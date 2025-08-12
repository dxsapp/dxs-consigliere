using System;

namespace Dxs.Bsv.Transactions.Read;

public readonly ref struct TransactionReadToken
{
    public enum Type
    {
        Version,
        InputCount,
        OutputCount,
        Input,
        Output,
        LockTime
    }

    private TransactionReadToken(
        Type tokenType,
        ReadOnlySpan<byte> txId,
        uint version,
        uint lockTime,
        ulong inputCount,
        ulong outputCount,
        uint vout,
        ulong value,
        uint sequence,
        ulong chunkSize,
        int txStartPosition
    )
    {
        TokenType = tokenType;
        TxId = txId;
        Version = version;
        LockTime = lockTime;
        InputCount = inputCount;
        OutputCount = outputCount;
        Vout = vout;
        Value = value;
        Sequence = sequence;
        ChunkSize = chunkSize;
        TxStartPosition = txStartPosition;
    }

    public Type TokenType { get; }
    public uint Version { get; }
    public uint LockTime { get; }
    public ulong InputCount { get; }
    public ulong OutputCount { get; }
    public ReadOnlySpan<byte> TxId { get; }
    public ulong ChunkSize { get; }
    public int TxStartPosition { get; }

    public uint Vout { get; }
    public ulong Value { get; }
    public uint Sequence { get; }

    public static TransactionReadToken BuildVersion(uint version)
        => new(Type.Version, default, version, default, default, default, default, default, default, default, default);

    public static TransactionReadToken BuildLockTime(uint lockTime) =>
        new(Type.LockTime, default, default, lockTime, default, default, default, default, default, default, default);

    public static TransactionReadToken BuildInputCount(ulong inputCount)
        => new(Type.InputCount, default, default, default, inputCount, default, default, default, default, default, default);

    public static TransactionReadToken BuildOutputCount(ulong outputCount)
        => new(Type.OutputCount, default, default, default, default, outputCount, default, default, default, default, default);

    public static TransactionReadToken BuildInput(int txStartPosition, ulong size, ReadOnlySpan<byte> txId, uint vout) =>
        new(Type.Input, txId, default, default, default, default, vout, default, default, size, txStartPosition);

    public static TransactionReadToken BuildOutput(int txStartPosition, ulong size, ulong value, uint idx)
        => new(Type.Output, default, default, default, default, default, idx, value, default, size, txStartPosition);
}