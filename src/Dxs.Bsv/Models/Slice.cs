using System;

namespace Dxs.Bsv.Models;

public class Slice
{
    public int Start { get; init; }
    public int Length { get; init; }

    private byte[] _data;

    public byte[] Materialize(byte[] source)
    {
        if (_data != null) return _data;

        _data = new byte[Length];
        Array.Copy(source, Start, _data, 0, Length);

        return _data;
    }
}
