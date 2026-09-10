using System;
using System.Security.Cryptography;
using System.Text;

namespace NorthLand.Core
{
    public static class CryptoUtil
    {
        private const int IvSize = 16;

        // 잘못된 UTF-8 데이터는 오류로 처리한다.
        private static readonly Encoding Utf8 = new UTF8Encoding(false, true);

        private static readonly byte[] KeyPartA =
        {
            0x7A, 0xC1, 0x4F, 0x92,
            0x16, 0xE8, 0x3B, 0xD5,
            0xA4, 0x69, 0x2D, 0xF0,
            0x81, 0x57, 0xBC, 0x23
        };

        private static readonly byte[] KeyPartB =
        {
            0x34, 0xED, 0x71, 0x08,
            0xC6, 0x9A, 0x42, 0xBF,
            0x15, 0xD3, 0x88, 0x5C,
            0xF7, 0x20, 0xAB, 0x61
        };

        private static readonly byte[] KeySalt =
        {
            0x9D, 0x26, 0xE3, 0x74,
            0x0B, 0xB8, 0x51, 0xCF,
            0x63, 0x1A, 0xF5, 0x97,
            0x48, 0xDC, 0x32, 0xAE
        };

        private static readonly byte[] Key = BuildKey();

        private static byte[] BuildKey()
        {
            int totalLength = KeyPartA.Length + KeySalt.Length + KeyPartB.Length;

            byte[] keyMaterial = new byte[totalLength];

            int offset = 0;

            Buffer.BlockCopy(KeyPartA,0,keyMaterial,offset,KeyPartA.Length);

            offset += KeyPartA.Length;

            Buffer.BlockCopy(KeySalt,0,keyMaterial,offset,KeySalt.Length);

            offset += KeySalt.Length;

            Buffer.BlockCopy(KeyPartB,0,keyMaterial,offset,KeyPartB.Length);

            using (SHA256 sha256 = SHA256.Create())
            {
                return sha256.ComputeHash(keyMaterial);
            }
        }


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

            if (encryptedBytes.Length < IvSize * 2 || (encryptedBytes.Length - IvSize) % IvSize != 0)
            {
                throw new ArgumentException("암호화 데이터의 길이가 올바르지 않습니다.",nameof(encryptedBytes));
            }

            byte[] iv = new byte[IvSize];

            Buffer.BlockCopy(encryptedBytes,0,iv,0,IvSize);

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
                    byte[] plainBytes =decryptor.TransformFinalBlock(encryptedBytes,IvSize,encryptedBytes.Length - IvSize);

                    return Utf8.GetString(plainBytes);
                }
            }
        }
    }
}