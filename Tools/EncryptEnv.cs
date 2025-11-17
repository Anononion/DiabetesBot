using System;
using System.IO;
using DiabetesBot.Utils.Crypto;

namespace DiabetesBot.Tools
{
    public static class EncryptEnv
    {
        private const string PlainPath = "Data/.env";
        private const string EncryptedPath = "Data/.env.enc";

        public static void Run()
        {
            Console.WriteLine("🔐 Starting .env encryption...");

            if (!File.Exists(PlainPath))
            {
                Console.WriteLine("⚠️ No .env file found in Data/");
                return;
            }

            string plain = File.ReadAllText(PlainPath);
            string encrypted = EnvCrypto.Encrypt(plain);

            Directory.CreateDirectory(Path.GetDirectoryName(EncryptedPath)!);
            File.WriteAllText(EncryptedPath, encrypted);

            Console.WriteLine("✅ .env encrypted and saved to Data/.env.enc");
        }
    }
}
