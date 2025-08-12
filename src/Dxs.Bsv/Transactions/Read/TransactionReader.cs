using System.Collections.Generic;
using System.IO;
using Dxs.Bsv.Models;
using Dxs.Bsv.Protocol;

namespace Dxs.Bsv.Transactions.Read;

public class TransactionReader : BaseTransactionReader
{
    private readonly Transaction _transaction;

    public TransactionReader(BitcoinStreamReader streamReader, Network network) : base(streamReader, network)
    {
        _transaction = new Transaction(network);
    }

    public TransactionReader(Stream stream, Network network) : this(new BitcoinStreamReader(stream), network) { }

    public Transaction Read()
    {
        var txBytes = new List<byte>(256);

        BitcoinStreamReader.AddBytesReceiver(txBytes);
        ReadInternal();
        BitcoinStreamReader.RemoveBytesReceiver();

        _transaction.Raw = txBytes.ToArray();

        return _transaction;


        //  BitcoinStreamReader.StartHashStream();
        //
        //  await Task.WhenAll(
        //      Task.Run(ReadInternal),
        //      Task.Run(() =>
        //      {
        //          var hash = Hash.Sha256(BitcoinStreamReader.HashStream);
        //          hash = Hash.Sha256(hash);
        //
        //          var (id, idRaw) = BitcoinHelpers.GetTxId2(hash);
        //
        //          _transaction.Id = id;
        //          _transaction.IdRaw = idRaw;
        //      })
        //  );
        //
        // BitcoinStreamReader.StopHashStream();
        // BitcoinStreamReader.ResetHashStream();

        // return _transaction;
    }

    protected override void HandleToken(TransactionReadToken token)
    {
        switch (token.TokenType)
        {
            case TransactionReadToken.Type.Output:
                _transaction.Outputs.Add(Output.Parse(BitcoinStreamReader, token.TxStartPosition, (int)token.ChunkSize, token.Value, token.Vout, Network));
                break;

            case TransactionReadToken.Type.Input:
                _transaction.Inputs.Add(Input.Parse(BitcoinStreamReader, token.TxStartPosition, (int)token.ChunkSize, token.TxId.ToHexString(), token.Vout, Network));
                break;
        }
    }
}