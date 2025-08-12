using System;
using Dxs.Bsv.Protocol;

namespace Dxs.Bsv.Transactions.Read;

public class BaseTransactionReader
{
    protected readonly BitcoinStreamReader BitcoinStreamReader;
    protected readonly Network Network;

    protected BaseTransactionReader(BitcoinStreamReader streamReader, Network network)
    {
        BitcoinStreamReader = streamReader;
        Network = network;
    }

    protected void ReadInternal()
    {
        var startPosition = BitcoinStreamReader.Position;

        var version = BitcoinStreamReader.ReadUInt32Le();

        HandleToken(TransactionReadToken.BuildVersion(version));

        var inputCount = BitcoinStreamReader.ReadVarInt();
        HandleToken(TransactionReadToken.BuildInputCount(inputCount));

        for (var i = 0UL; i < inputCount; i++)
            ReadInput(BitcoinStreamReader, startPosition);

        var outputCount = BitcoinStreamReader.ReadVarInt();
        HandleToken(TransactionReadToken.BuildOutputCount(outputCount));

        for (var i = 0UL; i < outputCount; i++)
        {
            var value = BitcoinStreamReader.ReadUInt64Le();
            var scriptPubKeySize = BitcoinStreamReader.ReadVarInt();

            var scriptPubKeyStart = startPosition;
            HandleToken(TransactionReadToken.BuildOutput(scriptPubKeyStart, scriptPubKeySize, value, (uint)i));
        }

        var lockTime = BitcoinStreamReader.ReadUInt32Le();
        HandleToken(TransactionReadToken.BuildLockTime(lockTime));
    }

    protected virtual void HandleToken(TransactionReadToken token) { }

    private void ReadInput(BitcoinStreamReader bitcoinStreamReader, int startPosition)
    {
        Span<byte> txId = stackalloc byte[32];
        for (var i = 0; i < 32; i++)
            txId[31 - i] = bitcoinStreamReader.ReadByte(); // Little endian

        var vout = bitcoinStreamReader.ReadUInt32Le();
        var scriptSigSize = bitcoinStreamReader.ReadVarInt();

        HandleToken(TransactionReadToken.BuildInput(startPosition, scriptSigSize, txId, vout));
    }
}