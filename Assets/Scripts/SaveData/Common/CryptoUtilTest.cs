using System;
using UnityEngine;

namespace NorthLand.Core
{
    public sealed class CryptoUtilTest : MonoBehaviour
    {
        [ContextMenu("Test Crypto Round Trip")]
        private void TestRoundTrip()
        {
            const string original = "Marshmallow Land Save Test";

            byte[] encrypted = CryptoUtil.Encrypt(original);
            string decrypted = CryptoUtil.Decrypt(encrypted);

            Debug.Log($"Original : {original}");
            Debug.Log($"Decrypted: {decrypted}");
            Debug.Log(original == decrypted
                ? "Crypto RoundTrip 성공"
                : "Crypto RoundTrip 실패");
        }

        [ContextMenu("Test Crypto Korean")]
        private void TestKorean()
        {
            const string original = "마시멜로우 왕국 세이브 테스트";

            byte[] encrypted = CryptoUtil.Encrypt(original);
            string decrypted = CryptoUtil.Decrypt(encrypted);

            Debug.Log(original == decrypted
                ? "한글 암복호화 성공"
                : "한글 암복호화 실패");
        }

        [ContextMenu("Test Random IV")]
        private void TestRandomIv()
        {
            const string original = "Same Save Data";

            byte[] encryptedA = CryptoUtil.Encrypt(original);
            byte[] encryptedB = CryptoUtil.Encrypt(original);

            bool same = ByteArraysEqual(encryptedA, encryptedB);

            Debug.Log(!same
                ? "랜덤 IV 테스트 성공: 암호문이 서로 다름"
                : "랜덤 IV 테스트 실패: 암호문이 동일함");
        }

        [ContextMenu("Test Invalid Data")]
        private void TestInvalidData()
        {
            byte[] invalidData = new byte[10];

            try
            {
                CryptoUtil.Decrypt(invalidData);
                Debug.LogError("잘못된 데이터인데 복호화가 성공했습니다.");
            }
            catch (Exception exception)
            {
                Debug.Log($"잘못된 데이터 감지 성공: {exception.GetType().Name}");
            }
        }

        private static bool ByteArraysEqual(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
                return false;

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                    return false;
            }

            return true;
        }
    }
}