using System;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace NzbDrone.Common.Crypto
{
    public static class HashConverter
    {
        public static int GetHashInt31(string target)
        {
            if (string.IsNullOrEmpty(target))
            {
                return 0;
            }

            Span<byte> hash = stackalloc byte[20];
            var maxByteCount = Encoding.Default.GetMaxByteCount(target.Length);
            if (maxByteCount <= 512)
            {
                Span<byte> bytes = stackalloc byte[maxByteCount];
                var written = Encoding.Default.GetBytes(target, bytes);
                SHA1.HashData(bytes.Slice(0, written), hash);
            }
            else
            {
                var bytes = Encoding.Default.GetBytes(target);
                SHA1.HashData(bytes, hash);
            }

            return BinaryPrimitives.ReadInt32LittleEndian(hash) & 0x7fffffff;
        }

        public static byte[] GetHash(string target)
        {
            if (string.IsNullOrEmpty(target))
            {
                return Array.Empty<byte>();
            }

            var maxByteCount = Encoding.Default.GetMaxByteCount(target.Length);
            if (maxByteCount <= 512)
            {
                Span<byte> bytes = stackalloc byte[maxByteCount];
                var written = Encoding.Default.GetBytes(target, bytes);
                return SHA1.HashData(bytes.Slice(0, written));
            }

            return SHA1.HashData(Encoding.Default.GetBytes(target));
        }
    }
}
