using System;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace MyFirstCRM
{
    /// <summary>
    /// Класс для управления данными CRM
    /// </summary>
    public static class DataManager
    {
        /// <summary>
        /// Полностью удаляет все данные CRM (кроме пользователей и компаний)
        /// </summary>
        public static void DeleteAllCRMData()
        {
            try
            {
                // Выводим отладочную информацию
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string basePath = Path.Combine(appDataPath, "MyFirstCRM");
                System.Diagnostics.Debug.WriteLine($"Путь к данным: {basePath}");
                
                // 1. Удаляем все данные из баз компаний (кроме пользователей)
                DeleteAllCompanyData();
                
                // 2. Удаляем файлы настроек и конфигураций
                DeleteSettingsFiles();
                
                // 3. Удаляем временные файлы
                DeleteTempFiles();
                
                System.Diagnostics.Debug.WriteLine("Удаление данных CRM завершено");
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при удалении данных CRM: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Очищает все данные в базах компаний (кроме пользователей)
        /// </summary>
        private static void DeleteAllCompanyData()
        {
            try
            {
                // Получаем список всех компаний из глобальной базы
                using (var globalContext = new GlobalDbContext())
                {
                    var companies = globalContext.Companies.ToList();
                    System.Diagnostics.Debug.WriteLine($"Найдено компаний: {companies.Count}");
                    
                    foreach (var company in companies)
                    {
                        try
                        {
                            System.Diagnostics.Debug.WriteLine($"Обработка компании ID: {company.Id}, Name: {company.Name}");
                            
                            // Используем отдельный контекст для удаления данных
                            using (var deletionContext = new CompanyDeletionContext(company.Id))
                            {
                                // Проверяем, что база данных существует и доступна
                                if (deletionContext.Database.CanConnect())
                                {
                                    System.Diagnostics.Debug.WriteLine($"База компании {company.Id} доступна");
                                    
                                    // Прямой список таблиц CRM
                                    var crmTables = new[] { 
                                        "Clients", "Deals", "Expenses", "CalendarEvents", 
                                        "Notifications", "Tasks", "Interactions", 
                                        "DeploymentConfigs", "SyncLogs", "ServerInfos" 
                                    };

                                    System.Diagnostics.Debug.WriteLine($"Попытка удаления таблиц: {string.Join(", ", crmTables)}");

                                    // Удаляем каждую таблицу если она существует
                                    foreach (var table in crmTables)
                                    {
                                        try
                                        {
                                            deletionContext.Database.ExecuteSqlRaw($"DELETE FROM {table}");
                                            System.Diagnostics.Debug.WriteLine($"Удалены {table}");
                                        }
                                        catch (Exception ex)
                                        {
                                            System.Diagnostics.Debug.WriteLine($"Таблица {table} не найдена или ошибка: {ex.Message}");
                                        }
                                    }
                                    
                                    // Принудительно сохраняем изменения
                                    deletionContext.SaveChanges();
                                    System.Diagnostics.Debug.WriteLine($"Изменения сохранены для компании {company.Id}");
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine($"База компании {company.Id} недоступна");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            // Логируем ошибки очистки отдельных баз, но не прерываем процесс
                            System.Diagnostics.Debug.WriteLine($"Ошибка при очистке базы компании {company.Id}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при очистке данных компаний: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Удаляет файлы настроек
        /// </summary>
        private static void DeleteSettingsFiles()
        {
            try
            {
                string appDataDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "MyFirstCRM");

                if (Directory.Exists(appDataDirectory))
                {
                    var settingsFiles = Directory.GetFiles(appDataDirectory, "*.json");
                    foreach (var settingsFile in settingsFiles)
                    {
                        try
                        {
                            File.Delete(settingsFile);
                        }
                        catch
                        {
                            // Игнорируем ошибки удаления отдельных файлов
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при удалении настроек: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Удаляет временные файлы
        /// </summary>
        private static void DeleteTempFiles()
        {
            try
            {
                string tempDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "MyFirstCRM",
                    "Temp");

                if (Directory.Exists(tempDirectory))
                {
                    try
                    {
                        Directory.Delete(tempDirectory, true);
                    }
                    catch
                    {
                        // Если не удается удалить папку, удаляем её содержимое
                        var tempFiles = Directory.GetFiles(tempDirectory, "*", SearchOption.AllDirectories);
                        foreach (var tempFile in tempFiles)
                        {
                            try
                            {
                                File.Delete(tempFile);
                            }
                            catch
                            {
                                // Игнорируем ошибки удаления отдельных файлов
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при удалении временных файлов: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Проверяет, есть ли какие-либо данные в системе
        /// </summary>
        public static bool HasAnyData()
        {
            try
            {
                // Проверяем глобальную базу
                using (var globalContext = new GlobalDbContext())
                {
                    if (globalContext.Companies.Any() || globalContext.Users.Any())
                    {
                        return true;
                    }
                }

                // Проверяем базы компаний
                string dataDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "MyFirstCRM",
                    "Data");

                if (Directory.Exists(dataDirectory))
                {
                    var dbFiles = Directory.GetFiles(dataDirectory, "company_*.db");
                    return dbFiles.Length > 0;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Получает размер всех данных CRM в байтах
        /// </summary>
        public static long GetDataSize()
        {
            try
            {
                string appDataDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "MyFirstCRM");

                if (!Directory.Exists(appDataDirectory))
                {
                    return 0;
                }

                return Directory.GetFiles(appDataDirectory, "*", SearchOption.AllDirectories)
                    .Sum(file => new FileInfo(file).Length);
            }
            catch
            {
                return 0;
            }
        }
    }
}
