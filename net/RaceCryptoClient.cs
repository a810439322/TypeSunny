using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TypeSunny.Net
{
    /// <summary>
    /// 赛文加密客户端（RSA+AES混合加密，双向加密）
    /// 用于与赛文API服务器进行加密通信
    /// </summary>
    public class RaceCryptoClient
    {
        private RSACryptoServiceProvider serverRsaProvider;  // 服务器公钥
        private RSACryptoServiceProvider clientRsaProvider;  // 客户端密钥对
        private string serverPublicKey;

        /// <summary>
        /// 初始化加密客户端
        /// </summary>
        /// <param name="publicKeyPem">服务器的RSA公钥（PEM格式）</param>
        /// <param name="clientKeyXml">客户端的RSA密钥对（XML格式，可选）</param>
        public RaceCryptoClient(string publicKeyPem, string clientKeyXml = null)
        {
            this.serverPublicKey = publicKeyPem;

            // 导入服务器公钥
            this.serverRsaProvider = new RSACryptoServiceProvider(2048);
            ImportPublicKey(publicKeyPem, this.serverRsaProvider);

            // 加载或生成客户端自己的RSA密钥对（用于接收服务器加密的响应）
            this.clientRsaProvider = new RSACryptoServiceProvider(2048);

            if (!string.IsNullOrEmpty(clientKeyXml))
            {
                // 加载已有的密钥对
                try
                {
                    this.clientRsaProvider.FromXmlString(clientKeyXml);
                    System.Diagnostics.Debug.WriteLine("✓ 加载客户端已有密钥对");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠ 加载客户端密钥失败，将生成新密钥: {ex.Message}");
                    // 如果加载失败，会使用新生成的密钥对
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("✓ 生成新的客户端密钥对");
            }
        }

        /// <summary>
        /// 获取客户端密钥对的XML格式（用于持久化保存）
        /// </summary>
        public string GetClientKeyXml()
        {
            return clientRsaProvider.ToXmlString(true);  // true = 包含私钥
        }

        /// <summary>
        /// 导入PEM格式的RSA公钥
        /// </summary>
        private void ImportPublicKey(string publicKeyPem, RSACryptoServiceProvider rsaProvider)
        {
            try
            {
                // 移除PEM头尾
                string publicKeyBase64 = publicKeyPem
                    .Replace("-----BEGIN RSA PUBLIC KEY-----", "")
                    .Replace("-----END RSA PUBLIC KEY-----", "")
                    .Replace("-----BEGIN PUBLIC KEY-----", "")
                    .Replace("-----END PUBLIC KEY-----", "")
                    .Replace("\n", "")
                    .Replace("\r", "")
                    .Trim();

                byte[] publicKeyBytes = Convert.FromBase64String(publicKeyBase64);

                // .NET Framework 需要使用 ImportParameters 方法
                // 解析 X.509 SubjectPublicKeyInfo 格式的公钥
                RSAParameters parameters = DecodeX509PublicKey(publicKeyBytes);
                rsaProvider.ImportParameters(parameters);
            }
            catch (Exception ex)
            {
                throw new Exception($"导入RSA公钥失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 获取客户端公钥的PEM格式（用于注册/登录时上传给服务器）
        /// </summary>
        public string GetClientPublicKeyPem()
        {
            try
            {
                // 导出公钥参数
                RSAParameters publicParams = clientRsaProvider.ExportParameters(false);

                System.Diagnostics.Debug.WriteLine("=== 客户端密钥信息 ===");
                System.Diagnostics.Debug.WriteLine($"模数长度: {publicParams.Modulus.Length} 字节");
                System.Diagnostics.Debug.WriteLine($"指数长度: {publicParams.Exponent.Length} 字节");
                System.Diagnostics.Debug.WriteLine($"模数前16字节: {BitConverter.ToString(publicParams.Modulus, 0, Math.Min(16, publicParams.Modulus.Length))}");

                try
                {
                    // 手动构建RSA公钥SEQUENCE
                    byte[] modulusBytes = publicParams.Modulus;
                    byte[] exponentBytes = publicParams.Exponent;

                    // 构建 RSAPublicKey ::= SEQUENCE { modulus INTEGER, exponent INTEGER }
                    byte[] rsaPublicKey;
                    using (var ms = new MemoryStream())
                    using (var writer = new BinaryWriter(ms))
                    {
                        WriteAsn1Integer(writer, modulusBytes);
                        WriteAsn1Integer(writer, exponentBytes);
                        rsaPublicKey = ms.ToArray();
                    }

                    // 包装为SEQUENCE
                    byte[] rsaPublicKeySeq;
                    using (var ms = new MemoryStream())
                    using (var writer = new BinaryWriter(ms))
                    {
                        writer.Write((byte)0x30); // SEQUENCE
                        WriteAsn1Length(writer, rsaPublicKey.Length);
                        writer.Write(rsaPublicKey);
                        rsaPublicKeySeq = ms.ToArray();
                    }

                    // 构建完整的SubjectPublicKeyInfo
                    byte[] spki;
                    using (var ms = new MemoryStream())
                    using (var writer = new BinaryWriter(ms))
                    {
                        // 外层SEQUENCE
                        writer.Write((byte)0x30); // SEQUENCE tag

                        // AlgorithmIdentifier: SEQUENCE { OID, NULL }
                        byte[] algorithmId = new byte[] {
                            0x30, 0x0D, // SEQUENCE, length 13
                            0x06, 0x09, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x01, 0x01, // OID 1.2.840.113549.1.1.1
                            0x05, 0x00  // NULL
                        };

                        // BIT STRING包装
                        int bitStringContentLen = rsaPublicKeySeq.Length + 1; // +1 for unused bits byte
                        byte[] bitStringHeader;
                        using (var bsMs = new MemoryStream())
                        using (var bsWriter = new BinaryWriter(bsMs))
                        {
                            bsWriter.Write((byte)0x03); // BIT STRING tag
                            WriteAsn1Length(bsWriter, bitStringContentLen);
                            bsWriter.Write((byte)0x00); // no unused bits
                            bsWriter.Write(rsaPublicKeySeq);
                            bitStringHeader = bsMs.ToArray();
                        }

                        // 计算总长度并写入
                        int totalLen = algorithmId.Length + bitStringHeader.Length;
                        WriteAsn1Length(writer, totalLen);
                        writer.Write(algorithmId);
                        writer.Write(bitStringHeader);

                        spki = ms.ToArray();
                    }

                    // 转换为Base64并添加PEM头尾
                    string base64 = Convert.ToBase64String(spki);
                    StringBuilder pem = new StringBuilder();
                    pem.AppendLine("-----BEGIN PUBLIC KEY-----");

                    // 每行64字符
                    for (int i = 0; i < base64.Length; i += 64)
                    {
                        int len = Math.Min(64, base64.Length - i);
                        pem.AppendLine(base64.Substring(i, len));
                    }

                    pem.Append("-----END PUBLIC KEY-----");

                    string result = pem.ToString();
                    System.Diagnostics.Debug.WriteLine($"客户端公钥长度: {result.Length} 字符");

                    return result;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"导出公钥失败: {ex.Message}");
                    throw;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"导出客户端公钥失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 写入ASN.1 INTEGER
        /// </summary>
        private void WriteAsn1Integer(BinaryWriter writer, byte[] value)
        {
            writer.Write((byte)0x02); // INTEGER tag

            // 去掉前导零（但如果最高位是1，需要保留一个0以表示正数）
            int offset = 0;
            while (offset < value.Length - 1 && value[offset] == 0)
                offset++;

            // 检查最高位，如果是1需要添加0x00前缀
            bool needPadding = value[offset] >= 0x80;
            int length = value.Length - offset + (needPadding ? 1 : 0);

            WriteAsn1Length(writer, length);

            if (needPadding)
                writer.Write((byte)0x00);

            writer.Write(value, offset, value.Length - offset);
        }

        /// <summary>
        /// 写入ASN.1长度字段
        /// </summary>
        private void WriteAsn1Length(BinaryWriter writer, int length)
        {
            if (length < 0x80)
            {
                writer.Write((byte)length);
            }
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
            {
                throw new Exception("长度值过大");
            }
        }

        /// <summary>
        /// 解析 X.509 SubjectPublicKeyInfo 格式的 RSA 公钥
        /// </summary>
        private RSAParameters DecodeX509PublicKey(byte[] x509Key)
        {
            byte[] seqOid = { 0x30, 0x0D, 0x06, 0x09, 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x01, 0x01, 0x05, 0x00 };

            using (var stream = new MemoryStream(x509Key))
            using (var reader = new BinaryReader(stream))
            {
                byte bt;
                ushort twobytes;

                twobytes = reader.ReadUInt16();
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

                bt = reader.ReadByte();
                if (bt != 0x00)
                    throw new Exception("意外的公钥格式");

                twobytes = reader.ReadUInt16();
                if (twobytes == 0x8130)
                    reader.ReadByte();
                else if (twobytes == 0x8230)
                    reader.ReadInt16();
                else
                    throw new Exception("意外的公钥格式");

                twobytes = reader.ReadUInt16();
                byte lowbyte = 0;
                byte highbyte = 0;

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

                RSAParameters rsaParams = new RSAParameters
                {
                    Modulus = modulus,
                    Exponent = exponent
                };

                return rsaParams;
            }
        }

        /// <summary>
        /// 比较两个字节数组是否相等
        /// </summary>
        private bool CompareByteArrays(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
                return false;

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 加密数据（RSA+AES混合加密）
        ///
        /// 加密流程：
        /// 1. 生成随机AES密钥（256位）
        /// 2. 用AES加密数据
        /// 3. 用服务器RSA公钥加密AES密钥
        /// 4. 返回：RSA加密的AES密钥 + IV + AES加密的数据
        /// </summary>
        /// <param name="data">要加密的字典数据</param>
        /// <returns>Base64编码的加密数据包</returns>
        public string Encrypt(object data)
        {
            try
            {
                // 1. 将数据转为JSON字符串
                string jsonStr = JsonConvert.SerializeObject(data, Formatting.None);
                byte[] jsonBytes = Encoding.UTF8.GetBytes(jsonStr);

                // 2. 生成随机AES密钥（32字节=256位）
                using (Aes aes = Aes.Create())
                {
                    aes.KeySize = 256;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    aes.GenerateKey();
                    aes.GenerateIV();

                    byte[] aesKey = aes.Key;
                    byte[] iv = aes.IV;

                    // 3. 用AES加密数据
                    byte[] encryptedData;
                    using (ICryptoTransform encryptor = aes.CreateEncryptor())
                    {
                        encryptedData = encryptor.TransformFinalBlock(jsonBytes, 0, jsonBytes.Length);
                    }

                    // 4. 用服务器RSA公钥加密AES密钥
                    byte[] encryptedAesKey = serverRsaProvider.Encrypt(aesKey, true); // 使用OAEP填充

                    // 5. 组合：加密的AES密钥(256字节) + IV(16字节) + 加密的数据
                    using (MemoryStream ms = new MemoryStream())
                    {
                        ms.Write(encryptedAesKey, 0, encryptedAesKey.Length);
                        ms.Write(iv, 0, iv.Length);
                        ms.Write(encryptedData, 0, encryptedData.Length);

                        byte[] combined = ms.ToArray();

                        // 6. Base64编码
                        return Convert.ToBase64String(combined);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"加密失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 生成数据签名（防篡改）
        /// </summary>
        /// <param name="data">要签名的数据</param>
        /// <param name="secret">签名密钥（必须与服务器一致）</param>
        /// <returns>SHA256签名字符串</returns>
        public static string GenerateSignature(object data, string secret = "race_signature_secret")
        {
            try
            {
                // 1. 将对象转换为字典，并按键排序
                var jsonObject = JObject.FromObject(data);
                var sortedDict = new SortedDictionary<string, JToken>(
                    jsonObject.ToObject<Dictionary<string, JToken>>()
                );

                // 2. 使用统一的序列化设置（先生成紧凑格式）
                var settings = new JsonSerializerSettings
                {
                    Formatting = Formatting.None,
                    Culture = System.Globalization.CultureInfo.InvariantCulture
                };

                // 3. 序列化排序后的字典
                string compactJson = JsonConvert.SerializeObject(sortedDict, settings);

                // 4. 转换为标准格式（冒号后加空格，逗号后加空格）
                // 匹配 Python json.dumps() 的默认格式
                string jsonStr = compactJson
                    .Replace("\":", "\": ")   // "key": value
                    .Replace(",\"", ", \"");  // , "key"

                System.Diagnostics.Debug.WriteLine($"[签名] 排序后JSON: {jsonStr}");
                System.Diagnostics.Debug.WriteLine($"[签名] 密钥: {secret}");

                // 5. 拼接密钥并哈希
                string signatureStr = jsonStr + secret;

                using (SHA256 sha256 = SHA256.Create())
                {
                    byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(signatureStr));

                    // 转换为十六进制字符串（小写）
                    StringBuilder sb = new StringBuilder();
                    foreach (byte b in hashBytes)
                    {
                        sb.Append(b.ToString("x2"));  // 小写
                    }

                    string signature = sb.ToString();
                    System.Diagnostics.Debug.WriteLine($"[签名] 生成的签名: {signature}");
                    return signature;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"生成签名失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 解密需要认证的接口返回数据（用客户端私钥解密）
        ///
        /// 服务器返回的加密数据格式：
        /// 1. 服务器用客户端公钥加密AES密钥（客户端用私钥解密）
        /// 2. AES加密实际数据
        ///
        /// 解密流程：
        /// 1. Base64解码得到：加密的AES密钥(256字节) + IV(16字节) + AES加密的数据
        /// 2. 用客户端RSA私钥解密AES密钥
        /// 3. 用AES密钥和IV解密数据
        /// </summary>
        /// <param name="encryptedBase64">Base64编码的加密数据</param>
        /// <returns>解密后的JSON对象</returns>
        public JObject DecryptAuthenticated(string encryptedBase64)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== 开始解密认证接口数据 ===");
                System.Diagnostics.Debug.WriteLine($"Base64密文长度: {encryptedBase64.Length}");

                // 1. Base64解码
                byte[] combined = Convert.FromBase64String(encryptedBase64);
                System.Diagnostics.Debug.WriteLine($"解码后数据总长度: {combined.Length} 字节");

                // 2. 分离组件：加密的AES密钥(256字节) + IV(16字节) + 加密的数据
                int encryptedKeySize = 256; // RSA-2048加密后的大小
                int ivSize = 16;

                if (combined.Length < encryptedKeySize + ivSize)
                {
                    throw new Exception($"加密数据格式错误：数据长度不足 (需要至少{encryptedKeySize + ivSize}字节，实际{combined.Length}字节)");
                }

                byte[] encryptedAesKey = new byte[encryptedKeySize];
                byte[] iv = new byte[ivSize];
                byte[] encryptedData = new byte[combined.Length - encryptedKeySize - ivSize];

                Array.Copy(combined, 0, encryptedAesKey, 0, encryptedKeySize);
                Array.Copy(combined, encryptedKeySize, iv, 0, ivSize);
                Array.Copy(combined, encryptedKeySize + ivSize, encryptedData, 0, encryptedData.Length);

                System.Diagnostics.Debug.WriteLine($"加密的AES密钥长度: {encryptedKeySize} 字节");
                System.Diagnostics.Debug.WriteLine($"IV长度: {ivSize} 字节");
                System.Diagnostics.Debug.WriteLine($"加密数据长度: {encryptedData.Length} 字节");

                // 3. 用客户端RSA私钥解密AES密钥
                byte[] aesKey;
                try
                {
                    System.Diagnostics.Debug.WriteLine("正在使用客户端私钥（OAEP填充）解密AES密钥...");
                    aesKey = clientRsaProvider.Decrypt(encryptedAesKey, true); // 使用OAEP填充
                    System.Diagnostics.Debug.WriteLine($"✓ 解密AES密钥成功，密钥长度: {aesKey.Length} 字节");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"✗ 解密AES密钥失败: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"完整异常: {ex}");
                    throw new Exception($"客户端私钥解密AES密钥失败: {ex.Message}", ex);
                }

                // 4. 用AES密钥和IV解密数据
                using (Aes aes = Aes.Create())
                {
                    aes.KeySize = 256;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    aes.Key = aesKey;
                    aes.IV = iv;

                    byte[] decryptedBytes;
                    using (ICryptoTransform decryptor = aes.CreateDecryptor())
                    {
                        decryptedBytes = decryptor.TransformFinalBlock(encryptedData, 0, encryptedData.Length);
                    }

                    // 5. 转换为JSON对象
                    string jsonStr = Encoding.UTF8.GetString(decryptedBytes);
                    System.Diagnostics.Debug.WriteLine($"✓ AES解密成功，JSON长度: {jsonStr.Length}");
                    System.Diagnostics.Debug.WriteLine("=== 解密完成 ===");
                    return JObject.Parse(jsonStr);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ 解密失败: {ex.Message}");
                throw new Exception($"解密认证接口数据失败: {ex.Message}", ex);
            }
        }
    }
}
