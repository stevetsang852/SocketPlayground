using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace CommonClassLibrary
{
    public static class BytesHelper
    {
        public static bool ByteArrayToFile(string fileName, byte[]? byteArray)
        {
            if (byteArray is null)
            {
                return false;
            }

            try
            {
                using (var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write))
                {
                    fs.Write(byteArray, 0, byteArray.Length);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception caught in process: {0}", ex);
                try
                {
                    if (File.Exists(fileName))
                    {
                        File.Delete(fileName);
                    }
                }
                catch
                {
                    // Best-effort cleanup of a partial write.
                }

                return false;
            }
        }

        /// <summary>Lowercase hex SHA-256 of <paramref name="data"/>.</summary>
        public static string ComputeSha256Hex(byte[] data)
        {
            ArgumentNullException.ThrowIfNull(data);
            var hash = SHA256.HashData(data);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        /// <summary>
        /// Returns true when <paramref name="expectedHex"/> is null/whitespace (caller treats as unverified)
        /// or when it matches the SHA-256 of <paramref name="data"/> (case-insensitive, optional 0x prefix).
        /// </summary>
        public static bool TryValidateSha256(byte[] data, string? expectedHex, out string actualHex)
        {
            actualHex = ComputeSha256Hex(data);
            if (string.IsNullOrWhiteSpace(expectedHex))
            {
                return true;
            }

            var expected = expectedHex.Trim();
            if (expected.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                expected = expected[2..];
            }

            return string.Equals(actualHex, expected, StringComparison.OrdinalIgnoreCase);
        }
    }
}
