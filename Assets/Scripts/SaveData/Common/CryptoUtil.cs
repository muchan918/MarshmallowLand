using System;
using System.Security.Cryptography;
using System.Text;

namespace NorthLand.Core
{
    public static class CryptoUtil
    {
        private const string KeyHex = "536176654C6F616453747564795F4145533235365F4B65795F32303235212100";

        private const int IvSize = 16;

        private static readonly byte[] Key = ParseHex(KeyHex);

        // 잘못된 UTF-8 데이터는 오류로 처리한다.
        private static readonly Encoding Utf8 = new UTF8Encoding(false, true);

        public static byte[] Encrypt(string plainText)
        {
            if (plainText == null)
                throw new ArgumentNullException(nameof(plainText));

            using (Aes aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.BlockSize = 128;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = Key;
                aes.GenerateIV();

                byte[] plainBytes = Utf8.GetBytes(plainText);

                using (ICryptoTransform encryptor = aes.CreateEncryptor())
                {
                    byte[] cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

                    // 앞 16바이트는 IV, 나머지는 암호문.
                    byte[] result = new byte[IvSize + cipherBytes.Length];

                    Buffer.BlockCopy(aes.IV, 0, result, 0, IvSize);
                    Buffer.BlockCopy(cipherBytes, 0, result, IvSize, cipherBytes.Length);

                    return result;
                }
            }
        }

        public static string Decrypt(byte[] encryptedBytes)
        {
            if (encryptedBytes == null)
                throw new ArgumentNullException(nameof(encryptedBytes));

            // IV 16바이트 + 최소 암호문 한 블록이 필요하다.
            if (encryptedBytes.Length < IvSize * 2 ||
                (encryptedBytes.Length - IvSize) % IvSize != 0)
            {
                throw new ArgumentException("암호화 데이터의 길이가 올바르지 않습니다.",nameof(encryptedBytes));
            }

            byte[] iv = new byte[IvSize];
            Buffer.BlockCopy(encryptedBytes, 0, iv, 0, IvSize);

            using (Aes aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.BlockSize = 128;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = Key;
                aes.IV = iv;

                using (ICryptoTransform decryptor = aes.CreateDecryptor())
                {
                    byte[] plainBytes = decryptor.TransformFinalBlock(encryptedBytes,IvSize,encryptedBytes.Length - IvSize);

                    return Utf8.GetString(plainBytes);
                }
            }
        }

        private static byte[] ParseHex(string hex)
        {
            if (hex == null || hex.Length != 64)
            {
                throw new ArgumentException("AES-256 키는 64자리 16진수 문자열이어야 합니다.");
            }

            byte[] bytes = new byte[hex.Length / 2];

            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }

            return bytes;
        }
    }
}