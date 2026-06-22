using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.EntityFrameworkCore;

namespace MyFirstCRM
{
    /// <summary>
    /// Менеджер резервного копирования CRM
    /// </summary>
    public class BackupManager
    {
        private static BackupManager? _instance;
        private static readonly object _lock = new object();
        private Timer? _backupTimer;
        private readonly AppSettings _settings;

        public static BackupManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                            _instance = new BackupManager();
                    }
                }
                return _instance;
            }
        }

        private BackupManager()
        {
            _settings = AppSettings.Load();
            StartAutoBackup();
        }

        /// <summary>
        /// Создать полную резервную копию
        /// </summary>
        public async Task<bool> CreateFullBackup(string? backupPath = null)
        {
            try
            {
                string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyFirstCRM");
                
                if (string.IsNullOrEmpty(backupPath))
                {
                    backupPath = Path.Combine(appDataPath, "Backups", $"CRM_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.zip");
                }

                // Создаем директорию для бэкапов если не существует
                string backupDir = Path.GetDirectoryName(backupPath);
                if (!Directory.Exists(backupDir))
                {
                    Directory.CreateDirectory(backupDir);
                }

                // Закрываем все соединения с базами данных перед бэкапом
                await CloseAllDatabaseConnections();
                
                // Небольшая пауза для освобождения файлов
                await Task.Delay(1000);

                // Проверяем доступность файлов перед созданием архива
                string globalDbPath = Path.Combine(appDataPath, "global.db");
                if (File.Exists(globalDbPath))
                {
                    try
                    {
                        // Проверяем, что файл не заблокирован
                        using (var testStream = File.Open(globalDbPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            // Файл доступен для чтения
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Global database is still locked: {ex.Message}");
                        // Дополнительная попытка закрыть соединения
                        await Task.Delay(2000);
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                    }
                }

                using (var archive = ZipFile.Open(backupPath, ZipArchiveMode.Create))
                {
                    // Добавляем глобальную базу данных
                    if (File.Exists(globalDbPath))
                    {
                        try
                        {
                            archive.CreateEntryFromFile(globalDbPath, "global.db");
                            System.Diagnostics.Debug.WriteLine("Successfully backed up global.db");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to backup global.db: {ex.Message}");
                            // Продолжаем бэкап других файлов
                        }
                    }

                    // Добавляем все базы данных компаний
                    string companiesDir = Path.Combine(appDataPath, "Companies");
                    if (Directory.Exists(companiesDir))
                    {
                        foreach (string companyDb in Directory.GetFiles(companiesDir, "company_*.db"))
                        {
                            string fileName = Path.GetFileName(companyDb);
                            try
                            {
                                // Проверяем доступность файла
                                using (var testStream = File.Open(companyDb, FileMode.Open, FileAccess.Read, FileShare.Read))
                                {
                                    // Файл доступен
                                }
                                
                                archive.CreateEntryFromFile(companyDb, $"Companies/{fileName}");
                                System.Diagnostics.Debug.WriteLine($"Successfully backed up {fileName}");
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Failed to backup {fileName}: {ex.Message}");
                                // Продолжаем бэкап других файлов
                            }
                        }
                    }

                    // Добавляем настройки
                    string settingsPath = Path.Combine(appDataPath, "settings.json");
                    if (File.Exists(settingsPath))
                    {
                        try
                        {
                            archive.CreateEntryFromFile(settingsPath, "settings.json");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to backup settings.json: {ex.Message}");
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Backup error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Восстановить из резервной копии
        /// </summary>
        public async Task<bool> RestoreFromBackup(string backupPath)
        {
            try
            {
                string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyFirstCRM");

                // Закрываем все соединения с базами данных перед восстановлением
                await CloseAllDatabaseConnections();
                
                // Небольшая пауза для освобождения файлов
                await Task.Delay(500);

                using (var archive = ZipFile.OpenRead(backupPath))
                {
                    foreach (var entry in archive.Entries)
                    {
                        string destinationPath = Path.Combine(appDataPath, entry.FullName);
                        
                        // Создаем директорию если нужно
                        string destinationDir = Path.GetDirectoryName(destinationPath);
                        if (!Directory.Exists(destinationDir))
                        {
                            Directory.CreateDirectory(destinationDir);
                        }

                        try
                        {
                            // Перезаписываем файл
                            entry.ExtractToFile(destinationPath, overwrite: true);
                            System.Diagnostics.Debug.WriteLine($"Restored: {entry.FullName}");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to restore {entry.FullName}: {ex.Message}");
                            // Продолжаем восстановление других файлов
                        }
                    }
                }

                // Проверяем целостность восстановленных данных
                bool integrityCheck = await VerifyDataIntegrity();
                if (!integrityCheck)
                {
                    System.Diagnostics.Debug.WriteLine("Warning: Data integrity check failed after restore");
                }

                // Создаем файл настроек если он отсутствует
                await CreateDefaultSettingsIfNeeded();

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Restore error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Создать файл настроек по умолчанию если он отсутствует
        /// </summary>
        private async Task CreateDefaultSettingsIfNeeded()
        {
            try
            {
                string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyFirstCRM");
                string settingsPath = Path.Combine(appDataPath, "settings.json");
                
                if (!File.Exists(settingsPath))
                {
                    var defaultSettings = new AppSettings();
                    string json = System.Text.Json.JsonSerializer.Serialize(defaultSettings, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(settingsPath, json);
                    System.Diagnostics.Debug.WriteLine("Created default settings.json file");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create default settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Запустить автоматическое резервное копирование
        /// </summary>
        private void StartAutoBackup()
        {
            StopAutoBackup();

            if (_settings.AutoBackup)
            {
                // Запускаем бэкап каждый день в 3 часа ночи
                DateTime now = DateTime.Now;
                DateTime scheduledTime = new DateTime(now.Year, now.Month, now.Day, 3, 0, 0);
                
                if (scheduledTime <= now)
                {
                    scheduledTime = scheduledTime.AddDays(1);
                }

                TimeSpan initialDelay = scheduledTime - now;
                
                _backupTimer = new Timer(async _ => await PerformAutoBackup(), 
                    null, initialDelay, TimeSpan.FromDays(1));
            }
        }

        /// <summary>
        /// Остановить автоматическое резервное копирование
        /// </summary>
        private void StopAutoBackup()
        {
            _backupTimer?.Dispose();
            _backupTimer = null;
        }

        /// <summary>
        /// Выполнить автоматическое резервное копирование
        /// </summary>
        private async Task PerformAutoBackup()
        {
            try
            {
                bool success = await CreateFullBackup();
                if (success)
                {
                    System.Diagnostics.Debug.WriteLine($"Auto backup created at {DateTime.Now}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Auto backup failed at {DateTime.Now}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Auto backup error: {ex.Message}");
            }
        }

        /// <summary>
        /// Очистить старые бэкапы
        /// </summary>
        private void CleanupOldBackups()
        {
            try
            {
                string backupDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                                               "MyFirstCRM", "Backups");
                
                if (!Directory.Exists(backupDir)) return;

                var backupFiles = Directory.GetFiles(backupDir, "CRM_Backup_*.zip")
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.CreationTime)
                    .ToList();

                DateTime cutoffDate = DateTime.Now.AddDays(-_settings.BackupDays);

                foreach (var file in backupFiles.Where(f => f.CreationTime < cutoffDate))
                {
                    try
                    {
                        file.Delete();
                        System.Diagnostics.Debug.WriteLine($"Deleted old backup: {file.Name}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to delete backup {file.Name}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Cleanup error: {ex.Message}");
            }
        }

        /// <summary>
        /// Закрыть все соединения с базами данных
        /// </summary>
        private async Task CloseAllDatabaseConnections()
        {
            try
            {
                // Очищаем текущего пользователя и компанию ПЕРВЫМ
                MultiUserSecurityManager.Logout();
                
                // Закрываем текущий контекст главного окна если он есть
                if (Application.Current is System.Windows.Application app)
                {
                    var mainWindow = app.MainWindow as MainWindow;
                    if (mainWindow != null)
                    {
                        // Получаем доступ к приватному полю _context через рефлексию
                        var contextField = typeof(MainWindow).GetField("_context", 
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (contextField != null)
                        {
                            var context = contextField.GetValue(mainWindow) as AppDbContext;
                            if (context != null)
                            {
                                try
                                {
                                    context.Database.CloseConnection();
                                    context.Dispose();
                                }
                                catch { }
                                contextField.SetValue(mainWindow, null);
                            }
                        }
                        
                        // Также ищем другие возможные контексты
                        var fields = typeof(MainWindow).GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        foreach (var field in fields)
                        {
                            if (field.FieldType == typeof(AppDbContext) || field.FieldType == typeof(GlobalDbContext))
                            {
                                var context = field.GetValue(mainWindow);
                                if (context != null)
                                {
                                    try
                                    {
                                        var dbContext = context as IDisposable;
                                        dbContext?.Dispose();
                                    }
                                    catch { }
                                    field.SetValue(mainWindow, null);
                                }
                            }
                        }
                    }
                }
                
                // Принудительно закрываем все SQLite соединения
                try
                {
                    // Очищаем пулы соединений SQLite с правильными параметрами
                    var clearPoolMethod = typeof(Microsoft.Data.Sqlite.SqliteConnection).GetMethod("ClearPool", new[] { typeof(Microsoft.Data.Sqlite.SqliteConnection) });
                    var clearAllPoolsMethod = typeof(Microsoft.Data.Sqlite.SqliteConnection).GetMethod("ClearAllPools", System.Type.EmptyTypes);
                    
                    if (clearAllPoolsMethod != null)
                    {
                        clearAllPoolsMethod.Invoke(null, null);
                    }
                }
                catch { }
                
                // Принудительно собираем мусор для закрытия соединений
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                
                // Дополнительная пауза для освобождения файлов
                await Task.Delay(1000);
                
                // Удаляем старый файл database.db если он существует
                await CleanupOldDatabaseFiles();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error closing database connections: {ex.Message}");
            }
        }

        /// <summary>
        /// Очистить старые файлы базы данных
        /// </summary>
        private async Task CleanupOldDatabaseFiles()
        {
            try
            {
                string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyFirstCRM");
                string oldDbPath = Path.Combine(appDataPath, "database.db");
                
                if (File.Exists(oldDbPath))
                {
                    try
                    {
                        File.Delete(oldDbPath);
                        System.Diagnostics.Debug.WriteLine("Deleted old database.db file");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to delete old database.db: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in cleanup: {ex.Message}");
            }
        }

        /// <summary>
        /// Перезапустить автоматический бэкап с новыми настройками
        /// </summary>
        public void RestartAutoBackup()
        {
            _settings.AutoBackup = AppSettings.Load().AutoBackup;
            _settings.BackupDays = AppSettings.Load().BackupDays;
            StartAutoBackup();
        }

        /// <summary>
        /// Получить список бэкапов
        /// </summary>
        public string[] GetBackupList()
        {
            try
            {
                string backupDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                                               "MyFirstCRM", "Backups");
                
                if (!Directory.Exists(backupDir)) return new string[0];

                return Directory.GetFiles(backupDir, "CRM_Backup_*.zip")
                    .OrderByDescending(f => f)
                    .ToArray();
            }
            catch
            {
                return new string[0];
            }
        }

        /// <summary>
        /// Проверить целостность данных после восстановления
        /// </summary>
        private async Task<bool> VerifyDataIntegrity()
        {
            try
            {
                string appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyFirstCRM");
                
                // Проверяем существование ключевых файлов
                string globalDbPath = Path.Combine(appDataPath, "global.db");
                string companiesDir = Path.Combine(appDataPath, "Companies");
                string settingsPath = Path.Combine(appDataPath, "settings.json");
                
                bool hasGlobalDb = File.Exists(globalDbPath);
                bool hasCompaniesDir = Directory.Exists(companiesDir);
                bool hasSettings = File.Exists(settingsPath);
                
                System.Diagnostics.Debug.WriteLine($"Integrity check - Global DB: {hasGlobalDb}, Companies: {hasCompaniesDir}, Settings: {hasSettings}");
                
                if (!hasGlobalDb || !hasCompaniesDir)
                {
                    return false;
                }
                
                // Проверяем, что в глобальной базе есть компании
                try
                {
                    using var globalContext = new GlobalDbContext();
                    int companyCount = await globalContext.Companies.CountAsync();
                    System.Diagnostics.Debug.WriteLine($"Companies in global DB: {companyCount}");
                    
                    if (companyCount == 0)
                    {
                        System.Diagnostics.Debug.WriteLine("Warning: No companies found in global database after restore");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error checking global database: {ex.Message}");
                    return false;
                }
                
                // Проверяем, что есть файлы баз компаний
                var companyDbFiles = Directory.GetFiles(companiesDir, "company_*.db");
                System.Diagnostics.Debug.WriteLine($"Company database files: {companyDbFiles.Length}");
                
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Data integrity check error: {ex.Message}");
                return false;
            }
        }

        public void Dispose()
        {
            StopAutoBackup();
        }
    }
}
