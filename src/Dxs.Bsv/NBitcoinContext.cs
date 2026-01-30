using System;

using NBitcoin;
using NBitcoin.Secp256k1;

namespace Dxs.Bsv;

public class NBitcoinContext
{
    private static readonly Lazy<Context> SingletonFactory = new(CreateInstance, true);

    public static Context Instance => SingletonFactory.Value;

    static Context CreateInstance()
    {
        var gen = new ECMultGenContext();
        gen.Blind(RandomUtils.GetBytes(32));

        return new Context(new ECMultContext(), gen);
    }
}
