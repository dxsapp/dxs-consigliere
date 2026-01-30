using System.Collections.Generic;
using System.IO;
using System.Linq;

using Dxs.Bsv.Protocol;
using Dxs.Bsv.Script.Build;

namespace Dxs.Bsv.Script.Read
{
    public class SimpleScriptReader : BaseScriptReader
    {
        private SimpleScriptReader(BitcoinStreamReader bitcoinStreamReader, int length, Network network) : base(bitcoinStreamReader, length, network) { }

        private readonly List<ScriptBuildToken> _tokens = new();

        private IReadOnlyList<ScriptBuildToken> Read()
        {
            ReadInternal();

            return _tokens;
        }

        protected override bool HandleToken(ScriptReadToken token, int tokenIdx, bool isLastToken)
        {
            _tokens.Add(new ScriptBuildToken(token));

            return true;
        }

        public static IReadOnlyList<ScriptBuildToken> Read(string hex, Network network)
        {
            var bytes = hex.FromHexString();

            return Read(bytes, network);
        }

        public static IReadOnlyList<ScriptBuildToken> Read(IList<byte> bytes, Network network)
        {
            using var stream = new MemoryStream(bytes.ToArray());

            return Read(stream, network);
        }

        public static IReadOnlyList<ScriptBuildToken> Read(Stream stream, Network network)
        {
            using var bitcoinStreamReader = new BitcoinStreamReader(stream);

            return Read(bitcoinStreamReader, (int)stream.Length, network);
        }

        public static IReadOnlyList<ScriptBuildToken> Read(BitcoinStreamReader bitcoinStreamReader, int expectedLength, Network network)
        {
            var reader = new SimpleScriptReader(bitcoinStreamReader, expectedLength, network);
            return reader.Read();
        }
    }
}
