using System;
using System.IO;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace NorthLand.Core
{
    /// <summary>
    /// 단일 Run 세이브 파일의 경로와 파일 IO를 담당한다.
    /// JSON 변환이나 게임 상태 수집·복원은 담당하지 않는다.
    /// </summary>
    public sealed class SaveFileStore
    {
        private const string DefaultSaveFileName = "run-save.json";

        private readonly bool useEncryption;

        // 암호화 파일 식별자와 형식 버전.
        private static readonly byte[] EncryptionHeader = Encoding.ASCII.GetBytes("NLSAVE01");

        private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

        public string SavePath { get; }

        public bool Exists => File.Exists(SavePath);

        public SaveFileStore(string directoryPath) : this(directoryPath, DefaultSaveFileName, true)
        {
        }

        public SaveFileStore(string directoryPath,string saveFileName,bool useEncryption = true)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                throw new ArgumentException("세이브 디렉터리 경로가 비어 있습니다.",nameof(directoryPath));
            }

            if (string.IsNullOrWhiteSpace(saveFileName))
            {
                throw new ArgumentException("세이브 파일 이름이 비어 있습니다.",nameof(saveFileName));
            }

            SavePath = Path.Combine(directoryPath, saveFileName);
            this.useEncryption = useEncryption;
        }
        

        private byte[] EncodeFile(string json)
        {
            if (!useEncryption)
                return StrictUtf8.GetBytes(json);

            byte[] encrypted = CryptoUtil.Encrypt(json);
            byte[] result =
                new byte[EncryptionHeader.Length + encrypted.Length];

            Buffer.BlockCopy(
                EncryptionHeader, 0,
                result, 0,
                EncryptionHeader.Length);

            Buffer.BlockCopy(
                encrypted, 0,
                result, EncryptionHeader.Length,
                encrypted.Length);

            return result;
        }

        private string DecodeFile(byte[] bytes)
        {
            if (HasEncryptionHeader(bytes))
            {
                int encryptedLength = bytes.Length - EncryptionHeader.Length;
                byte[] encrypted = new byte[encryptedLength];

                Buffer.BlockCopy(bytes, EncryptionHeader.Length,encrypted, 0,encryptedLength);

                // 복호화 실패 시 예외를 전달한다.
                // 실패한 암호문을 평문으로 재해석하지 않는다.
                return CryptoUtil.Decrypt(encrypted);
            }

            // 기존 UTF-8 평문 파일의 BOM을 허용한다.
            int offset = 0;

            if (bytes.Length >= 3 &&bytes[0] == 0xEF &&bytes[1] == 0xBB &&bytes[2] == 0xBF)
            {
                offset = 3;
            }

            string json = StrictUtf8.GetString(bytes, offset, bytes.Length - offset);

            // 이 프로젝트의 기존 세이브는 JSON 객체 형식이다.
            if (!json.TrimStart().StartsWith("{", StringComparison.Ordinal))
            {
                throw new FormatException("지원하지 않는 세이브 파일 형식입니다.");
            }

            // 실제 JSON 파싱·버전 검증은 기존 Serializer가 담당한다.
            return json;
        }

        private static bool HasEncryptionHeader(byte[] bytes)
        {
            if (bytes.Length < EncryptionHeader.Length)
                return false;

            for (int i = 0; i < EncryptionHeader.Length; i++)
            {
                if (bytes[i] != EncryptionHeader[i])
                    return false;
            }

            return true;
        }


        /// <summary>
        /// 저장 파일의 JSON 문자열을 읽는다.
        /// 파일이 없거나 읽기에 실패하면 false를 반환한다.
        /// </summary>
        public bool TryRead(out string json, out string error)
        {
            json = null;
            error = null;

            if (!Exists)
            {
                error = "세이브 파일이 없습니다.";
                return false;
            }

            try
            {
                byte[] bytes = File.ReadAllBytes(SavePath);
                json = DecodeFile(bytes);
                return true;
            }
            catch (Exception exception)
            {
                error = $"세이브 파일을 읽을 수 없습니다: {exception.Message}";

                return false;
            }
        }
        /// <summary>
        /// JSON을 임시 파일에 먼저 기록한 뒤 실제 세이브 파일로 교체한다.
        /// 기록 도중 실패하면 기존 세이브 파일은 유지한다.
        /// </summary>
        public bool TryWrite(string json, out string error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "저장할 JSON이 비어 있습니다.";
                return false;
            }

            string directoryPath = Path.GetDirectoryName(SavePath);

            string temporaryPath = SavePath + ".tmp";

            try
            {
                byte[] bytes = EncodeFile(json);

                Directory.CreateDirectory(directoryPath);
                File.WriteAllBytes(temporaryPath, bytes);

                if (File.Exists(SavePath))
                {
                    File.Replace(temporaryPath,SavePath,null);
                }
                else
                {
                    File.Move(temporaryPath,SavePath);
                }

                return true;
            }
            catch (Exception exception)
            {
                error = $"세이브 파일을 기록할 수 없습니다: {exception.Message}";

                try
                {
                    if (File.Exists(temporaryPath))
                        File.Delete(temporaryPath);
                }
                catch (Exception cleanupException)
                {
                    error += $" 임시 파일 정리도 실패했습니다: {cleanupException.Message}";
                }

                return false;
            }
        }

        public async UniTask<SaveResult> WriteAsync(string json,CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return SaveResult.Failed("저장할 JSON이 비어 있습니다.");
            }

            try
            {
                return await UniTask.RunOnThreadPool(() =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (!TryWrite(json, out string error))
                        {
                            return SaveResult.Failed(error);
                        }

                        return SaveResult.Succeeded();
                    },
                    cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
        }

        public async UniTask<SaveResult<string>> ReadAsync(CancellationToken cancellationToken)
        {
            try
            {
                return await UniTask.RunOnThreadPool(() =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (!TryRead(out string json, out string error))
                        {
                            return SaveResult<string>.Failed(error);
                        }

                        return SaveResult<string>.Succeeded(json);
                    },
                    cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
        }

        /// <summary>
        /// 현재 Run 세이브와 남아 있는 임시 파일을 삭제한다.
        /// 파일이 이미 없어도 성공으로 처리한다.
        /// </summary>
        public bool TryDelete(out string error)
        {
            error = null;

            string temporaryPath = SavePath + ".tmp";

            try
            {
                if (File.Exists(SavePath))
                    File.Delete(SavePath);

                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);

                return true;
            }
            catch (Exception exception)
            {
                error = $"세이브 파일을 삭제할 수 없습니다: {exception.Message}";

                return false;
            }
        }
    }
}