using System;

using Dxs.Bsv.Protocol;

namespace Dxs.Bsv.Script.Read;

public abstract class BaseScriptReader(BitcoinStreamReader bitcoinStreamReader, int length, Network network)
{
    protected readonly BitcoinStreamReader BitcoinStreamReader = bitcoinStreamReader;
    protected readonly Network Network = network;
    protected readonly int ExpectedLength = length;

    private int _readBytes;

    protected int ReadBytes
    {
        get => _readBytes;
        set
        {
            if (value > ExpectedLength)
                throw new Exception("Read more bytes than expected");

            _readBytes = value;
        }
    }

    /// <summary>
    /// returns token count
    /// </summary>
    protected int ReadInternal()
    {
        var tokenIdx = 0;

        while (ReadBytes < ExpectedLength)
        {
            var opCodeNum = BitcoinStreamReader.ReadByte();

            ReadBytes++;

            switch (opCodeNum)
            {
                case > 0 and < (byte)OpCode.OP_PUSHDATA1:
                    {
                        var count = opCodeNum;

                        if (!HandleBytes(opCodeNum, count, tokenIdx, count))
                            return -1;

                        break;
                    }
                case (byte)OpCode.OP_PUSHDATA1:
                    {
                        if (ReadBytes == ExpectedLength)
                        {
                            if (!HandleRest(opCodeNum, tokenIdx))
                                return -1;

                            break;
                        }

                        var count = BitcoinStreamReader.ReadByte();
                        ReadBytes++;

                        if (!HandleBytes(opCodeNum, count, tokenIdx, count))
                            return -1;

                        break;
                    }
                case (byte)OpCode.OP_PUSHDATA2:
                    {
                        if (ReadBytes + sizeof(ushort) >= ExpectedLength)
                        {
                            if (!HandleRest(opCodeNum, tokenIdx))
                                return -1;

                            break;
                        }

                        var count = BitcoinStreamReader.ReadUInt16Le();
                        ReadBytes += 2;

                        if (!HandleBytes(opCodeNum, count, tokenIdx, count))
                            return -1;

                        break;
                    }
                case (byte)OpCode.OP_PUSHDATA4:
                    {
                        if (ReadBytes + sizeof(uint) >= ExpectedLength)
                        {
                            if (!HandleRest(opCodeNum, tokenIdx))
                                return -1;

                            break;
                        }

                        var count = BitcoinStreamReader.ReadUInt32Le();
                        ReadBytes += 4;

                        if (!HandleBytes(opCodeNum, count, tokenIdx, count))
                            return -1;

                        break;
                    }
                default:
                    {
                        if (!HandleTokenInternal(new ScriptReadToken(opCodeNum, default), tokenIdx, ReadBytes == ExpectedLength))
                            return -1;

                        break;
                    }
            }

            tokenIdx++;
        }

        return tokenIdx;
    }

    protected abstract bool HandleToken(ScriptReadToken token, int tokenIdx, bool isLastToken);

    private bool HandleTokenInternal(ScriptReadToken token, int tokenIdx, bool isLastToken)
    {
        return HandleToken(token, tokenIdx, isLastToken);
    }

    private bool HandleBytes(byte opCodeNum, uint count, int tokenIdx, ulong varInt)
    {
        // sometimes data after op_return written using in incorrect format 
        if (count + ReadBytes > ExpectedLength)
        {
            var rest = ExpectedLength - ReadBytes;

            var bufferWriter = new BufferWriter(BufferWriter.GetVarIntLength(varInt) + 1 /*OpCodeNum*/ + rest);
            bufferWriter.WriteByte(opCodeNum);
            bufferWriter.WriteVarInt(varInt);
            bufferWriter.Write(BitcoinStreamReader.ReadNBytes((ulong)rest));

            ReadBytes += rest;

            return HandleTokenInternal(new ScriptReadToken(opCodeNum, bufferWriter.Bytes), tokenIdx, ReadBytes == ExpectedLength);
        }

        var bytes = BitcoinStreamReader.ReadNBytes(count);

        ReadBytes += (int)count;

        return HandleTokenInternal(new ScriptReadToken(opCodeNum, bytes), tokenIdx, ReadBytes == ExpectedLength);
    }

    private bool HandleRest(byte opCodeNum, int tokenIdx)
    {
        var count = ExpectedLength - ReadBytes;
        var bytes = count > 0
            ? BitcoinStreamReader.ReadNBytes((ulong)(count))
            : null;

        return HandleTokenInternal(new ScriptReadToken(opCodeNum, bytes), tokenIdx, true);
    }
}
