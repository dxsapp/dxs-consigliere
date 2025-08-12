using System;
using System.Text;

namespace Dxs.Bsv.Script.Read;

public static class ScriptReaderExtensions
{
    public static string GetTokenId(this LockingScriptReader reader)
    {
        if (reader.ScriptType != ScriptType.P2STAS)
            return null;

        if (reader.Data.Count == 0)
            return null;

        return reader.Data[0].ToHexString();
    }

    public static bool IsSplittable(this LockingScriptReader reader)
    {
        if (reader.ScriptType != ScriptType.P2STAS)
            return true;

        if (reader.Data.Count < 2 && reader.Data[1].Length != 1)
            return true;

        return reader.Data[1][0] == 0x0;
    }

    public static string GetSymbol(this LockingScriptReader reader)
    {
        if (reader.ScriptType != ScriptType.P2STAS)
            return null;

        if (reader.Data.Count < 2)
            return null;

        return Encoding.UTF8.GetString(reader.Data[1]);
    }

    public static byte[] GetData(this LockingScriptReader reader)
    {
        if (reader.ScriptType != ScriptType.P2STAS)
            return Array.Empty<byte>();

        return reader.Data.Count > 2 ? reader.Data[2] : Array.Empty<byte>();
    }
}