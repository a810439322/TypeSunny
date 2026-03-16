using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace TypeSunny.Utils
{
    /// <summary>
    /// 密码加密工具类
    /// 使用 AES-256-CBC 加密，密钥硬编码
    /// </summary>
    public static class PasswordCrypto
    {
        // AES-256 需要 32 字节密钥
        private static readonly byte[] Key = new byte[32]
        {
            0x54, 0x79, 0x70, 0x65, 0x53, 0x75, 0x6E, 0x6E,
            0x79, 0x53, 0x65, 0x63, 0x4B, 0x65, 0x79, 0x33,
            0x32, 0x42, 0x79, 0x74, 0x65, 0x73, 0x21, 0x40,
            0x23, 0x24, 0x25, 0x5E, 0x26, 0x2A, 0x28, 0x29
        };

        public static string Encrypt(string plainText)
        {
            if (string.IsNullOrWhiteSpace(plainText))
                return "";

            using (var aes = Aes.Create())
            {
                aes.Key = Key;
                aes.GenerateIV(); // 随机 IV，每次加密结果不同
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var encryptor = aes.CreateEncryptor())
                using (var ms = new MemoryStream())
                {
                    // 将 IV 写入密文开头
                    ms.Write(aes.IV, 0, aes.IV.Length);

                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    using (var sw = new StreamWriter(cs))
                    {
                        sw.Write(plainText);
                    }
                    return Convert.ToBase64String(ms.ToArray());
                }
            }
        }

        public static string Decrypt(string cipherText)
        {
            if (string.IsNullOrWhiteSpace(cipherText))
                return "";

            try
            {
                byte[] cipherBytes = Convert.FromBase64String(cipherText);

                using (var aes = Aes.Create())
                {
                    aes.Key = Key;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    // 从密文开头提取 IV
                    byte[] iv = new byte[16];
                    if (cipherBytes.Length < iv.Length)
                        return "";

                    Array.Copy(cipherBytes, 0, iv, 0, iv.Length);
                    aes.IV = iv;

                    using (var decryptor = aes.CreateDecryptor())
                    using (var ms = new MemoryStream(cipherBytes, iv.Length, cipherBytes.Length - iv.Length))
                    using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                    using (var sr = new StreamReader(cs))
                    {
                        return sr.ReadToEnd();
                    }
                }
            }
            catch
            {
                return "";
            }
        }
    }
}
