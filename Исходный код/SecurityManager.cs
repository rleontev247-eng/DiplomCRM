using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;

namespace MyFirstCRM
{
    public static class SecurityManager
    {
        private static SecurityData _currentSecurity = new SecurityData();
        private static readonly string SecurityPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "MyFirstCRM", "security.dat");

        private static readonly string TempDataPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "MyFirstCRM", "temp_encrypted.db");

        public class SecurityData
        {
            public bool IsFirstRun { get; set; } = true;
            public string MasterPasswordHash { get; set; } = "";
            public string Hint { get; set; } = "";
            public DateTime SetupDate { get; set; }

            [JsonIgnore]
            public byte[] EncryptionKey { get; set; } = new byte[32];

            public string EncryptionKeyBase64
            {
                get => Convert.ToBase64String(EncryptionKey);
                set => EncryptionKey = Convert.FromBase64String(value ?? "");
            }

            public int FailedAttempts { get; set; } = 0;
            public DateTime? LastFailedAttempt { get; set; }
        }

        public static SecurityData CurrentSecurity
        {
            get => _currentSecurity;
            set => _currentSecurity = value;
        }

        // Загрузка данных безопасности
        public static void LoadSecurityData()
        {
            try
            {
                if (File.Exists(SecurityPath))
                {
                    var encryptedData = File.ReadAllBytes(SecurityPath);
                    var json = DecryptData(encryptedData, GetMachineKey());

                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        WriteIndented = true
                    };

                    CurrentSecurity = JsonSerializer.Deserialize<SecurityData>(json, options) ?? new SecurityData();
                }
            }
            catch
            {
                CurrentSecurity = new SecurityData();
            }
        }

        // Сохранение данных безопасности
        public static void SaveSecurityData()
        {
            try
            {
                var directory = Path.GetDirectoryName(SecurityPath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    WriteIndented = true
                };

                var json = JsonSerializer.Serialize(CurrentSecurity, options);
                var encryptedData = EncryptData(json, GetMachineKey());
                File.WriteAllBytes(SecurityPath, encryptedData);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения безопасности: {ex.Message}", "Критическая ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Генерация хэша пароля
        public static string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(password + GetMachineId());
                var hash = sha256.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
        }

        // Проверка пароля
        public static bool VerifyPassword(string password)
        {
            if (CurrentSecurity.FailedAttempts >= 5)
            {
                var lockTime = CurrentSecurity.LastFailedAttempt?.AddMinutes(15) ?? DateTime.MinValue;
                if (DateTime.Now < lockTime)
                {
                    throw new Exception($"Система заблокирована на 15 минут. Попробуйте после {lockTime:HH:mm}");
                }
                CurrentSecurity.FailedAttempts = 0;
            }

            var hash = HashPassword(password);
            if (hash == CurrentSecurity.MasterPasswordHash)
            {
                CurrentSecurity.FailedAttempts = 0;
                SaveSecurityData();
                return true;
            }
            else
            {
                CurrentSecurity.FailedAttempts++;
                CurrentSecurity.LastFailedAttempt = DateTime.Now;
                SaveSecurityData();
                return false;
            }
        }

        // Шифрование данных
        private static byte[] EncryptData(string data, byte[] key)
        {
            try
            {
                using (var aes = Aes.Create())
                {
                    aes.Key = key;
                    aes.IV = new byte[16]; // Для простоты

                    using (var encryptor = aes.CreateEncryptor())
                    using (var ms = new MemoryStream())
                    {
                        using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                        using (var writer = new StreamWriter(cs))
                        {
                            writer.Write(data);
                        }
                        return ms.ToArray();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка шифрования: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return new byte[0];
            }
        }

        private static string DecryptData(byte[] data, byte[] key)
        {
            try
            {
                using (var aes = Aes.Create())
                {
                    aes.Key = key;
                    aes.IV = new byte[16];

                    using (var decryptor = aes.CreateDecryptor())
                    using (var ms = new MemoryStream(data))
                    using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                    using (var reader = new StreamReader(cs))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка дешифрования: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return "{}";
            }
        }

        // Получение уникального ключа машины
        private static byte[] GetMachineKey()
        {
            var machineId = GetMachineId();
            using (var sha256 = SHA256.Create())
            {
                return sha256.ComputeHash(Encoding.UTF8.GetBytes(machineId + "MyFirstCRM_Salt_2024"));
            }
        }

        private static string GetMachineId()
        {
            return Environment.MachineName + Environment.UserName;
        }

        // Сброс системы (удаление всех данных)
        public static void EmergencyReset()
        {
            try
            {
                var result = MessageBox.Show(
                    "🚨 ВНИМАНИЕ! ЭКСТРЕННОЕ УДАЛЕНИЕ\n\n" +
                    "Это действие:\n" +
                    "• Удалит ВСЕ данные безвозвратно\n" +
                    "• Удалит всех клиентов и сделки\n" +
                    "• Удалит все настройки\n" +
                    "• Программа закроется\n\n" +
                    "Вы уверены? Это действие нельзя отменить!",
                    "КРИТИЧЕСКОЕ ПОДТВЕРЖДЕНИЕ",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    var appDataPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "MyFirstCRM");

                    if (Directory.Exists(appDataPath))
                    {
                        Directory.Delete(appDataPath, true);
                    }

                    MessageBox.Show("Все данные были удалены в целях безопасности.", "Сброс системы",
                        MessageBoxButton.OK, MessageBoxImage.Information);

                    Application.Current.Shutdown();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сбросе: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Проверка сложности пароля
        public static PasswordStrength CheckPasswordStrength(string password)
        {
            if (string.IsNullOrEmpty(password)) return PasswordStrength.None;

            int score = 0;

            if (password.Length >= 8) score++;
            if (password.Length >= 12) score++;

            if (password.Any(char.IsUpper)) score++;
            if (password.Any(char.IsLower)) score++;
            if (password.Any(char.IsDigit)) score++;
            if (password.Any(c => !char.IsLetterOrDigit(c))) score++;

            if (score <= 2)
                return PasswordStrength.Weak;
            else if (score == 3 || score == 4)
                return PasswordStrength.Medium;
            else if (score == 5)
                return PasswordStrength.Strong;
            else // score >= 6
                return PasswordStrength.VeryStrong;
        }

        public enum PasswordStrength
        {
            None,
            Weak,
            Medium,
            Strong,
            VeryStrong
        }
    }
}