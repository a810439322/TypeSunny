using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;

namespace TypeSunny.Net
{
    /// <summary>
    /// 赛文加密客户端（新版）
    /// AES-256-GCM + RSA-OAEP-SHA256 + RSA-SHA256签名
    /// 使用 BouncyCastle 实现 AES-GCM（.NET Framework 4.8 不支持 AesGcm）
    /// 使用 RSACng 实现 RSA-OAEP-SHA256
    /// </summary>
    public class RaceCryptoClient
    {
        private RSACng clientRsaCng;  // 客户端密钥对（RSACng 支持 OAEP-SHA256）

        /// <summary>
        /// 初始化加密客户端（仅管理客户端密钥对）
        /// </summary>
        /// <param name="clientKeyXml">客户端的RSA密钥对（XML格式，可选）</param>
        public RaceCryptoClient(string clientKeyXml = null)
        {
            // 加载或生成客户端 RSA 密钥对
            if (!string.IsNullOrEmpty(clientKeyXml))
            {
                try
                {
                    // 先用 RSACryptoServiceProvider 解析 XML，再导入到 RSACng
                    using (var tempRsa = new RSACryptoServiceProvider(2048))
                    {
                        tempRsa.FromXmlString(clientKeyXml);
                        var parameters = tempRsa.ExportParameters(true);
                        clientRsaCng = new RSACng(2048);
                        clientRsaCng.ImportParameters(parameters);
                    }
                    System.Diagnostics.Debug.WriteLine("✓ 加载客户端已有密钥对（RSACng）");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠ 加载客户端密钥失败，将生成新密钥: {ex.Message}");
                    clientRsaCng = new RSACng(2048);
                }
            }
            else
            {
                clientRsaCng = new RSACng(2048);
                System.Diagnostics.Debug.WriteLine("✓ 生成新的客户端密钥对（RSACng）");
            }
        }

        /// <summary>
        /// 获取客户端密钥对的XML格式（用于持久化保存）
        /// </summary>
        public string GetClientKeyXml()
        {
            var parameters = clientRsaCng.ExportParameters(true);
            using (var tempRsa = new RSACryptoServiceProvider(2048))
            {
                tempRsa.ImportParameters(parameters);
                return tempRsa.ToXmlString(true);
            }
        }

        /// <summary>
        /// 获取客户端公钥的PEM格式（用于注册时上传给服务器）
        /// </summary>
        public string GetClientPublicKeyPem()
        {
            RSAParameters publicParams = clientRsaCng.ExportParameters(false);
            byte[] spki = BuildSpkiBytes(publicParams);
            string base64 = Convert.ToBase64String(spki);

            var pem = new StringBuilder();
            pem.AppendLine("-----BEGIN PUBLIC KEY-----");
            for (int i = 0; i < base64.Length; i += 64)
            {
                int len = Math.Min(64, base64.Length - i);
                pem.AppendLine(base64.Substring(i, len));
            }
            pem.Append("-----END PUBLIC KEY-----");
            return pem.ToString();
        }

        /// <summary>
        /// 获取客户端公钥的 Base64 格式（不含 PEM 头尾，用于注册请求）
        /// </summary>
        public string GetClientPublicKeyBase64()
        {
            RSAParameters publicParams = clientRsaCng.ExportParameters(false);
            byte[] spki = BuildSpkiBytes(publicParams);
            return Convert.ToBase64String(spki);
        }

        // ==================== 解密 ====================

        /// <summary>
        /// 解密 init 响应的加密 envelope
        /// 新格式：{ encryptedKey(Base64), iv(Base64), encryptedData(Base64) }
        /// 加密方式：AES-256-GCM，密钥用客户端公钥 RSA-OAEP-SHA256 加密
        /// </summary>
        public JObject DecryptInitResponse(JObject envelope)
        {
            try
            {
                // 1. 从 envelope 取各字段
                string encryptedKeyB64 = envelope["encryptedKey"]?.ToString();
                string ivB64 = envelope["iv"]?.ToString();
                string encryptedDataB64 = envelope["encryptedData"]?.ToString();

                if (string.IsNullOrEmpty(encryptedKeyB64) || string.IsNullOrEmpty(ivB64) || string.IsNullOrEmpty(encryptedDataB64))
                    throw new Exception("加密 envelope 缺少必要字段（encryptedKey/iv/encryptedData）");

                // 2. RSA-OAEP-SHA256 解密 AES key
                byte[] encryptedKeyBytes = Convert.FromBase64String(encryptedKeyB64);
                byte[] aesKeyBase64Bytes = clientRsaCng.Decrypt(encryptedKeyBytes, RSAEncryptionPadding.OaepSHA256);
                // 服务端加密的是 AES key 的 Base64 字符串，需要再 Base64 解码
                string aesKeyBase64Str = Encoding.UTF8.GetString(aesKeyBase64Bytes);
                byte[] aesKey = Convert.FromBase64String(aesKeyBase64Str);

                // 3. Base64 解码 IV（12 字节，GCM 标准）
                byte[] iv = Convert.FromBase64String(ivB64);

                // 4. Base64 解码加密数据（包含 GCM tag）
                byte[] cipherWithTag = Convert.FromBase64String(encryptedDataB64);

                // 5. AES-256-GCM 解密（使用 BouncyCastle）
                byte[] plaintext = AesGcmDecrypt(aesKey, iv, cipherWithTag);

                // 6. 解析为 JSON
                string jsonStr = Encoding.UTF8.GetString(plaintext);
                System.Diagnostics.Debug.WriteLine($"[赛文加密] ✓ 解密 init 响应成功，JSON长度: {jsonStr.Length}");
                return JObject.Parse(jsonStr);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[赛文加密] ✗ 解密 init 响应失败: {ex.Message}");
                throw new Exception($"解密赛文 init 响应失败: {ex.Message}", ex);
            }
        }

        // ==================== 加密 ====================

        /// <summary>
        /// 用服务器公钥加密提交数据
        /// </summary>
        /// <param name="jsonPayload">要加密的 JSON 字符串</param>
        /// <param name="serverPublicKeyBase64">服务器公钥（Base64 编码的 SPKI）</param>
        /// <returns>加密后的 JSON envelope { encryptedKey, iv, encryptedData }</returns>
        public JObject EncryptForServer(string jsonPayload, string serverPublicKeyBase64)
        {
            // 1. 生成随机 AES-256 key 和 12 字节 IV
            byte[] aesKey = new byte[32];
            byte[] iv = new byte[12];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(aesKey);
                rng.GetBytes(iv);
            }

            // 2. AES-256-GCM 加密
            byte[] plaintext = Encoding.UTF8.GetBytes(jsonPayload);
            byte[] cipherWithTag = AesGcmEncrypt(aesKey, iv, plaintext);

            // 3. 将 AES key 转为 Base64 字符串，再用服务器公钥 RSA-OAEP-SHA256 加密
            string aesKeyBase64Str = Convert.ToBase64String(aesKey);
            byte[] aesKeyBase64Bytes = Encoding.UTF8.GetBytes(aesKeyBase64Str);

            using (var serverRsa = new RSACng(2048))
            {
                byte[] serverPubBytes = Convert.FromBase64String(serverPublicKeyBase64);
                RSAParameters serverParams = DecodeX509PublicKey(serverPubBytes);
                serverRsa.ImportParameters(serverParams);

                byte[] encryptedKey = serverRsa.Encrypt(aesKeyBase64Bytes, RSAEncryptionPadding.OaepSHA256);

                // 4. 构造 envelope
                var envelope = new JObject
                {
                    ["encryptedKey"] = Convert.ToBase64String(encryptedKey),
                    ["iv"] = Convert.ToBase64String(iv),
                    ["encryptedData"] = Convert.ToBase64String(cipherWithTag)
                };
                return envelope;
            }
        }

        // ==================== 签名 ====================

        /// <summary>
        /// 用客户端私钥 RSA-SHA256 签名
        /// </summary>
        /// <param name="jsonPayload">要签名的 JSON 字符串</param>
        /// <returns>Base64 编码的签名</returns>
        public string SignPayload(string jsonPayload)
        {
            byte[] data = Encoding.UTF8.GetBytes(jsonPayload);
            byte[] signature = clientRsaCng.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            return Convert.ToBase64String(signature);
        }

        // ==================== AES-GCM（BouncyCastle） ====================

        /// <summary>
        /// AES-256-GCM 解密（BouncyCastle 实现）
        /// 输入的 cipherWithTag 包含密文 + 16字节 GCM tag
        /// </summary>
        private static byte[] AesGcmDecrypt(byte[] key, byte[] iv, byte[] cipherWithTag)
        {
            var cipher = new GcmBlockCipher(new AesEngine());
            var parameters = new AeadParameters(new KeyParameter(key), 128, iv);
            cipher.Init(false, parameters);

            byte[] output = new byte[cipher.GetOutputSize(cipherWithTag.Length)];
            int len = cipher.ProcessBytes(cipherWithTag, 0, cipherWithTag.Length, output, 0);
            len += cipher.DoFinal(output, len);

            // DoFinal 验证 tag，如果 tag 不匹配会抛异常
            byte[] result = new byte[len];
            Array.Copy(output, 0, result, 0, len);
            return result;
        }

        /// <summary>
        /// AES-256-GCM 加密（BouncyCastle 实现）
        /// 返回密文 + 16字节 GCM tag
        /// </summary>
        private static byte[] AesGcmEncrypt(byte[] key, byte[] iv, byte[] plaintext)
        {
            var cipher = new GcmBlockCipher(new AesEngine());
            var parameters = new AeadParameters(new KeyParameter(key), 128, iv);
            cipher.Init(true, parameters);

            byte[] output = new byte[cipher.GetOutputSize(plaintext.Length)];
            int len = cipher.ProcessBytes(plaintext, 0, plaintext.Length, output, 0);
            len += cipher.DoFinal(output, len);

            byte[] result = new byte[len];
            Array.Copy(output, 0, result, 0, len);
            return result;
        }

        // ==================== ASN.1 / 公钥工具方法 ====================

        /// <summary>
        /// 构建 SubjectPublicKeyInfo (SPKI) 格式的公钥字节
        /// </summary>
        private static byte[] BuildSpkiBytes(RSAParameters publicParams)
        {
            byte[] modulusBytes = publicParams.Modulus;
            byte[] exponentBytes = publicParams.Exponent;

            // 构建 RSAPublicKey SEQUENCE { modulus INTEGER, exponent INTEGER }
            byte[] rsaPublicKey;
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                WriteAsn1Integer(writer, modulusBytes);
                WriteAsn1Integer(writer, exponentBytes);
                rsaPublicKey = ms.ToArray();
            }

            // 包装为 SEQUENCE
            byte[] rsaPublicKeySeq;
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write((byte)0x30);
                WriteAsn1Length(writer, rsaPublicKey.Length);
                writer.Write(rsaPublicKey);
                rsaPublicKeySeq = ms.ToArray();
            }

            // 构建完整的 SubjectPublicKeyInfo
            byte[] spki;
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write((byte)0x30); // SEQUENCE tag

                // AlgorithmIdentifier: SEQUENCE { OID rsaEncryption, NULL }
                byte[] algorithmId = new byte[] {
                    0x30, 0x0D,
                    0x06, 0x09, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x01, 0x01,
                    0x05, 0x00
                };

                // BIT STRING 包装
                int bitStringContentLen = rsaPublicKeySeq.Length + 1;
                byte[] bitStringHeader;
                using (var bsMs = new MemoryStream())
                using (var bsWriter = new BinaryWriter(bsMs))
                {
                    bsWriter.Write((byte)0x03);
                    WriteAsn1Length(bsWriter, bitStringContentLen);
                    bsWriter.Write((byte)0x00);
                    bsWriter.Write(rsaPublicKeySeq);
                    bitStringHeader = bsMs.ToArray();
                }

                int totalLen = algorithmId.Length + bitStringHeader.Length;
                WriteAsn1Length(writer, totalLen);
                writer.Write(algorithmId);
                writer.Write(bitStringHeader);

                spki = ms.ToArray();
            }

            return spki;
        }

        /// <summary>
        /// 解析 X.509 SubjectPublicKeyInfo 格式的 RSA 公钥
        /// </summary>
        private static RSAParameters DecodeX509PublicKey(byte[] x509Key)
        {
            byte[] seqOid = { 0x30, 0x0D, 0x06, 0x09, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x01, 0x01, 0x05, 0x00 };

            using (var stream = new MemoryStream(x509Key))
            using (var reader = new BinaryReader(stream))
            {
                ushort twobytes = reader.ReadUInt16();
                if (twobytes == 0x8130)
                    reader.ReadByte();
                else if (twobytes == 0x8230)
                    reader.ReadInt16();
                else
                    throw new Exception("意外的公钥格式");

                byte[] seq = reader.ReadBytes(15);
                if (!CompareByteArrays(seq, seqOid))
                    throw new Exception("意外的公钥格式");

                twobytes = reader.ReadUInt16();
                if (twobytes == 0x8103)
                    reader.ReadByte();
                else if (twobytes == 0x8203)
                    reader.ReadInt16();
                else
                    throw new Exception("意外的公钥格式");

                if (reader.ReadByte() != 0x00)
                    throw new Exception("意外的公钥格式");

                twobytes = reader.ReadUInt16();
                if (twobytes == 0x8130)
                    reader.ReadByte();
                else if (twobytes == 0x8230)
                    reader.ReadInt16();
                else
                    throw new Exception("意外的公钥格式");

                twobytes = reader.ReadUInt16();
                byte lowbyte = 0, highbyte = 0;

                if (twobytes == 0x8102)
                    lowbyte = reader.ReadByte();
                else if (twobytes == 0x8202)
                {
                    highbyte = reader.ReadByte();
                    lowbyte = reader.ReadByte();
                }
                else
                    throw new Exception("意外的公钥格式");

                byte[] modint = { lowbyte, highbyte, 0x00, 0x00 };
                int modsize = BitConverter.ToInt32(modint, 0);

                byte firstbyte = reader.ReadByte();
                reader.BaseStream.Seek(-1, SeekOrigin.Current);

                if (firstbyte == 0x00)
                {
                    reader.ReadByte();
                    modsize -= 1;
                }

                byte[] modulus = reader.ReadBytes(modsize);

                if (reader.ReadByte() != 0x02)
                    throw new Exception("意外的公钥格式");

                int expbytes = reader.ReadByte();
                byte[] exponent = reader.ReadBytes(expbytes);

                return new RSAParameters { Modulus = modulus, Exponent = exponent };
            }
        }

        private static void WriteAsn1Integer(BinaryWriter writer, byte[] value)
        {
            writer.Write((byte)0x02);
            int offset = 0;
            while (offset < value.Length - 1 && value[offset] == 0)
                offset++;

            bool needPadding = value[offset] >= 0x80;
            int length = value.Length - offset + (needPadding ? 1 : 0);

            WriteAsn1Length(writer, length);
            if (needPadding)
                writer.Write((byte)0x00);
            writer.Write(value, offset, value.Length - offset);
        }

        private static void WriteAsn1Length(BinaryWriter writer, int length)
        {
            if (length < 0x80)
                writer.Write((byte)length);
            else if (length <= 0xFF)
            {
                writer.Write((byte)0x81);
                writer.Write((byte)length);
            }
            else if (length <= 0xFFFF)
            {
                writer.Write((byte)0x82);
                writer.Write((byte)(length >> 8));
                writer.Write((byte)(length & 0xFF));
            }
            else
                throw new Exception("长度值过大");
        }

        private static bool CompareByteArrays(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }
    }
}