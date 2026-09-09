using System;
using System.Linq;
using UnityEngine;

namespace NorthLand.Core
{
    public class CryptoUtilTest : MonoBehaviour
    {
        [ContextMenu("암복호화 테스트")]
        private void RunTest()
        {
            try
            {
                string[] samples =
                {
                    "",
                    "1234567890123456", // 정확히 16바이트
                    "{\"Gold\":100,\"Name\":\"테스트 타워\"}"
                };

                foreach (string original in samples)
                {
                    byte[] encrypted = CryptoUtil.Encrypt(original);
                    string restored = CryptoUtil.Decrypt(encrypted);

                    if (original != restored)
                        throw new Exception("원본과 복호화 결과가 다릅니다.");
                }

                Debug.Log("문자열 복원 테스트 통과: 빈 문자열 / 16바이트 / 한글 JSON");

                const string json = "{\"Gold\":100}";

                byte[] first = CryptoUtil.Encrypt(json);
                byte[] second = CryptoUtil.Encrypt(json);

                if (first.Take(16).SequenceEqual(second.Take(16)))
                    throw new Exception("두 암호화 결과의 IV가 같습니다.");

                if (first.SequenceEqual(second))
                    throw new Exception("두 암호화 결과가 같습니다.");

                Debug.Log("랜덤 IV 테스트 통과: 같은 원문도 다른 암호문 생성");

                bool invalidLengthRejected = false;

                try
                {
                    CryptoUtil.Decrypt(new byte[17]);
                }
                catch (ArgumentException)
                {
                    invalidLengthRejected = true;
                }

                if (!invalidLengthRejected)
                    throw new Exception("잘못된 길이의 데이터가 거부되지 않았습니다.");

                Debug.Log("입력 길이 검사 통과");
                Debug.Log("암복호화 테스트 전체 통과");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }
    }
}