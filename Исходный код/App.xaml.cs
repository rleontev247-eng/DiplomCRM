using System;

using System.Linq;

using System.Windows;

using System.Diagnostics;

using System.IO;

using System.Reflection;

using Microsoft.EntityFrameworkCore;



namespace MyFirstCRM

{

    public partial class App : Application

    {

        private bool _isMainWindowLaunched = false;



        private void App_Startup(object sender, StartupEventArgs e)

        {

            try

            {

                Console.WriteLine("=== DiplomCRM Startup ===");

                Console.WriteLine($"Start time: {DateTime.Now}");



                // Иконка приложения уже существует в Resources



                // 1. Инициализация глобальной базы данных (компании и пользователи)

                try

                {

                    MultiUserSecurityManager.InitializeGlobalDatabase();

                    Console.WriteLine("✅ Глобальная база данных инициализирована");

                }

                catch (Exception ex)

                {

                    Console.WriteLine($"❌ Ошибка инициализации глобальной базы: {ex.Message}");

                    MessageBox.Show(

                        $"Ошибка инициализации базы данных:\n{ex.Message}\n\n" +

                        "Возможные причины:\n" +

                        "• Недостаточно прав доступа к папке приложения\n" +

                        "• Антивирус блокирует создание файлов\n" +

                        "• Системная папка недоступна\n\n" +

                        "Попробуйте запустить приложение от имени администратора или добавьте в исключения антивируса.",

                        "Ошибка базы данных",

                        MessageBoxButton.OK,

                        MessageBoxImage.Error);

                    Application.Current.Shutdown(1);

                    return;

                }

                

                // Применяем миграцию для добавления столбца ABC_Category ко всем базам данных компаний

                // MigrationHelper.ApplyABC_CategoryMigrationToAllCompanies();

                Console.WriteLine("✅ Миграция ABC_Category пропущена (удалена)");

                

                // Инициализируем менеджер резервного копирования

                var backupManager = BackupManager.Instance;

                Console.WriteLine("✅ Менеджер резервного копирования инициализирован");

                

                // 2. Проверяем, есть ли компании в системе

                bool hasCompanies = MultiUserSecurityManager.HasAnyCompanies();

                Console.WriteLine($"Has companies: {hasCompanies}");



                // 3. Определяем какое окно открывать

                if (!hasCompanies)

                {

                    // Первый запуск - создаем компанию

                    Console.WriteLine("Opening CompanyRegistrationWindow");

                    var window = new CompanyRegistrationWindow();

                    window.Show();

                }

                else

                {

                    // Есть компании - показываем окно входа

                    Console.WriteLine("Opening UserLoginWindow");

                    var window = new UserLoginWindow();

                    window.Show();

                }

            }

            catch (Exception ex)

            {

                Console.WriteLine($"CRITICAL ERROR: {ex.Message}");

                MessageBox.Show(

                    $"Критическая ошибка при запуске:\n{ex.Message}\n\n" +

                    "Попробуйте переустановить приложение или обратитесь в поддержку.",

                    "Ошибка запуска",

                    MessageBoxButton.OK,

                    MessageBoxImage.Error);

                Application.Current.Shutdown(1);

            }

        }



        private void FixDatabaseSchema()

        {

            try

            {

                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

                string dbPath = Path.Combine(appDataPath, "MyFirstCRM", "database.db");



                // Если база существует и старая - удаляем ее

                if (File.Exists(dbPath))

                {

                    // Проверяем размер базы

                    var fileInfo = new FileInfo(dbPath);



                    // Если база слишком старая или повреждена, удаляем

                    if (fileInfo.Length < 1000) // Маленький размер = возможно, старая схема

                    {

                        File.Delete(dbPath);

                        Console.WriteLine("✅ Удалена старая/поврежденная база данных");

                    }

                }



                // Создаем новую базу с правильной схемой

                using (var context = new AppDbContext())

                {

                    context.Database.EnsureCreated();

                    Console.WriteLine("✅ База данных проверена/создана");

                    

                    // Инициализируем новые таблицы

                    DatabaseInitializer.Initialize();



                    // Добавляем минимальные тестовые данные

                    if (!context.Clients.Any())

                    {

                        var testClient = new Client

                        {

                            Name = "Тестовый Клиент",

                            Phone = "+7 (999) 000-00-00",

                            Email = "test@example.com",

                            Notes = "Демонстрационный клиент",

                            CreatedAt = DateTime.Now

                        };

                        context.Clients.Add(testClient);

                        context.SaveChanges();

                        Console.WriteLine("✅ Добавлен тестовый клиент");

                    }

                }

            }

            catch (Exception ex)

            {

                Console.WriteLine($"Ошибка фикса базы данных: {ex.Message}");

                // Пробуем создать заново

                try

                {

                    string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

                    string dbPath = Path.Combine(appDataPath, "MyFirstCRM", "database.db");

                    if (File.Exists(dbPath)) File.Delete(dbPath);



                    using (var context = new AppDbContext())

                    {

                        context.Database.EnsureCreated();

                    }

                }

                catch

                {

                    // Игнорируем - пользователь получит сообщение об ошибке

                }

            }

        }



        public static void SafeLaunchMain()

        {

            try

            {

                WriteLog("Starting SafeLaunchMain");



                // Ждем 100мс для стабильности

                System.Threading.Thread.Sleep(100);



                // Создаем главное окно

                var mainWindow = new MainWindow();



                // Показываем его

                mainWindow.Show();



                WriteLog("MainWindow shown");



                // Закрываем все другие окна

                CloseAllOtherWindows(mainWindow);

            }

            catch (Exception ex)

            {

                WriteLog($"Error in SafeLaunchMain: {ex.Message}\n{ex.StackTrace}");



                // Аварийный запуск

                try

                {

                    new MainWindow().Show();

                }

                catch { }

            }

        }



        private static void CloseAllOtherWindows(MainWindow mainWindow)

        {

            try

            {

                var windows = new List<Window>();



                // Собираем все окна кроме главного

                foreach (Window window in Application.Current.Windows)

                {

                    if (window != mainWindow)

                    {

                        windows.Add(window);

                    }

                }



                // Закрываем их

                foreach (var window in windows)

                {

                    try

                    {

                        window.Close();

                    }

                    catch { }

                }



                WriteLog($"Closed {windows.Count} other windows");

            }

            catch (Exception ex)

            {

                WriteLog($"Error closing windows: {ex.Message}");

            }

        }



        public static void EmergencyLaunch()

        {

            try

            {

                // Аварийный запуск - просто главное окно
                var window = new MainWindow();

                window.Show();

            }

            catch

            {

                // Последняя попытка

                Application.Current.Shutdown();

            }

        }



        public static void WriteLog(string message)

        {

            string logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "crm_debug.log");

            File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n");

        }



       

        private void CreateDatabaseStructure()

        {

            try

            {

                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

                string dbPath = Path.Combine(appDataPath, "MyFirstCRM", "database.db");



                // Удаляем старую базу данных, если она существует

                if (File.Exists(dbPath))

                {

                    // Сначала закрываем все соединения

                    try

                    {

                        // Закрываем контекст, если он открыт

                        // Просто удаляем файл

                        File.Delete(dbPath);

                        Console.WriteLine("✅ Удалена старая база данных из-за несоответствия схемы");

                    }

                    catch

                    {

                        // Если не удается удалить, пробуем переименовать

                        string backupPath = dbPath + ".old";

                        if (File.Exists(backupPath))

                        {

                            File.Delete(backupPath);

                        }

                        File.Move(dbPath, backupPath);

                        Console.WriteLine($"⚠️ База данных переименована в {backupPath}");

                    }

                }



                // Создаем новую базу данных с правильной схемой

                using (var context = new AppDbContext())

                {

                    context.Database.EnsureCreated();

                    Console.WriteLine("✅ Новая база данных создана");



                    // Добавляем тестовые данные только если база пустая

                    if (!context.Clients.Any() && !context.Deals.Any())

                    {

                        AddTestData(context);

                    }

                }

            }

            catch (Exception ex)

            {

                Console.WriteLine($"❌ Ошибка создания базы: {ex.Message}");

            }

        }



        private void AddTestData(AppDbContext context)

        {

            try

            {

                Console.WriteLine("Adding test data...");



                // Добавляем тестовых клиентов

                var clients = new List<Client>

        {

            new Client

            {

                Name = "Иванов Иван Иванович",

                Phone = "+7 (999) 123-45-67",

                Email = "ivanov@example.com",

                Notes = "Постоянный клиент, VIP статус",

                CreatedAt = DateTime.Now.AddDays(-30)

            },

            new Client

            {

                Name = "Петрова Анна Сергеевна",

                Phone = "+7 (999) 987-65-43",

                Email = "petrova@example.com",

                Notes = "Частые заказы, предпочитает телефонные звонки",

                CreatedAt = DateTime.Now.AddDays(-25)

            },

            new Client

            {

                Name = "Сидоров Алексей Петрович",

                Phone = "+7 (999) 555-44-33",

                Email = "sidorov@mail.ru",

                Notes = "Новый клиент, требуется дополнительное внимание",

                CreatedAt = DateTime.Now.AddDays(-10)

            },

            new Client

            {

                Name = "Козлова Мария Владимировна",

                Phone = "+7 (999) 777-88-99",

                Email = "kozlova@gmail.com",

                Notes = "Корпоративный клиент, оптовые закупки",

                CreatedAt = DateTime.Now.AddDays(-5)

            },

            new Client

            {

                Name = "Новиков Дмитрий Александрович",

                Phone = "+7 (999) 666-55-44",

                Email = "novikov@yandex.ru",

                Notes = "Менеджер по закупкам ООО 'СтройТех'",

                CreatedAt = DateTime.Now.AddDays(-3)

            }

        };



                context.Clients.AddRange(clients);

                context.SaveChanges();

                Console.WriteLine($"Added {clients.Count} test clients");



                // Добавляем тестовые сделки

                var random = new Random();

                var deals = new List<Deal>();

                var dealTitles = new[]

                {

            "Поставка офисной техники",

            "Разработка программного обеспечения",

            "Консультационные услуги",

            "Ремонт офисного помещения",

            "Закупка канцелярских товаров",

            "Обслуживание компьютерной сети",

            "Маркетинговые услуги",

            "Дизайн интерьера",

            "Юридическое сопровождение",

            "Бухгалтерские услуги"

        };



                var categories = new[] { "IT", "Консалтинг", "Ремонт", "Поставки", "Услуги" };



                foreach (var client in clients)

                {

                    // Каждому клиенту добавляем 2-3 сделки

                    for (int i = 0; i < random.Next(2, 4); i++)

                    {

                        var deal = new Deal

                        {

                            Title = $"{dealTitles[random.Next(dealTitles.Length)]} для {client.Name.Split(' ')[0]}",

                            Description = $"Сделка по {dealTitles[random.Next(dealTitles.Length)].ToLower()}",

                            Amount = random.Next(10000, 500000),

                            Status = (DealStatus)random.Next(0, 4),

                            CreatedAt = DateTime.Now.AddDays(-random.Next(1, 30)),

                            Deadline = DateTime.Now.AddDays(random.Next(1, 90)),

                            ClientId = client.Id,

                            Probability = random.Next(10, 100),

                            Category = categories[random.Next(categories.Length)],

                            Priority = (Priority)random.Next(0, 4)

                        };



                        // Если сделка завершена, добавляем дату закрытия

                        if (deal.Status == DealStatus.Successful || deal.Status == DealStatus.Failed)

                        {

                            deal.ClosedAt = deal.CreatedAt.AddDays(random.Next(1, 10));

                        }



                        deals.Add(deal);

                    }

                }



                context.Deals.AddRange(deals);

                context.SaveChanges();

                Console.WriteLine($"Added {deals.Count} test deals");



                // Добавляем тестовые расходы (если таблица существует)

                try

                {

                    var expenses = new List<Expense>

            {

                new Expense { Title = "Аренда офиса", Category = "Аренда", Amount = 50000, Date = DateTime.Now.AddMonths(-1), Type = ExpenseType.Rent },

                new Expense { Title = "Зарплата сотрудникам", Category = "Зарплата", Amount = 150000, Date = DateTime.Now.AddMonths(-1), Type = ExpenseType.Salary },

                new Expense { Title = "Закупка компьютеров", Category = "Оборудование", Amount = 75000, Date = DateTime.Now.AddMonths(-2), Type = ExpenseType.Equipment },

                new Expense { Title = "Рекламная кампания", Category = "Маркетинг", Amount = 30000, Date = DateTime.Now.AddMonths(-1), Type = ExpenseType.Marketing },

                new Expense { Title = "Коммунальные услуги", Category = "Коммунальные", Amount = 15000, Date = DateTime.Now.AddMonths(-1), Type = ExpenseType.Utilities }

            };



                    context.Expenses.AddRange(expenses);

                    context.SaveChanges();

                    Console.WriteLine($"Added {expenses.Count} test expenses");

                }

                catch

                {

                    Console.WriteLine("Expenses table not available, skipping");

                }



                // Добавляем тестовые уведомления

                try

                {

                    var notifications = new List<Notification>

            {

                new Notification

                {

                    Title = "🎉 Добро пожаловать в DiplomCRM!",

                    Message = "Ваша CRM система успешно настроена и готова к работе.",

                    Type = NotificationType.System,

                    Icon = "🚀",

                    Color = "#3B82F6",

                    CreatedAt = DateTime.Now.AddHours(-1)

                },

                new Notification

                {

                    Title = "📊 Добавлены демо-данные",

                    Message = "В систему добавлены демонстрационные клиенты и сделки. Вы можете их редактировать или удалить.",

                    Type = NotificationType.Info,

                    Icon = "📈",

                    Color = "#10B981",

                    CreatedAt = DateTime.Now.AddMinutes(-30)

                }

            };



                    context.Notifications.AddRange(notifications);

                    context.SaveChanges();

                    Console.WriteLine($"Added {notifications.Count} test notifications");

                }

                catch

                {

                    Console.WriteLine("Notifications table not available, skipping");

                }



                Console.WriteLine("✅ Test data successfully added!");

            }

            catch (Exception ex)

            {

                Console.WriteLine($"Error adding test data: {ex.Message}");

                // Не прерываем выполнение, если не удалось добавить тестовые данные

            }

        }



        // Новый метод: запустить основной интерфейс после успешной аутентификации

        public void LaunchMainInterface()

        {

            try

            {

                if (_isMainWindowLaunched) return;



                Console.WriteLine("=== Launching Main Interface ===");



                // Создаем главное окно

                var mainWindow = new MainWindow();



                // Отмечаем, что главное окно запущено

                _isMainWindowLaunched = true;



                // Показываем главное окно

                mainWindow.Show();



                Console.WriteLine("MainWindow launched successfully");

            }

            catch (Exception ex)

            {

                Console.WriteLine($"ERROR launching main interface: {ex.Message}");

                MessageBox.Show($"Ошибка запуска: {ex.Message}", "Ошибка",

                    MessageBoxButton.OK, MessageBoxImage.Error);

            }

        }



        protected override void OnExit(ExitEventArgs e)

        {

            Console.WriteLine($"=== Application exiting with code: {e.ApplicationExitCode} ===");

            Console.WriteLine($"IsMainWindowLaunched: {_isMainWindowLaunched}");

            Console.WriteLine($"Active windows count: {Windows.Count}");



            base.OnExit(e);

        }



        // Метод для безопасного перехода

        public static void SafeLaunchMainInterface()

        {

            if (Application.Current is App app)

            {

                app.LaunchMainInterface();

            }

        }

    }

}