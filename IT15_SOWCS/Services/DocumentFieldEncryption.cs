using System.Security.Cryptography;
using System.Text;

namespace IT15_SOWCS.Services
{
    public static class DocumentFieldEncryption
    {
        private const string Prefix = "enc:";
        private static byte[]? _keyBytes;

        public static void Configure(string? keyMaterial)
        {
            if (string.IsNullOrWhiteSpace(keyMaterial))
            {
                throw new InvalidOperationException("Document encryption key is missing. Configure Security:DocumentEncryptionKey.");
            }

            _keyBytes = DeriveKey(keyMaterial.Trim());
        }

        public static string Encrypt(string? plainText)
        {
            if (string.IsNullOrWhiteSpace(plainText))
            {
                return plainText ?? string.Empty;
            }

            if (plainText.StartsWith(Prefix, StringComparison.Ordinal))
            {
                return plainText;
            }

            using var aes = Aes.Create();
            aes.Key = GetKeyBytes();
            aes.GenerateIV();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor();
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

            var payload = new byte[aes.IV.Length + cipherBytes.Length];
            Buffer.BlockCopy(aes.IV, 0, payload, 0, aes.IV.Length);
            Buffer.BlockCopy(cipherBytes, 0, payload, aes.IV.Length, cipherBytes.Length);
            return Prefix + Convert.ToBase64String(payload);
        }

        public static string Decrypt(string? cipherText)
        {
            if (string.IsNullOrWhiteSpace(cipherText))
            {
                return cipherText ?? string.Empty;
            }

            if (!cipherText.StartsWith(Prefix, StringComparison.Ordinal))
            {
                return cipherText;
            }

            try
            {
                var payload = Convert.FromBase64String(cipherText[Prefix.Length..]);
                using var aes = Aes.Create();
                aes.Key = GetKeyBytes();
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                var ivLength = aes.BlockSize / 8;
                var iv = payload[..ivLength];
                var cipherBytes = payload[ivLength..];
                aes.IV = iv;

                using var decryptor = aes.CreateDecryptor();
                var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
                return Encoding.UTF8.GetString(plainBytes);
            }
            catch
            {
                return cipherText;
            }
        }

        private static byte[] DeriveKey(string keyMaterial)
        {
            using var sha = SHA256.Create();
            return sha.ComputeHash(Encoding.UTF8.GetBytes(keyMaterial));
        }

        private static byte[] GetKeyBytes()
        {
            return _keyBytes ?? throw new InvalidOperationException("Document encryption key is not configured.");
        }
    }
}
