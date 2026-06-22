using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace MyFirstCRM
{
    /// <summary>
    /// Менеджер безопасности для многопользовательской системы с компаниями
    /// </summary>
    public static class MultiUserSecurityManager
    {
        private static User? _currentUser;
        private static Company? _currentCompany;

        /// <summary>
        /// Текущий авторизованный пользователь
        /// </summary>
        public static User? CurrentUser
        {
            get => _currentUser;
            set => _currentUser = value;
        }

        /// <summary>
        /// Текущая компания
        /// </summary>
        public static Company? CurrentCompany
        {
            get => _currentCompany;
            set => _currentCompany = value;
        }

        /// <summary>
        /// Проверяет, авторизован ли пользователь
        /// </summary>
        public static bool IsAuthenticated => CurrentUser != null && CurrentCompany != null;

        /// <summary>
        /// Проверяет, имеет ли текущий пользователь указанную роль или выше
        /// </summary>
        public static bool HasRole(UserRole minimumRole)
        {
            return CurrentUser?.HasRole(minimumRole) ?? false;
        }

        /// <summary>
        /// Проверяет, является ли текущий пользователь администратором
        /// </summary>
        public static bool IsAdmin => HasRole(UserRole.Admin);

        // ==================== УПРАВЛЕНИЕ КОМПАНИЯМИ ====================

        /// <summary>
        /// Создает новую компанию с первым администратором
        /// </summary>
        public static (bool Success, string Message, Company? Company) CreateCompany(
            string companyName,
            string adminUsername,
            string adminFullName,
            string adminPassword,
            string? contactEmail = null,
            string? contactPhone = null)
        {
            try
            {
                using var context = new GlobalDbContext();

                // Проверяем уникальность имени компании
                if (context.Companies.Any(c => c.Name.ToLower() == companyName.ToLower()))
                {
                    return (false, "Компания с таким названием уже существует", null);
                }

                // Проверяем уникальность username
                if (context.Users.Any(u => u.Username.ToLower() == adminUsername.ToLower()))
                {
                    return (false, "Пользователь с таким логином уже существует", null);
                }

                // Валидация пароля
                if (adminPassword.Length < 8)
                {
                    return (false, "Пароль должен содержать минимум 8 символов", null);
                }

                // Создаем компанию
                var company = new Company
                {
                    Name = companyName,
                    ContactEmail = contactEmail,
                    ContactPhone = contactPhone,
                    CreatedAt = DateTime.Now,
                    IsActive = true,
                    MaxUsers = 10
                };

                // Генерируем путь к базе данных компании
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string companiesDir = Path.Combine(appDataPath, "MyFirstCRM", "Companies");
                if (!Directory.Exists(companiesDir))
                    Directory.CreateDirectory(companiesDir);

                company.DatabasePath = Path.Combine(companiesDir, $"company_{company.CompanyCode}.db");

                context.Companies.Add(company);
                context.SaveChanges();

                // Создаем администратора
                var salt = GenerateSalt();
                var admin = new User
                {
                    CompanyId = company.Id,
                    Username = adminUsername.ToLower(),
                    FullName = adminFullName,
                    PasswordHash = HashPassword(adminPassword, salt),
                    PasswordSalt = salt,
                    Role = UserRole.Admin,
                    IsPrimaryAdmin = true,
                    CreatedAt = DateTime.Now,
                    IsActive = true
                };

                context.Users.Add(admin);
                context.SaveChanges();

                // Создаем базу данных компании
                CreateCompanyDatabase(company.Id);

                return (true, "Компания успешно создана", company);
            }
            catch (Exception ex)
            {
                return (false, $"Ошибка при создании компании: {ex.Message}", null);
            }
        }

        /// <summary>
        /// Создает базу данных для конкретной компании
        /// </summary>
        private static void CreateCompanyDatabase(int companyId)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Creating database for company {companyId}");
                
                using var companyContext = new AppDbContext();
                companyContext.CurrentCompanyId = companyId;
                
                // Проверяем возможность создания базы
                var canCreate = companyContext.Database.CanConnect();
                System.Diagnostics.Debug.WriteLine($"Database can connect: {canCreate}");
                
                // Создаем базу данных
                companyContext.Database.EnsureCreated();
                System.Diagnostics.Debug.WriteLine($"Database for company {companyId} created successfully");
                
                // Синхронизируем пользователей из глобальной базы в базу компании
                SyncUsersToCompanyDatabase(companyId);
                
                // Проверяем, что база работает
                try
                {
                    companyContext.Database.CanConnect();
                    System.Diagnostics.Debug.WriteLine($"Database for company {companyId} is accessible");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Database accessibility check failed: {ex.Message}");
                    throw new Exception($"База данных создана, но недоступна: {ex.Message}", ex);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create database for company {companyId}: {ex.Message}");
                throw new Exception($"Ошибка создания базы данных компании {companyId}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Синхронизирует пользователей из глобальной базы в базу компании
        /// </summary>
        private static void SyncUsersToCompanyDatabase(int companyId)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"SyncUsersToCompanyDatabase: Starting sync for company {companyId}");
                
                // Получаем пользователей из глобальной базы
                using var globalContext = new GlobalDbContext();
                var users = globalContext.Users.Where(u => u.CompanyId == companyId).ToList();
                
                System.Diagnostics.Debug.WriteLine($"SyncUsersToCompanyDatabase: Found {users.Count} users in global database for company {companyId}");
                
                if (users.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"SyncUsersToCompanyDatabase: No users to sync for company {companyId}");
                    return;
                }
                
                // Добавляем пользователей в базу компании
                using var companyContext = new AppDbContext();
                companyContext.CurrentCompanyId = companyId;
                
                // Отключаем проверку внешних ключей для синхронизации
                companyContext.Database.ExecuteSqlRaw("PRAGMA foreign_keys = OFF");
                
                foreach (var user in users)
                {
                    System.Diagnostics.Debug.WriteLine($"SyncUsersToCompanyDatabase: Processing user {user.Username} (ID: {user.Id})");
                    
                    // Проверяем, существует ли уже такой пользователь
                    var existingUser = companyContext.Users.FirstOrDefault(u => u.Id == user.Id);
                    if (existingUser == null)
                    {
                        // Создаем нового пользователя с тем же ID
                        var newUser = new User
                        {
                            Id = user.Id,
                            CompanyId = user.CompanyId,
                            Username = user.Username,
                            FullName = user.FullName,
                            Email = user.Email,
                            Phone = user.Phone,
                            PasswordHash = user.PasswordHash,
                            PasswordSalt = user.PasswordSalt,
                            Role = user.Role,
                            IsPrimaryAdmin = user.IsPrimaryAdmin,
                            Position = user.Position,
                            Department = user.Department,
                            PasswordHint = user.PasswordHint,
                            CreatedAt = user.CreatedAt,
                            LastLoginAt = user.LastLoginAt,
                            IsActive = user.IsActive,
                            FailedLoginAttempts = user.FailedLoginAttempts,
                            LockoutUntil = user.LockoutUntil
                        };
                        
                        companyContext.Users.Add(newUser);
                        System.Diagnostics.Debug.WriteLine($"SyncUsersToCompanyDatabase: Added user {user.Username} (ID: {user.Id}) to company database");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"SyncUsersToCompanyDatabase: User {user.Username} (ID: {user.Id}) already exists in company database");
                    }
                }
                
                companyContext.SaveChanges();
                
                // Включаем обратно проверку внешних ключей
                companyContext.Database.ExecuteSqlRaw("PRAGMA foreign_keys = ON");
                
                System.Diagnostics.Debug.WriteLine($"SyncUsersToCompanyDatabase: Successfully synced {users.Count} users to company database {companyId}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SyncUsersToCompanyDatabase: Failed to sync users to company database {companyId}: {ex.Message}");
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"SyncUsersToCompanyDatabase: Inner exception: {ex.InnerException.Message}");
                }
                throw new Exception($"Ошибка синхронизации пользователей в базу компании {companyId}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Получает список всех компаний
        /// </summary>
        public static List<Company> GetAllCompanies()
        {
            using var context = new GlobalDbContext();
            return context.Companies.Where(c => c.IsActive).ToList();
        }

        /// <summary>
        /// Находит компанию по коду
        /// </summary>
        public static Company? GetCompanyByCode(string code)
        {
            using var context = new GlobalDbContext();
            return context.Companies
                .FirstOrDefault(c => c.CompanyCode.ToUpper() == code.ToUpper() && c.IsActive);
        }

        // ==================== УПРАВЛЕНИЕ ПОЛЬЗОВАТЕЛЯМИ ====================

        /// <summary>
        /// Создает нового пользователя в компании
        /// </summary>
        public static (bool Success, string Message, User? User) CreateUser(
            int companyId,
            string username,
            string fullName,
            string password,
            UserRole role,
            string? email = null,
            string? phone = null,
            string? position = null,
            string? department = null,
            string? passwordHint = null)
        {
            try
            {
                using var context = new GlobalDbContext();

                // Проверяем существование компании
                var company = context.Companies.Find(companyId);
                if (company == null)
                    return (false, "Компания не найдена", null);

                // Проверяем лимит пользователей
                var currentUserCount = context.Users.Count(u => u.CompanyId == companyId);
                if (company.MaxUsers > 0 && currentUserCount >= company.MaxUsers)
                {
                    return (false, $"Достигнут лимит пользователей ({company.MaxUsers})", null);
                }

                // Проверяем уникальность username
                if (context.Users.Any(u => u.CompanyId == companyId &&
                    u.Username.ToLower() == username.ToLower()))
                {
                    return (false, "Пользователь с таким логином уже существует в этой компании", null);
                }

                // Валидация пароля
                if (password.Length < 6)
                {
                    return (false, "Пароль должен содержать минимум 6 символов", null);
                }

                var salt = GenerateSalt();
                var user = new User
                {
                    CompanyId = companyId,
                    Username = username.ToLower(),
                    FullName = fullName,
                    PasswordHash = HashPassword(password, salt),
                    PasswordSalt = salt,
                    Role = role,
                    Email = email,
                    Phone = phone,
                    Position = position,
                    Department = department,
                    PasswordHint = passwordHint,
                    CreatedAt = DateTime.Now,
                    IsActive = true
                };

                context.Users.Add(user);
                context.SaveChanges();

                return (true, "Пользователь успешно создан", user);
            }
            catch (Exception ex)
            {
                return (false, $"Ошибка при создании пользователя: {ex.Message}", null);
            }
        }

        /// <summary>
        /// Получает список пользователей компании
        /// </summary>
        public static List<User> GetCompanyUsers(int companyId)
        {
            using var context = new GlobalDbContext();
            return context.Users
                .Where(u => u.CompanyId == companyId)
                .OrderBy(u => u.FullName)
                .ToList();
        }

        /// <summary>
        /// Обновляет данные пользователя
        /// </summary>
        public static (bool Success, string Message) UpdateUser(User user)
        {
            try
            {
                using var context = new GlobalDbContext();
                context.Users.Update(user);
                context.SaveChanges();
                return (true, "Данные пользователя обновлены");
            }
            catch (Exception ex)
            {
                return (false, $"Ошибка при обновлении: {ex.Message}");
            }
        }

        /// <summary>
        /// Удаляет пользователя (только админ может удалять)
        /// </summary>
        public static (bool Success, string Message) DeleteUser(int userId)
        {
            try
            {
                using var context = new GlobalDbContext();
                var user = context.Users.Find(userId);
                if (user == null)
                    return (false, "Пользователь не найден");

                if (user.IsPrimaryAdmin)
                    return (false, "Нельзя удалить главного администратора компании");

                context.Users.Remove(user);
                context.SaveChanges();
                return (true, "Пользователь удален");
            }
            catch (Exception ex)
            {
                return (false, $"Ошибка при удалении: {ex.Message}");
            }
        }

        /// <summary>
        /// Меняет пароль пользователя
        /// </summary>
        public static (bool Success, string Message) ChangePassword(int userId, string newPassword)
        {
            try
            {
                if (newPassword.Length < 6)
                    return (false, "Пароль должен содержать минимум 6 символов");

                using var context = new GlobalDbContext();
                var user = context.Users.Find(userId);
                if (user == null)
                    return (false, "Пользователь не найден");

                var salt = GenerateSalt();
                user.PasswordHash = HashPassword(newPassword, salt);
                user.PasswordSalt = salt;

                context.SaveChanges();
                return (true, "Пароль успешно изменен");
            }
            catch (Exception ex)
            {
                return (false, $"Ошибка при смене пароля: {ex.Message}");
            }
        }

        // ==================== АУТЕНТИФИКАЦИЯ ====================

        /// <summary>
        /// Авторизация пользователя
        /// </summary>
        public static (bool Success, string Message) Login(string companyCode, string username, string password)
        {
            try
            {
                using var context = new GlobalDbContext();

                // Находим компанию
                var company = context.Companies
                    .FirstOrDefault(c => c.CompanyCode.ToUpper() == companyCode.ToUpper());

                if (company == null)
                    return (false, "Компания не найдена");

                if (!company.IsActive)
                    return (false, "Компания деактивирована");

                // Находим пользователя
                var user = context.Users
                    .FirstOrDefault(u => u.CompanyId == company.Id &&
                                        u.Username.ToLower() == username.ToLower());

                if (user == null)
                    return (false, "Неверный логин или пароль");

                // Проверяем блокировку
                if (user.IsLockedOut())
                {
                    var remaining = user.LockoutUntil.Value - DateTime.Now;
                    return (false, $"Аккаунт заблокирован. Попробуйте через {remaining:mm\\:ss}");
                }

                if (!user.IsActive)
                    return (false, "Аккаунт деактивирован");

                // Проверяем пароль
                var hash = HashPassword(password, user.PasswordSalt);
                if (hash != user.PasswordHash)
                {
                    // Увеличиваем счетчик неудачных попыток
                    user.FailedLoginAttempts++;
                    if (user.FailedLoginAttempts >= 5)
                    {
                        user.LockoutUntil = DateTime.Now.AddMinutes(15);
                    }
                    context.SaveChanges();

                    var remainingAttempts = 5 - user.FailedLoginAttempts;
                    var message = remainingAttempts > 0
                        ? $"Неверный пароль. Осталось попыток: {remainingAttempts}"
                        : "Аккаунт заблокирован на 15 минут";
                    return (false, message);
                }

                // Успешный вход - сбрасываем счетчики
                user.FailedLoginAttempts = 0;
                user.LockoutUntil = null;
                user.LastLoginAt = DateTime.Now;
                context.SaveChanges();

                // Устанавливаем текущего пользователя и компанию
                CurrentUser = user;
                CurrentCompany = company;

                return (true, "Вход выполнен успешно");
            }
            catch (Exception ex)
            {
                return (false, $"Ошибка при входе: {ex.Message}");
            }
        }

        /// <summary>
        /// Выход из системы
        /// </summary>
        public static void Logout()
        {
            CurrentUser = null;
            CurrentCompany = null;
        }

        // ==================== ХЕШИРОВАНИЕ ====================

        /// <summary>
        /// Генерирует случайную соль
        /// </summary>
        private static string GenerateSalt()
        {
            byte[] saltBytes = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(saltBytes);
            }
            return Convert.ToBase64String(saltBytes);
        }

        /// <summary>
        /// Хеширует пароль с солью
        /// </summary>
        private static string HashPassword(string password, string salt)
        {
            using (var sha256 = SHA256.Create())
            {
                var saltedPassword = password + salt;
                var bytes = Encoding.UTF8.GetBytes(saltedPassword);
                var hash = sha256.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
        }

        /// <summary>
        /// Публичный метод для хеширования пароля с солью
        /// </summary>
        public static string HashPasswordWithSalt(string password, string salt)
        {
            return HashPassword(password, salt);
        }

        /// <summary>
        /// Проверяет сложность пароля
        /// </summary>
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

            return score switch
            {
                <= 2 => PasswordStrength.Weak,
                3 or 4 => PasswordStrength.Medium,
                5 => PasswordStrength.Strong,
                _ => PasswordStrength.VeryStrong
            };
        }

        public enum PasswordStrength
        {
            None,
            Weak,
            Medium,
            Strong,
            VeryStrong
        }

        // ==================== УТИЛИТЫ ====================

        /// <summary>
        /// Проверяет существование хотя бы одной компании
        /// </summary>
        public static bool HasAnyCompanies()
        {
            try
            {
                using var context = new GlobalDbContext();
                return context.Companies.Any();
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Инициализирует глобальную базу данных при первом запуске
        /// </summary>
        public static void InitializeGlobalDatabase()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Initializing global database...");
                
                using var context = new GlobalDbContext();
                
                // Проверяем права доступа к папке
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string basePath = Path.Combine(appDataPath, "MyFirstCRM");
                
                if (!Directory.Exists(basePath))
                {
                    Directory.CreateDirectory(basePath);
                    System.Diagnostics.Debug.WriteLine($"Created base directory: {basePath}");
                }
                
                // Проверяем возможность записи
                var testFile = Path.Combine(basePath, "test_write.tmp");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                System.Diagnostics.Debug.WriteLine("Write permissions verified");
                
                // Создаем базу данных
                context.Database.EnsureCreated();
                System.Diagnostics.Debug.WriteLine("Global database initialized successfully");
                
                // Проверяем доступность
                if (context.Database.CanConnect())
                {
                    System.Diagnostics.Debug.WriteLine("Global database is accessible");
                }
                else
                {
                    throw new Exception("Глобальная база данных недоступна после создания");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Global database initialization failed: {ex.Message}");
                throw new Exception($"Ошибка инициализации глобальной базы данных: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Создает контекст базы данных для текущей компании
        /// </summary>
        public static AppDbContext CreateCompanyContext()
        {
            if (CurrentCompany == null)
                throw new InvalidOperationException("Не выбрана компания");

            var context = new AppDbContext();
            context.CurrentCompanyId = CurrentCompany.Id;
            context.CurrentUser = CurrentUser;
            
            // Проверяем и добавляем недостающие колонки
            context.EnsureMissingColumns();
            System.Diagnostics.Debug.WriteLine($"EnsureMissingColumns completed for company {CurrentCompany.Id}");
            
            // Синхронизируем пользователей из глобальной базы (для обратной совместимости)
            EnsureUsersSynced(CurrentCompany.Id);
            
            return context;
        }
        
        /// <summary>
        /// Проверяет и синхронизирует пользователей при необходимости
        /// </summary>
        private static void EnsureUsersSynced(int companyId)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"EnsureUsersSynced: Checking company {companyId}");
                
                using var companyContext = new AppDbContext();
                companyContext.CurrentCompanyId = companyId;
                
                // Проверяем и добавляем недостающие колонки
                companyContext.EnsureMissingColumns();
                System.Diagnostics.Debug.WriteLine($"EnsureMissingColumns completed for company {companyId}");
                
                // Проверяем, есть ли пользователи в базе компании
                var userCount = companyContext.Users.Count();
                System.Diagnostics.Debug.WriteLine($"EnsureUsersSynced: Found {userCount} users in company database {companyId}");
                
                // Записываем в лог
                try
                {
                    string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyFirstCRM", "sync_log.txt");
                    Directory.CreateDirectory(Path.GetDirectoryName(logPath));
                    File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] EnsureUsersSynced: Company {companyId}, Users: {userCount}\n");
                }
                catch { }
                
                if (userCount == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"No users in company database {companyId}, trying to sync...");
                    
                    try
                    {
                        SyncUsersToCompanyDatabase(companyId);
                        
                        // Проверяем результат синхронизации
                        var newUserCount = companyContext.Users.Count();
                        System.Diagnostics.Debug.WriteLine($"After sync: {newUserCount} users in company database {companyId}");
                        
                        // Записываем результат в лог
                        try
                        {
                            string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyFirstCRM", "sync_log.txt");
                            File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Sync result: {newUserCount} users after sync\n");
                        }
                        catch { }
                        
                        if (newUserCount > 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"Users synced successfully");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"Sync failed - trying to recreate database...");
                            RecreateCompanyDatabase(companyId);
                        }
                    }
                    catch (Exception syncEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Sync failed: {syncEx.Message}, trying to recreate database...");
                        try
                        {
                            string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyFirstCRM", "sync_log.txt");
                            File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Sync failed: {syncEx.Message}\n");
                        }
                        catch { }
                        
                        RecreateCompanyDatabase(companyId);
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Users already exist in company database {companyId}, no sync needed");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to ensure users synced: {ex.Message}");
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
                
                // Записываем ошибку в лог
                try
                {
                    string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyFirstCRM", "sync_log.txt");
                    File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR: {ex.Message}\n");
                }
                catch { }
                
                // Не выбрасываем исключение, чтобы не блокировать работу приложения
            }
        }
        
        /// <summary>
        /// Пересоздает базу данных компании
        /// </summary>
        private static void RecreateCompanyDatabase(int companyId)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Recreating company database {companyId}");
                
                // Получаем информацию о компании
                using var globalContext = new GlobalDbContext();
                var company = globalContext.Companies.Find(companyId);
                if (company == null)
                {
                    System.Diagnostics.Debug.WriteLine($"Company {companyId} not found in global database");
                    return;
                }
                
                // Удаляем старый файл базы данных
                if (File.Exists(company.DatabasePath))
                {
                    File.Delete(company.DatabasePath);
                    System.Diagnostics.Debug.WriteLine($"Deleted old database file: {company.DatabasePath}");
                }
                
                // Создаем новую базу данных
                CreateCompanyDatabase(companyId);
                
                System.Diagnostics.Debug.WriteLine($"Company database {companyId} recreated successfully");
                
                // Записываем в лог
                try
                {
                    string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyFirstCRM", "sync_log.txt");
                    File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Company database {companyId} recreated\n");
                }
                catch { }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to recreate company database {companyId}: {ex.Message}");
                
                // Записываем ошибку в лог
                try
                {
                    string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MyFirstCRM", "sync_log.txt");
                    File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Recreation failed: {ex.Message}\n");
                }
                catch { }
            }
        }
    }
}
