using System;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace MyFirstCRM
{
    /// <summary>
    /// Отдельный контекст для удаления данных без рекурсии
    /// </summary>
    public class CompanyDeletionContext : DbContext
    {
        private readonly int _companyId;

        public CompanyDeletionContext(int companyId)
        {
            _companyId = companyId;
        }

        // CRM сущности
        public DbSet<Client> Clients { get; set; }
        public DbSet<Deal> Deals { get; set; }
        public DbSet<Expense> Expenses { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<CRMTask> Tasks { get; set; }
        public DbSet<Interaction> Interactions { get; set; }
        public DbSet<CalendarEvent> CalendarEvents { get; set; }
        public DbSet<DeploymentConfig> DeploymentConfigs { get; set; }
        public DbSet<SyncLog> SyncLogs { get; set; }
        public DbSet<ServerInfo> ServerInfos { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Получаем путь к базе компании напрямую
            string dbPath = GetCompanyDatabasePath(_companyId);
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }

        private string GetCompanyDatabasePath(int companyId)
        {
            try
            {
                // Получаем путь из глобальной БД
                using (var globalContext = new GlobalDbContext())
                {
                    var company = globalContext.Companies.FirstOrDefault(c => c.Id == companyId);
                    if (company != null && !string.IsNullOrEmpty(company.DatabasePath))
                    {
                        return company.DatabasePath;
                    }
                    
                    // Если DatabasePath не указан, но есть CompanyCode, используем его
                    if (company != null && !string.IsNullOrEmpty(company.CompanyCode))
                    {
                        string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                        string basePath = Path.Combine(appDataPath, "MyFirstCRM");
                        return Path.Combine(basePath, "Companies", $"company_{company.CompanyCode}.db");
                    }
                }
            }
            catch
            {
                // Если не удалось получить из БД, используем стандартный путь
            }

            // Стандартный путь если не нашли в БД (fallback)
            string appDataPathFallback = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string basePathFallback = Path.Combine(appDataPathFallback, "MyFirstCRM");
            return Path.Combine(basePathFallback, "Companies", $"company_{companyId}.db");
        }
    }
}
