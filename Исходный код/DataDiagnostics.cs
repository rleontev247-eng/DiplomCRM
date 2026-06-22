using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace MyFirstCRM
{
    /// <summary>
    /// Утилита для проверки и диагностики данных
    /// </summary>
    public static class DataDiagnostics
    {
        /// <summary>
        /// Получить информацию о состоянии системы
        /// </summary>
        public static async Task<string> GetSystemInfo()
        {
            try
            {
                var info = new System.Text.StringBuilder();
                info.AppendLine("=== ДИАГНОСТИКА СИСТЕМЫ MyFirstCRM ===");
                info.AppendLine($"Время проверки: {DateTime.Now}");
                info.AppendLine();

                // Проверка глобальной базы
                try
                {
                    using var globalContext = new GlobalDbContext();
                    int companyCount = await globalContext.Companies.CountAsync();
                    int userCount = await globalContext.Users.CountAsync();
                    
                    info.AppendLine("ГЛОБАЛЬНАЯ БАЗА ДАННЫХ:");
                    info.AppendLine($"  • Компаний: {companyCount}");
                    info.AppendLine($"  • Пользователей: {userCount}");
                    
                    if (companyCount > 0)
                    {
                        var companies = await globalContext.Companies.ToListAsync();
                        info.AppendLine("  • Список компаний:");
                        foreach (var company in companies)
                        {
                            info.AppendLine($"    - {company.Name} (Код: {company.CompanyCode})");
                        }
                    }
                }
                catch (Exception ex)
                {
                    info.AppendLine($"ОШИБКА глобальной базы: {ex.Message}");
                }
                
                info.AppendLine();

                // Проверка баз компаний
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string companiesDir = Path.Combine(appDataPath, "MyFirstCRM", "Companies");
                
                if (Directory.Exists(companiesDir))
                {
                    var companyDbFiles = Directory.GetFiles(companiesDir, "company_*.db");
                    info.AppendLine("БАЗЫ КОМПАНИЙ:");
                    info.AppendLine($"  • Файлов баз: {companyDbFiles.Length}");
                    
                    foreach (var dbFile in companyDbFiles)
                    {
                        var fileInfo = new FileInfo(dbFile);
                        string fileName = Path.GetFileNameWithoutExtension(dbFile);
                        string companyId = fileName.Replace("company_", "");
                        
                        info.AppendLine($"    - {fileName} ({fileInfo.Length / 1024} КБ)");
                        
                        // Проверяем данные в каждой базе
                        try
                        {
                            using var context = new AppDbContext();
                            context.CurrentCompanyId = int.Parse(companyId);
                            
                            int clientCount = await context.Clients.CountAsync();
                            int dealCount = await context.Deals.CountAsync();
                            int expenseCount = await context.Expenses.CountAsync();
                            
                            info.AppendLine($"      Клиентов: {clientCount}, Сделок: {dealCount}, Расходов: {expenseCount}");
                        }
                        catch (Exception ex)
                        {
                            info.AppendLine($"      Ошибка проверки: {ex.Message}");
                        }
                    }
                }
                else
                {
                    info.AppendLine("БАЗЫ КОМПАНИЙ: директория не найдена");
                }
                
                info.AppendLine();

                // Проверка файлов настроек
                string settingsPath = Path.Combine(appDataPath, "MyFirstCRM", "settings.json");
                info.AppendLine($"ФАЙЛ НАСТРОЕК: {(File.Exists(settingsPath) ? "Существует" : "Отсутствует")}");
                
                return info.ToString();
            }
            catch (Exception ex)
            {
                return $"Ошибка диагностики: {ex.Message}";
            }
        }
        
        /// <summary>
        /// Проверить возможность входа для компании
        /// </summary>
        public static async Task<bool> CanLoginToCompany(string companyCode, string username, string password)
        {
            try
            {
                using var globalContext = new GlobalDbContext();
                
                // Находим компанию
                var company = await globalContext.Companies
                    .FirstOrDefaultAsync(c => c.CompanyCode == companyCode && c.IsActive);
                    
                if (company == null)
                {
                    return false;
                }
                
                // Находим пользователя
                var user = await globalContext.Users
                    .FirstOrDefaultAsync(u => u.CompanyId == company.Id && 
                                           u.Username == username && 
                                           u.IsActive);
                    
                if (user == null)
                {
                    return false;
                }
                
                // Проверяем пароль
                string hashedPassword = MultiUserSecurityManager.HashPasswordWithSalt(password, user.PasswordSalt);
                return hashedPassword == user.PasswordHash;
            }
            catch
            {
                return false;
            }
        }
    }
}
