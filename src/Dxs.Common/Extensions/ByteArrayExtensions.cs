using System.IO.Compression;
using TrustMargin.Common.Extensions;

namespace Dxs.Common.Extensions
{
    public static class ByteArrayExtensions
    {
        public static byte[] GZipDecompress(this byte[] bytes)
        {
            if (bytes == null || !EnumerableExtensions.Any(bytes))
                return [];

            using var decompressedStream = new MemoryStream();
            using var originalStream = new MemoryStream(bytes);
            using var decompressionStream = new GZipStream(originalStream, CompressionMode.Decompress);

            decompressionStream.CopyTo(decompressedStream);

            return decompressedStream.ToArray();
        }

        public static bool StartsWith(this byte[] source, byte[] pattern)
        {
            if (pattern.Length > source.Length)
                return false;

            return !EnumerableExtensions.Any(pattern.Where((t, i) => source[i] != t));
        }

        public static string ToUtf8String(this byte[] source)
            => System.Text.Encoding.UTF8.GetString(source);
    }
}