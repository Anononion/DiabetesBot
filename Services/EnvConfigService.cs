using DiabetesBot.Utils.Crypto;

namespace DiabetesBot.Services;

public class EnvConfigService
{
    private static readonly string BaseDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Data");
    private static readonly string EncryptedPath = Path.Combine(BaseDir, ".env.enc");
    private static readonly string PlainPath = Path.Combine(BaseDir, ".env");

    public void LoadAndDecryptEnv()
    {
        // если уже есть зашифрованный — расшифровываем
        if (File.Exists(EncryptedPath))
        {
            string encrypted = File.ReadAllText(EncryptedPath);
            string decrypted = EnvCrypto.Decrypt(encrypted);
            ApplyToEnvironment(decrypted);
            Console.WriteLine("🔐 Environment variables loaded from encrypted .env file");
            return;
        }

        // иначе пробуем зашифровать .env при первом запуске
        if (File.Exists(PlainPath))
        {
            Console.WriteLine("⚙️ No encrypted .env found — encrypting automatically...");
            try
            {
                string plain = File.ReadAllText(PlainPath);
                string encrypted = EnvCrypto.Encrypt(plain);
                File.WriteAllText(EncryptedPath, encrypted);
                Console.WriteLine("✅ .env file encrypted successfully.");
                ApplyToEnvironment(plain);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to encrypt .env automatically: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine("⚠️ No .env or .env.enc found. BOT_TOKEN cannot be loaded.");
        }
    }

    private void ApplyToEnvironment(string content)
    {
        foreach (var line in content.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith('#')) continue;
            var parts = line.Split('=', 2);
            if (parts.Length == 2)
                Environment.SetEnvironmentVariable(parts[0].Trim(), parts[1].Trim());
        }
    }
}
