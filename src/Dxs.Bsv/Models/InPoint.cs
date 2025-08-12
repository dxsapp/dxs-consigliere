using System;

namespace Dxs.Bsv.Models;

public readonly struct InPoint
{
    private readonly byte[] _sigScript;

    public InPoint(Transaction transaction, int idx)
    {
        Transaction = transaction;
        Idx = idx;
        _sigScript = new byte[transaction.Inputs[Idx].ScriptSig.Length];

        Array.Copy(Transaction.Raw, Input.ScriptSig.Start, _sigScript, 0, Input.ScriptSig.Length);
    }

    public Transaction Transaction { get; }

    public int Idx { get; }

    public Input Input => Transaction.Inputs[Idx];

    public byte[] ScriptSig => _sigScript;
}