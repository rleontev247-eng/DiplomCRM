using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Linq;

namespace MyFirstCRM
{
    /// <summary>
    /// Контекст базы данных с поддержкой многокомпанийности
    /// </summary>
    public class AppDbContext : DbContext
    {
        // === Компании и пользователи ===
        public DbSet<Company> Companies { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<UserSession> UserSessions { get; set; }

        // === Основные сущности CRM ===
        public DbSet<Client> Clients { get; set; }
        public DbSet<Deal> Deals { get; set; }
        public DbSet<Expense> Expenses { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<CRMTask> Tasks { get; set; }
        public DbSet<Interaction> Interactions { get; set; }
        public DbSet<CalendarEvent> CalendarEvents { get; set; }

        // === Сообщения и уведомления ===
        public DbSet<EmailMessage> EmailMessages { get; set; }
        public DbSet<SmsMessage> SmsMessages { get; set; }

        // === Сущности развертывания и синхронизации ===
        public DbSet<DeploymentConfig> DeploymentConfigs { get; set; }
        public DbSet<SyncLog> SyncLogs { get; set; }
        public DbSet<ServerInfo> ServerInfos { get; set; }

        /// <summary>
        /// Текущий ID компании для фильтрации данных
        /// </summary>
        public int? CurrentCompanyId { get; set; }

        /// <summary>
        /// Текущий пользователь
        /// </summary>
        public User? CurrentUser { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            try
            {
                // Если не указан путь к БД компании, используем глобальную БД (для списка компаний)
                string dbPath = GetDatabasePath();
                System.Diagnostics.Debug.WriteLine($"Database path: {dbPath}");

                // Создаем папку, если её нет
                string directory = Path.GetDirectoryName(dbPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    System.Diagnostics.Debug.WriteLine($"Created directory: {directory}");
                }

                optionsBuilder.UseSqlite($"Data Source={dbPath}");

                // Проверяем права доступа к папке
                if (!string.IsNullOrEmpty(directory))
                {
                    try
                    {
                        var testFile = Path.Combine(directory, "test_write.tmp");
                        File.WriteAllText(testFile, "test");
                        File.Delete(testFile);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Нет прав доступа к папке {directory}: {ex.Message}");
                    }
                }

                optionsBuilder.UseSqlite($"Data Source={dbPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Database configuration error: {ex.Message}");
                throw new Exception($"Ошибка конфигурации базы данных: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Получает путь к базе данных
        /// Если указан CurrentCompanyId - используется БД этой компании
        /// Иначе - глобальная БД (только для Companies, Users)
        /// </summary>
        private string GetDatabasePath()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string basePath = Path.Combine(appDataPath, "MyFirstCRM");

            // Если есть активная компания - используем её базу данных
            if (CurrentCompanyId.HasValue && CurrentCompanyId.Value > 0)
            {
                // Сначала проверяем, есть ли уже загруженная компания
                var company = GetCompanyFromGlobalDb(CurrentCompanyId.Value);
                if (company != null && !string.IsNullOrEmpty(company.DatabasePath))
                {
                    return company.DatabasePath;
                }

                // Иначе создаем путь по умолчанию (используем CompanyCode для соответствия с реальными файлами)
                if (company != null && !string.IsNullOrEmpty(company.CompanyCode))
                {
                    return Path.Combine(basePath, "Companies", $"company_{company.CompanyCode}.db");
                }

                // Fallback: если по какой-то причине нет CompanyCode, используем ID
                return Path.Combine(basePath, "Companies", $"company_{CurrentCompanyId.Value}.db");
            }

            // Глобальная база для компаний и пользователей
            return Path.Combine(basePath, "global.db");
        }

        /// <summary>
        /// Проверяет и добавляет недостающие колонки в базу данных
        /// </summary>
        public void EnsureMissingColumns()
        {
            try
            {
                Database.OpenConnection();
                
                // Проверяем существование таблиц и создаем их если нужно
                EnsureTableExists("Clients");
                EnsureTableExists("Deals");
                EnsureTableExists("Expenses");
                EnsureTableExists("Notifications");
                EnsureTableExists("CalendarEvents");
                EnsureTableExists("Tasks");
                
                // Проверяем колонку UpdatedAt в таблице Deals
                using var command1 = Database.GetDbConnection().CreateCommand();
                command1.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Deals') WHERE name = 'UpdatedAt'";
                var dealsColumns = command1.ExecuteScalar();
                if (Convert.ToInt32(dealsColumns) == 0)
                {
                    command1.CommandText = "ALTER TABLE Deals ADD COLUMN UpdatedAt TEXT NOT NULL DEFAULT '2024-01-01 00:00:00'";
                    command1.ExecuteNonQuery();
                    System.Diagnostics.Debug.WriteLine("Added UpdatedAt column to Deals table");
                }

                // Проверяем колонку ClientId в таблице Expenses
                using var command2 = Database.GetDbConnection().CreateCommand();
                command2.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Expenses') WHERE name = 'ClientId'";
                var expensesClientId = command2.ExecuteScalar();
                if (Convert.ToInt32(expensesClientId) == 0)
                {
                    command2.CommandText = "ALTER TABLE Expenses ADD COLUMN ClientId INTEGER";
                    command2.ExecuteNonQuery();
                    System.Diagnostics.Debug.WriteLine("Added ClientId column to Expenses table");
                }

                // Проверяем колонку CreatedAt в таблице Expenses
                using var command3 = Database.GetDbConnection().CreateCommand();
                command3.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Expenses') WHERE name = 'CreatedAt'";
                var expensesCreatedAt = command3.ExecuteScalar();
                if (Convert.ToInt32(expensesCreatedAt) == 0)
                {
                    command3.CommandText = "ALTER TABLE Expenses ADD COLUMN CreatedAt TEXT NOT NULL DEFAULT '2024-01-01 00:00:00'";
                    command3.ExecuteNonQuery();
                    System.Diagnostics.Debug.WriteLine("Added CreatedAt column to Expenses table");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error ensuring missing columns: {ex.Message}");
            }
            finally
            {
                Database.CloseConnection();
            }
        }

        /// <summary>
        /// Проверяет существование таблицы и создает ее если нужно
        /// </summary>
        private void EnsureTableExists(string tableName)
        {
            try
            {
                using var command = Database.GetDbConnection().CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@tableName";
                var parameter = command.CreateParameter();
                parameter.ParameterName = "@tableName";
                parameter.Value = tableName;
                command.Parameters.Add(parameter);
                
                var tableExists = Convert.ToInt32(command.ExecuteScalar());
                if (tableExists == 0)
                {
                    // Таблица не существует, создаем ее через SQL
                    System.Diagnostics.Debug.WriteLine($"Table {tableName} does not exist, creating...");
                    
                    switch (tableName)
                    {
                        case "Clients":
                            // Сначала проверяем существование таблицы
                            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Clients'";
                            var clientsTableExists = Convert.ToInt32(command.ExecuteScalar()) > 0;
                            
                            if (!clientsTableExists)
                            {
                                // Создаем таблицу если она не существует
                                command.CommandText = @"
                                    CREATE TABLE Clients (
                                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                        Name TEXT NOT NULL,
                                        Phone TEXT,
                                        Email TEXT,
                                        Notes TEXT,
                                        CompanyId INTEGER NOT NULL,
                                        CreatedAt TEXT NOT NULL DEFAULT '2024-01-01 00:00:00',
                                        ABC_Category TEXT,
                                        CreatedByUserId INTEGER,
                                        UpdatedByUserId INTEGER
                                    )";
                                command.ExecuteNonQuery();
                                System.Diagnostics.Debug.WriteLine("Created table Clients");
                            }
                            else
                            {
                                // Проверяем наличие колонки ABC_Category
                                command.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Clients') WHERE name = 'ABC_Category'";
                                var columnExists = Convert.ToInt32(command.ExecuteScalar()) > 0;
                                
                                if (!columnExists)
                                {
                                    command.CommandText = "ALTER TABLE Clients ADD COLUMN ABC_Category TEXT";
                                    command.ExecuteNonQuery();
                                    System.Diagnostics.Debug.WriteLine("Added ABC_Category column to Clients table");
                                }
                            }
                            return;
                        case "Deals":
                            command.CommandText = @"
                                CREATE TABLE IF NOT EXISTS Deals (
                                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                    Title TEXT NOT NULL,
                                    Description TEXT,
                                    Amount REAL NOT NULL,
                                    Status TEXT NOT NULL DEFAULT 'New',
                                    CreatedAt TEXT NOT NULL DEFAULT '2024-01-01 00:00:00',
                                    UpdatedAt TEXT NOT NULL DEFAULT '2024-01-01 00:00:00',
                                    ClosedAt TEXT,
                                    Deadline TEXT NOT NULL DEFAULT '2024-01-01 00:00:00',
                                    ClientId INTEGER NOT NULL,
                                    CompanyId INTEGER NOT NULL,
                                    CreatedByUserId INTEGER,
                                    AssignedToUserId INTEGER
                                )";
                            break;
                        case "Expenses":
                            command.CommandText = @"
                                CREATE TABLE IF NOT EXISTS Expenses (
                                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                    Title TEXT NOT NULL,
                                    Category TEXT NOT NULL DEFAULT 'Прочее',
                                    Amount REAL NOT NULL,
                                    Date TEXT NOT NULL,
                                    Notes TEXT,
                                    CompanyId INTEGER NOT NULL,
                                    CreatedByUserId INTEGER,
                                    ClientId INTEGER,
                                    CreatedAt TEXT NOT NULL DEFAULT '2024-01-01 00:00:00'
                                )";
                            break;
                        case "Notifications":
                            command.CommandText = @"
                                CREATE TABLE IF NOT EXISTS Notifications (
                                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                    Title TEXT NOT NULL,
                                    Message TEXT,
                                    Type TEXT NOT NULL DEFAULT 'info',
                                    IsRead INTEGER NOT NULL DEFAULT 0,
                                    CreatedAt TEXT NOT NULL DEFAULT '2024-01-01 00:00:00',
                                    CompanyId INTEGER NOT NULL,
                                    CreatedByUserId INTEGER,
                                    ClientId INTEGER
                                )";
                            break;
                        case "CalendarEvents":
                            command.CommandText = @"
                                CREATE TABLE IF NOT EXISTS CalendarEvents (
                                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                    Title TEXT NOT NULL,
                                    Description TEXT,
                                    StartTime TEXT NOT NULL,
                                    EndTime TEXT,
                                    Location TEXT,
                                    EventType TEXT NOT NULL DEFAULT 'meeting',
                                    CreatedAt TEXT NOT NULL DEFAULT '2024-01-01 00:00:00',
                                    UpdatedAt TEXT NOT NULL DEFAULT '2024-01-01 00:00:00',
                                    ClientId INTEGER,
                                    CompanyId INTEGER NOT NULL,
                                    AssignedToUserId INTEGER
                                )";
                            break;
                        case "Tasks":
                            command.CommandText = @"
                                CREATE TABLE IF NOT EXISTS Tasks (
                                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                    Title TEXT NOT NULL,
                                    Description TEXT,
                                    Status TEXT NOT NULL DEFAULT 'new',
                                    Priority TEXT NOT NULL DEFAULT 'Medium',
                                    DueDate TEXT,
                                    CreatedAt TEXT NOT NULL DEFAULT '2024-01-01 00:00:00',
                                    UpdatedAt TEXT NOT NULL DEFAULT '2024-01-01 00:00:00',
                                    ClientId INTEGER,
                                    CompanyId INTEGER NOT NULL,
                                    CreatedByUserId INTEGER,
                                    AssignedToUserId INTEGER
                                )";
                            break;
                        default:
                            System.Diagnostics.Debug.WriteLine($"Unknown table type: {tableName}");
                            return;
                    }
                    
                    command.Parameters.Clear();
                    command.ExecuteNonQuery();
                    System.Diagnostics.Debug.WriteLine($"Created table {tableName}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error ensuring table {tableName} exists: {ex.Message}");
            }
        }

        /// <summary>
        /// Получает компанию из глобальной БД
        /// </summary>
        private Company? GetCompanyFromGlobalDb(int companyId)
        {
            try
            {
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string globalDbPath = Path.Combine(appDataPath, "MyFirstCRM", "global.db");

                if (!File.Exists(globalDbPath))
                    return null;

                using var globalContext = new GlobalDbContext();
                return globalContext.Companies.FirstOrDefault(c => c.Id == companyId);
            }
            catch
            {
                return null;
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // === Конфигурация Company ===
            modelBuilder.Entity<Company>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.CompanyCode).IsRequired().HasMaxLength(50);
                entity.HasIndex(e => e.CompanyCode).IsUnique();
                entity.Property(e => e.DatabasePath).IsRequired().HasMaxLength(500);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("datetime('now')");
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.MaxUsers).HasDefaultValue(10);
            });

            // === Конфигурация User ===
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Username).IsRequired().HasMaxLength(100);
                entity.Property(e => e.FullName).IsRequired().HasMaxLength(200);
                entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(100);
                entity.Property(e => e.PasswordSalt).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Role).HasConversion<string>().HasMaxLength(20);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("datetime('now')");
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.HasIndex(e => new { e.CompanyId, e.Username }).IsUnique();

                // Связь с компанией
                entity.HasOne(e => e.Company)
                      .WithMany(c => c.Users)
                      .HasForeignKey(e => e.CompanyId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // === Конфигурация UserSession ===
            modelBuilder.Entity<UserSession>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.SessionToken).IsRequired().HasMaxLength(100);
                entity.Property(e => e.StartedAt).HasDefaultValueSql("datetime('now')");
                entity.HasIndex(e => e.SessionToken).IsUnique();

                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // === Конфигурация Client (с CompanyId) ===
            modelBuilder.Entity<Client>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Phone).HasMaxLength(50);
                entity.Property(e => e.Email).HasMaxLength(200);
                entity.Property(e => e.Notes).HasMaxLength(1000);
                entity.Property(e => e.ABC_Category).HasMaxLength(1);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("datetime('now')");
                entity.Property(e => e.CompanyId).IsRequired();
                entity.HasIndex(e => e.CompanyId);
                entity.HasIndex(e => new { e.CompanyId, e.Name });

                // Связи с пользователями
                entity.HasOne(c => c.CreatedByUser)
                      .WithMany()
                      .HasForeignKey(c => c.CreatedByUserId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(c => c.UpdatedByUser)
                      .WithMany()
                      .HasForeignKey(c => c.UpdatedByUserId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // === Конфигурация Deal (с CompanyId) ===
            modelBuilder.Entity<Deal>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Description).HasMaxLength(2000);
                entity.Property(e => e.Category).HasMaxLength(100);
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("datetime('now')");
                entity.Property(e => e.CompanyId).IsRequired();
                entity.HasIndex(e => e.CompanyId);
                entity.HasIndex(e => new { e.CompanyId, e.Status });

                entity.HasOne(d => d.Client)
                      .WithMany()
                      .HasForeignKey(d => d.ClientId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Связи с пользователями
                entity.HasOne(d => d.CreatedByUser)
                      .WithMany()
                      .HasForeignKey(d => d.CreatedByUserId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(d => d.AssignedToUser)
                      .WithMany()
                      .HasForeignKey(d => d.AssignedToUserId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(d => d.UpdatedByUser)
                      .WithMany()
                      .HasForeignKey(d => d.UpdatedByUserId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // === Конфигурация Expense (с CompanyId) ===
            modelBuilder.Entity<Expense>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Category).HasMaxLength(100);
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Notes).HasMaxLength(1000);
                entity.Property(e => e.Date).HasDefaultValueSql("datetime('now')");
                entity.Property(e => e.CompanyId).IsRequired();
                entity.HasIndex(e => e.CompanyId);

                // Связи с пользователями
                entity.HasOne(e => e.CreatedByUser)
                      .WithMany()
                      .HasForeignKey(e => e.CreatedByUserId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.UpdatedByUser)
                      .WithMany()
                      .HasForeignKey(e => e.UpdatedByUserId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // === Конфигурация Notification (с CompanyId) ===
            modelBuilder.Entity<Notification>(entity =>
            {
                entity.ToTable("Notifications");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Message).HasMaxLength(1000);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("datetime('now')");
                entity.Property(e => e.Icon).HasMaxLength(10);
                entity.Property(e => e.Color).HasMaxLength(20);
                entity.Property(e => e.CompanyId).IsRequired();
                entity.HasIndex(e => new { e.CompanyId, e.IsRead });
            });

            // === Конфигурация CRMTask (с CompanyId и UserId) ===
            modelBuilder.Entity<CRMTask>(entity =>
            {
                entity.ToTable("Tasks");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("datetime('now')");
                entity.Property(e => e.CompanyId).IsRequired();
                entity.HasIndex(e => new { e.CompanyId, e.AssignedToUserId });
                entity.HasIndex(e => new { e.CompanyId, e.Status });

                entity.HasOne(t => t.Deal)
                      .WithMany()
                      .HasForeignKey(t => t.DealId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(t => t.Client)
                      .WithMany()
                      .HasForeignKey(t => t.ClientId)
                      .OnDelete(DeleteBehavior.SetNull);

                // Связи с пользователями
                entity.HasOne(t => t.CreatedByUser)
                      .WithMany()
                      .HasForeignKey(t => t.CreatedByUserId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(t => t.AssignedToUser)
                      .WithMany()
                      .HasForeignKey(t => t.AssignedToUserId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(t => t.UpdatedByUser)
                      .WithMany()
                      .HasForeignKey(t => t.UpdatedByUserId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // === Конфигурация Interaction (с CompanyId и UserId) ===
            modelBuilder.Entity<Interaction>(entity =>
            {
                entity.ToTable("Interactions");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.DateTime).HasDefaultValueSql("datetime('now')");
                entity.Property(e => e.CompanyId).IsRequired();
                entity.HasIndex(e => new { e.CompanyId, e.UserId });

                entity.HasOne(i => i.Client)
                      .WithMany()
                      .HasForeignKey(i => i.ClientId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(i => i.Deal)
                      .WithMany()
                      .HasForeignKey(i => i.DealId)
                      .OnDelete(DeleteBehavior.SetNull);

                // Связь с пользователем
                entity.HasOne(i => i.User)
                      .WithMany()
                      .HasForeignKey(i => i.UserId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // === Конфигурация CalendarEvent (с CompanyId и UserId) ===
            modelBuilder.Entity<CalendarEvent>(entity =>
            {
                entity.ToTable("CalendarEvents");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Description).HasMaxLength(2000);
                entity.Property(e => e.Location).HasMaxLength(300);
                entity.Property(e => e.Color).HasMaxLength(20);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("datetime('now')");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("datetime('now')");
                entity.Property(e => e.CompanyId).IsRequired();
                entity.HasIndex(e => new { e.CompanyId, e.StartDate });
                entity.HasIndex(e => new { e.CompanyId, e.CreatedByUserId });

                entity.HasOne(e => e.Deal)
                      .WithMany()
                      .HasForeignKey(e => e.DealId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.Client)
                      .WithMany()
                      .HasForeignKey(e => e.ClientId)
                      .OnDelete(DeleteBehavior.SetNull);

                // Связи с пользователями
                entity.HasOne(e => e.CreatedByUser)
                      .WithMany()
                      .HasForeignKey(e => e.CreatedByUserId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.AssignedToUser)
                      .WithMany()
                      .HasForeignKey(e => e.AssignedToUserId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(e => e.UpdatedByUser)
                      .WithMany()
                      .HasForeignKey(e => e.UpdatedByUserId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // === Конфигурация DeploymentConfig ===
            modelBuilder.Entity<DeploymentConfig>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CloudServerUrl).HasMaxLength(500);
                entity.Property(e => e.CloudApiKey).HasMaxLength(200);
                entity.Property(e => e.ServerIpAddress).HasMaxLength(50);
                entity.Property(e => e.LastSyncError).HasMaxLength(1000);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("datetime('now')");
                entity.Property(e => e.UpdatedAt).HasDefaultValueSql("datetime('now')");
            });

            // === Конфигурация SyncLog ===
            modelBuilder.Entity<SyncLog>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
                entity.Property(e => e.Details).HasMaxLength(2000);
                entity.Property(e => e.StartedAt).HasDefaultValueSql("datetime('now')");
                entity.HasIndex(e => new { e.CompanyId, e.StartedAt });
            });

            // === Конфигурация ServerInfo ===
            modelBuilder.Entity<ServerInfo>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ServerId).IsRequired().HasMaxLength(100);
                entity.Property(e => e.ServerName).IsRequired().HasMaxLength(200);
                entity.Property(e => e.IpAddress).HasMaxLength(50);
                entity.Property(e => e.Version).HasMaxLength(50);
                entity.Property(e => e.AllowedIpAddresses).HasMaxLength(500);
                entity.Property(e => e.ApiSecret).HasMaxLength(200);
                entity.Property(e => e.StartedAt).HasDefaultValueSql("datetime('now')");
            });
        }

        /// <summary>
        /// Автоматически устанавливает CompanyId при сохранении
        /// </summary>
        public override int SaveChanges()
        {
            try
            {
                SetCompanyIdForEntities();
                var result = base.SaveChanges();
                System.Diagnostics.Debug.WriteLine($"SaveChanges completed successfully. Rows affected: {result}");
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SaveChanges error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Inner exception: {ex.InnerException?.Message}");
                System.Diagnostics.Debug.WriteLine($"Inner inner exception: {ex.InnerException?.InnerException?.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                
                // Записываем ошибку в файл
                try
                {
                    string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyFirstCRM", "error_log.txt");
                    Directory.CreateDirectory(Path.GetDirectoryName(logPath));
                    
                    string logContent = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ОШИБКА СОХРАНЕНИЯ ДАННЫХ\n";
                    logContent += $"Основная ошибка: {ex.Message}\n";
                    if (ex.InnerException != null)
                    {
                        logContent += $"Внутреннее исключение: {ex.InnerException.Message}\n";
                        if (ex.InnerException.InnerException != null)
                        {
                            logContent += $"Внутреннее исключение 2: {ex.InnerException.InnerException.Message}\n";
                        }
                    }
                    logContent += $"Stack trace: {ex.StackTrace}\n";
                    logContent += $"Database path: {GetDatabasePath()}\n";
                    logContent += $"CurrentCompanyId: {CurrentCompanyId}\n";
                    logContent += "=====================================\n\n";
                    
                    File.AppendAllText(logPath, logContent);
                }
                catch (Exception logEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to write error log: {logEx.Message}");
                }
                
                // Дополнительная диагностика
                if (ex.InnerException != null && ex.InnerException.Message.Contains("no such table"))
                {
                    throw new Exception("Ошибка структуры базы данных. Таблица не найдена. Попробуйте переустановить приложение.", ex);
                }
                else if (ex.InnerException != null && ex.InnerException.Message.Contains("attempt to write a readonly database"))
                {
                    throw new Exception("Ошибка прав доступа. База данных доступна только для чтения.", ex);
                }
                else if (ex.InnerException != null && ex.InnerException.Message.Contains("database is locked"))
                {
                    throw new Exception("База данных заблокирована другим процессом.", ex);
                }
                
                throw new Exception($"Ошибка сохранения данных: {ex.Message}", ex);
            }
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                SetCompanyIdForEntities();
                var result = base.SaveChangesAsync(cancellationToken);
                System.Diagnostics.Debug.WriteLine($"SaveChangesAsync completed successfully");
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SaveChangesAsync error: {ex.Message}");
                throw new Exception($"Ошибка асинхронного сохранения данных: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Устанавливает CompanyId и UserId для всех новых сущностей
        /// </summary>
        private void SetCompanyIdForEntities()
        {
            // Логируем состояние для диагностики
            System.Diagnostics.Debug.WriteLine($"SetCompanyIdForEntities: CurrentCompanyId={CurrentCompanyId}, CurrentUser={CurrentUser?.Id ?? 0}");
            
            if (!CurrentCompanyId.HasValue) return;

            var entries = ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

            foreach (var entry in entries)
            {
                var entity = entry.Entity;
                
                // Устанавливаем CompanyId
                var companyIdProperty = entity.GetType().GetProperty("CompanyId");
                if (companyIdProperty != null && companyIdProperty.CanWrite)
                {
                    var currentValue = companyIdProperty.GetValue(entity);
                    if (currentValue == null || (int)currentValue == 0)
                    {
                        companyIdProperty.SetValue(entity, CurrentCompanyId.Value);
                        System.Diagnostics.Debug.WriteLine($"Set CompanyId={CurrentCompanyId.Value} for {entity.GetType().Name}");
                    }
                }

                // Устанавливаем CreatedByUserId для новых сущностей
                if (entry.State == EntityState.Added)
                {
                    var createdByProperty = entity.GetType().GetProperty("CreatedByUserId");
                    if (createdByProperty != null && createdByProperty.CanWrite && CurrentUser != null)
                    {
                        var currentValue = createdByProperty.GetValue(entity);
                        if (currentValue == null || (int)currentValue == 0)
                        {
                            createdByProperty.SetValue(entity, CurrentUser.Id);
                            System.Diagnostics.Debug.WriteLine($"Set CreatedByUserId={CurrentUser.Id} for {entity.GetType().Name}");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"CreatedByUserId already set for {entity.GetType().Name}: {currentValue}");
                        }
                    }
                    else if (createdByProperty != null && CurrentUser == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"WARNING: CreatedByProperty exists but CurrentUser is null for {entity.GetType().Name}");
                    }
                }

                // Устанавливаем UpdatedByUserId для измененных сущностей
                if (entry.State == EntityState.Modified)
                {
                    var updatedByProperty = entity.GetType().GetProperty("UpdatedByUserId");
                    if (updatedByProperty != null && updatedByProperty.CanWrite && CurrentUser != null)
                    {
                        updatedByProperty.SetValue(entity, CurrentUser.Id);
                        System.Diagnostics.Debug.WriteLine($"Set UpdatedByUserId={CurrentUser.Id} for {entity.GetType().Name}");
                    }
                    else if (updatedByProperty != null && CurrentUser == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"WARNING: UpdatedByProperty exists but CurrentUser is null for {entity.GetType().Name}");
                    }
                }
            }
        }
    }

    // Класс для работы с глобальным контекстом (компании, пользователи)
    public class GlobalDbContext : AppDbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string dbPath = Path.Combine(appDataPath, "MyFirstCRM", "global.db");

            string directory = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // В глобальной БД только Companies, Users, UserSessions, DeploymentConfigs, SyncLogs, ServerInfos
            // Остальные сущности игнорируем или конфигурируем без связей
        }
    }
}
