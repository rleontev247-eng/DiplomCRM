using System;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace MyFirstCRM
{
    public class AppSettings
    {
        public string YandexDiskToken { get; set; } = "";
        public string EmployeeName { get; set; } = "";
        public string Theme { get; set; } = "Dark";
        public bool AutoBackup { get; set; } = true;
        public int BackupDays { get; set; } = 7;
        public bool ShowNotifications { get; set; } = true;
        public bool SoundNotifications { get; set; } = true;
        public string Language { get; set; } = "ru";
        // Новые свойства
        public string AccentColor { get; set; } = "Blue";
        public bool AutoColorBySeason { get; set; } = false;
        public bool ShowPerformanceTips { get; set; } = true;
        public bool AutoOptimizeDatabase { get; set; } = false;
        public int DataRetentionMonths { get; set; } = 12;
        public bool EnableDataExportLog { get; set; } = true;

        // Отчеты / Дашборд
        public bool ShowFinanceOnDashboard { get; set; } = true;
        public bool ShowFinanceReports { get; set; } = true;

        // Свойства уведомлений
        public bool NotifyUpcomingDeals { get; set; } = true;
        public int NotifyDaysBefore { get; set; } = 3;
        public bool NotifyLargeDeals { get; set; } = true;
        public decimal LargeDealThreshold { get; set; } = 100000;

        // Email настройки
        public string SmtpHost { get; set; } = "smtp.gmail.com";
        public int SmtpPort { get; set; } = 587;
        public string SmtpUser { get; set; } = string.Empty;
        public string SmtpPassword { get; set; } = string.Empty;
        public bool UseSsl { get; set; } = true;

        // SMS настройки
        public string SmsApiKey { get; set; } = string.Empty;
        public string SmsSenderName { get; set; } = "CRM";
        public string SmsProvider { get; set; } = "SMSRU";
        public bool PreferEmailForNotifications { get; set; } = true;
        public bool SendBothChannels { get; set; } = false;

        // Настройки развертывания и синхронизации
        public DeploymentMode DeploymentMode { get; set; } = DeploymentMode.Local;
        public string? CloudServerUrl { get; set; }
        public string? CloudApiKey { get; set; }
        public string? ServerIpAddress { get; set; }
        public int ServerPort { get; set; } = 8080;
        public bool AutoSyncEnabled { get; set; } = true;
        public int SyncIntervalMinutes { get; set; } = 30;
        public bool UseCompression { get; set; } = true;
        public bool UseEncryption { get; set; } = true;
        public bool OneWaySyncOnly { get; set; } = false;

        private static readonly string SettingsPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "MyFirstCRM", "settings.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch { }

            return new AppSettings();
        }
        public void ApplyTheme()
        {
            ColorThemeManager.ApplyTheme(AccentColor);
        }

        public void Save()
        {
            try
            {
                var directory = Path.GetDirectoryName(SettingsPath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения настроек: {ex.Message}", "Ошибка");
            }
        }
    }
}