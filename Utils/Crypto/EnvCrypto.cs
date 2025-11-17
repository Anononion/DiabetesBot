using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace DiabetesBot.Utils.Crypto;

public static class EnvCrypto
{
    // ⚠️ пока статический ключ — потом можно хранить его в отдельном зашифрованном файле или DPAPI
    private static readonly byte[] Key = Encoding.UTF8.GetBytes("16BYTELONGSECRET"); // 16 символов = 128 бит
    private static readonly byte[] IV = Encoding.UTF8.GetBytes("16BYTEINITVECTOR"); // тоже 16 символов

    public static string Encrypt(string plain)
    {
        using var aes = Aes.Create();
        aes.Key = Key;
        aes.IV = IV;
        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream();
        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
        using (var sw = new StreamWriter(cs))
            sw.Write(plain);
        return Convert.ToBase64String(ms.ToArray());
    }

    public static string Decrypt(string cipher)
    {
        using var aes = Aes.Create();
        aes.Key = Key;
        aes.IV = IV;
        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        using var ms = new MemoryStream(Convert.FromBase64String(cipher));
        using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
        using var sr = new StreamReader(cs);
        return sr.ReadToEnd();
    }
}
