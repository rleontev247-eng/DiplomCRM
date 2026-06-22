using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using System.IO;
using System.Windows.Documents;
using System.Text;
using System.Diagnostics;
using System.Collections.ObjectModel;
using System.Windows.Media.Animation;

namespace MyFirstCRM
{
    public partial class MainWindow : Window
    {
        private AppDbContext _context;
        private ObservableCollection<Client> _allClients, _filteredClients;
        private int _currentPage = 1;
        private const int _pageSize = 10;
        private int _totalPages = 1;
        private int _currentClientId = 0;
        private bool _isSearching = false;
        private int totalRecords = 0;
        private int dealsCurrentPage = 1;
        private int dealsPageSize = 10;
        private int dealsTotalPages = 1;
        private int dealsTotalRecords = 0;

        // Переменные для сделок
        private ObservableCollection<Deal> _allDeals;
        private ObservableCollection<Deal> _filteredDeals;
        private int _currentDealId = 0;
        private int _currentDealPage = 1;
        private const int _dealsPageSize = 10;
        private int _dealsTotalPages;

        private AppSettings _settings = AppSettings.Load();

        public class FinanceTransaction
        {
            public DateTime Date { get; set; }
            public string Title { get; set; }
            public string Category { get; set; }
            public decimal Amount { get; set; }
            public string TypeName { get; set; } // "ДОХОД" или "РАСХОД"
            public string TypeColor { get; set; } // Зеленый или Красный
            public string AmountColor { get; set; }
            public string AmountDisplay => TypeName == "ДОХОД" ? $"+ {Amount:N0} ₽" : $"- {Amount:N0} ₽";
        }


        public MainWindow()
        {
            try
            {
                InitializeComponent();

                // Инициализация данных
                InitializeDbContext();
                LoadClients();
                UpdatePaginationDisplay();
                UpdateStats();

                // Проверка уведомлений
                CheckAndUpdateNotifications();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка создания главного окна:\n{ex.Message}", "Критическая ошибка");
                Application.Current.Shutdown();
            }

        }
        private void InitializeDbContext()
        {
            try
            {
                _context?.Dispose();

                // Проверяем авторизацию
                if (!MultiUserSecurityManager.IsAuthenticated)
                {
                    MessageBox.Show("Не выполнен вход в систему", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    Application.Current.Shutdown();
                    return;
                }

                // Создаем контекст с текущей компанией
                _context = MultiUserSecurityManager.CreateCompanyContext();

                // Проверяем доступность базы
                if (!_context.Database.CanConnect())
                {
                    MessageBox.Show("База данных компании недоступна. Создаем новую...", "Информация");
                    _context.Database.EnsureCreated();
                }

                Debug.WriteLine("База данных компании успешно инициализирована");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка инициализации базы данных: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Отображаем информацию о текущем пользователе и компании
            if (MultiUserSecurityManager.CurrentUser != null)
            {
                UserNameText.Text = MultiUserSecurityManager.CurrentUser.FullName;
                UserAvatar.Text = MultiUserSecurityManager.CurrentUser.FullName[..1].ToUpper();
            }
            if (MultiUserSecurityManager.CurrentCompany != null)
            {
                CompanyNameText.Text = MultiUserSecurityManager.CurrentCompany.Name;
            }

            var textBoxes = FindVisualChildren<TextBox>(this);
            foreach (var textBox in textBoxes)
            {
                InputMethod.SetPreferredImeState(textBox, InputMethodState.On);
                InputMethod.SetIsInputMethodEnabled(textBox, true);
            }
            // NavClients.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3498DB"));
            LoadSettings();
            RFMStartDate.SelectedDate = DateTime.Now.AddYears(-1);
            RFMEndDate.SelectedDate = DateTime.Now;
            EfficiencyStartDate.SelectedDate = DateTime.Now.AddMonths(-6);
            EfficiencyEndDate.SelectedDate = DateTime.Now;

            // Период по умолчанию для финансового отчета
            if (ProfitReportStartDate != null && ProfitReportStartDate.SelectedDate == null)
                ProfitReportStartDate.SelectedDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            if (ProfitReportEndDate != null && ProfitReportEndDate.SelectedDate == null)
                ProfitReportEndDate.SelectedDate = DateTime.Now;
        }

        private void OnUserInfoClick(object sender, MouseButtonEventArgs e)
        {
            try
            {
                // Проверяем, что сессия активна
                if (MultiUserSecurityManager.CurrentUser == null)
                {
                    MessageBox.Show("Сессия истекла. Пожалуйста, войдите снова.", "Ошибка", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Открываем меню с информацией о пользователе
                var menu = new ContextMenu();

                menu.Items.Add(new MenuItem
                {
                    Header = $"👤 {MultiUserSecurityManager.CurrentUser?.FullName ?? "Неизвестно"}",
                    IsEnabled = false
                });
                menu.Items.Add(new MenuItem
                {
                    Header = $"🏢 {MultiUserSecurityManager.CurrentCompany?.Name ?? "Неизвестно"}",
                    IsEnabled = false
                });
                menu.Items.Add(new MenuItem
                {
                    Header = $"Роль: {GetRoleName(MultiUserSecurityManager.CurrentUser?.Role ?? UserRole.Employee)}",
                    IsEnabled = false
                });
                menu.Items.Add(new Separator());

                // Управление пользователями (только для админов)
                if (MultiUserSecurityManager.IsAdmin)
                {
                    var manageUsersItem = new MenuItem { Header = "👥 Управление пользователями" };
                    manageUsersItem.Click += (s, args) =>
                    {
                        try
                        {
                            var userManagementWindow = new UserManagementWindow();
                            userManagementWindow.ShowDialog();
                            LoadClients();
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Ошибка открытия окна: {ex.Message}", "Ошибка");
                        }
                    };
                    menu.Items.Add(manageUsersItem);
                    menu.Items.Add(new Separator());
                }

                var logoutItem = new MenuItem { Header = "🚪 Выйти из системы" };
                logoutItem.Click += (s, args) =>
                {
                    try
                    {
                        MultiUserSecurityManager.Logout();
                        var loginWindow = new UserLoginWindow();
                        loginWindow.Show();
                        this.Close();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка выхода: {ex.Message}", "Ошибка");
                    }
                };
                menu.Items.Add(logoutItem);

                // Проверяем PlacementTarget
                if (UserInfoBorder != null)
                {
                    menu.PlacementTarget = UserInfoBorder;
                    menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                }
                menu.IsOpen = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка открытия меню: {ex.Message}\n\n{ex.StackTrace}", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetRoleName(UserRole role)
        {
            return role switch
            {
                UserRole.Admin => "Администратор",
                UserRole.Manager => "Менеджер",
                UserRole.Employee => "Сотрудник",
                UserRole.Viewer => "Только просмотр",
                _ => "Неизвестно"
            };
        }

        private void CreateTestData_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show(
                    "Создать тестовые данные?\n\nБудет добавлено:\n• 7 клиентов (IT-компании)\n• 7 сделок (разработка ПО)\n• 7 расходов (операционные)",
                    "Подтверждение",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes) return;

                // Тема: Веб-студия / Digital Agency
                var testClients = new[]
                {
                    ("ООО \"ТехноПрогресс\"", "+7 (495) 123-45-67", "info@technoprogress.ru", "Крупная IT-компания, заказчик корпоративных порталов"),
                    ("ООО \"СмартРитейл\"", "+7 (812) 234-56-78", "contact@smartretail.ru", "Сеть магазинов электроники, нужен e-commerce"),
                    ("ООО \"МедиаГрупп\"", "+7 (499) 345-67-89", "hello@mediagroup.ru", "Медиа-холдинг, разработка стриминговой платформы"),
                    ("ООО \"ЛогистикПро\"", "+7 (383) 456-78-90", "order@logistikpro.ru", "Логистическая компания, TMS-система"),
                    ("ООО \"ФинТехСолюшнс\"", "+7 (495) 567-89-01", "sales@fintech-sol.ru", "Финтех-стартап, мобильное банковское приложение"),
                    ("ООО \"ОбразПлюс\"", "+7 (843) 678-90-12", "support@obrazplus.ru", "Образовательная платформа, онлайн-курсы"),
                    ("ООО \"ЭкоФуд\"", "+7 (495) 789-01-23", "manager@ecofud.ru", "Производитель органических продуктов, маркетплейс")
                };

                var testDeals = new[]
                {
                    ("Разработка корпоративного портала", "Внутренний портал для сотрудников с личными кабинетами", 850000m, DealStatus.InProgress, 75, "Веб-разработка", Priority.High),
                    ("Интернет-магазин электроники", "Платформа e-commerce с интеграцией 1C и платежными системами", 1200000m, DealStatus.New, 60, "E-commerce", Priority.Critical),
                    ("Стриминговая платформа", "Видео-хостинг с системой монетизации для блогеров", 2500000m, DealStatus.InProgress, 80, "Медиа", Priority.Critical),
                    ("Система управления логистикой", "TMS с отслеживанием грузов в реальном времени", 950000m, DealStatus.Successful, 100, "Логистика", Priority.High),
                    ("Мобильный банкинг", "iOS и Android приложение для онлайн-банкинга", 1800000m, DealStatus.New, 45, "Финтех", Priority.Critical),
                    ("Платформа онлайн-обучения", "LMS с видео-лекциями, тестами и сертификатами", 650000m, DealStatus.InProgress, 70, "EdTech", Priority.Medium),
                    ("Маркетплейс органических продуктов", "B2C платформа с доставкой и подпиской", 1100000m, DealStatus.Failed, 0, "E-commerce", Priority.Medium)
                };

                var testExpenses = new[]
                {
                    ("Аренда офиса (квартал)", ExpenseType.Rent, 450000m, "Офис в бизнес-центре \"Башня на набережной\""),
                    ("Зарплата разработчикам", ExpenseType.Salary, 1200000m, "Выплата зарплаты за текущий месяц"),
                    ("Лицензии ПО", ExpenseType.Materials, 85000m, "JetBrains, GitHub Enterprise, Figma"),
                    ("Реклама в Яндекс.Директ", ExpenseType.Marketing, 120000m, "Кампания по привлечению клиентов"),
                    ("Налог УСН 6%", ExpenseType.Taxes, 180000m, "Упрощенная система налогообложения"),
                    ("Закупка серверов", ExpenseType.Equipment, 320000m, "Dell PowerEdge для собственного хостинга"),
                    ("Командировка в Москву", ExpenseType.Transport, 45000m, "Переговоры с крупным заказчиком")
                };

                int createdClients = 0, createdDeals = 0, createdExpenses = 0;

                // Создаем клиентов
                for (int i = 0; i < testClients.Length; i++)
                {
                    var (name, phone, email, notes) = testClients[i];
                    
                    // Проверяем на дубликаты
                    if (!_context.Clients.Any(c => c.Email == email || c.Phone == phone))
                    {
                        var client = new Client
                        {
                            Name = name,
                            Phone = phone,
                            Email = email,
                            Notes = notes,
                            CreatedAt = DateTime.Now.AddDays(-Random.Shared.Next(30, 180))
                        };
                        _context.Clients.Add(client);
                        createdClients++;
                    }
                }

                _context.SaveChanges();

                // Получаем ID созданных клиентов для связи со сделками
                var clientIds = _context.Clients.OrderByDescending(c => c.Id).Take(7).Select(c => c.Id).ToList();

                // Получаем созданных клиентов для связи с календарем
                var clientList = _context.Clients.OrderByDescending(c => c.Id).Take(7).ToList();

                // Создаем сделки
                for (int i = 0; i < testDeals.Length && i < clientIds.Count; i++)
                {
                    var (title, desc, amount, status, prob, category, priority) = testDeals[i];
                    
                    if (!_context.Deals.Any(d => d.Title == title))
                    {
                        var deadline = DateTime.Now.AddDays(Random.Shared.Next(14, 180));
                        var deal = new Deal
                        {
                            Title = title,
                            Description = desc,
                            Amount = amount,
                            Status = status,
                            Probability = prob,
                            Category = category,
                            Priority = priority,
                            ClientId = clientIds[i],
                            CreatedAt = DateTime.Now.AddDays(-Random.Shared.Next(7, 90)),
                            Deadline = deadline,
                            ClosedAt = status == DealStatus.Successful || status == DealStatus.Failed 
                                ? DateTime.Now.AddDays(-Random.Shared.Next(1, 30)) 
                                : null
                        };
                        _context.Deals.Add(deal);
                        _context.SaveChanges();
                        createdDeals++;
                    }
                }

                _context.SaveChanges();

                // Создаем расходы
                for (int i = 0; i < testExpenses.Length; i++)
                {
                    var (title, type, amount, notes) = testExpenses[i];
                    
                    // Проверяем на дубликаты по названию и дате
                    var expenseDate = DateTime.Now.AddDays(-Random.Shared.Next(0, 60));
                    if (!_context.Expenses.Any(ex => ex.Title == title && ex.Date.Date == expenseDate.Date))
                    {
                        var expense = new Expense
                        {
                            Title = title,
                            Type = type,
                            Amount = amount,
                            Category = type.ToString(),
                            Notes = notes,
                            Date = expenseDate
                        };
                        _context.Expenses.Add(expense);
                        createdExpenses++;
                    }
                }

                _context.SaveChanges();

                // Обновляем отображение
                LoadClients();
                LoadDealsData();
                LoadFinancesData();
                LoadDashboardData();

                MessageBox.Show(
                    $"✅ Тестовые данные успешно созданы!\n\n" +
                    $"📋 Клиентов: {createdClients}\n" +
                    $"💼 Сделок: {createdDeals}\n" +
                    $"💰 Расходов: {createdExpenses}",
                    "Успех",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при создании тестовых данных:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void ShowAIAssistant_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var aiWindow = new AIAssistantWindow();
                aiWindow.Owner = this;
                aiWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка запуска AI-ассистента: {ex.Message}", "Ошибка");
            }
        }

        private void AddExpenseButton_Click(object sender, RoutedEventArgs e)
        {
            AddExpenseWindow expenseWin = new AddExpenseWindow();
            expenseWin.Owner = this;
            // По умолчанию оно открывается как "Расход"

            if (expenseWin.ShowDialog() == true)
            {
                LoadFinancesData();
            }
        }

        private void DeleteExpense_Click(object sender, RoutedEventArgs e)
        {
            var transaction = (sender as Button).DataContext as FinanceTransaction;

            if (transaction == null || transaction.TypeName == "ДОХОД")
            {
                MessageBox.Show("Доходы из сделок удаляются в разделе Сделки.");
                return;
            }

            if (MessageBox.Show($"Удалить расход '{transaction.Title}'?", "Подтверждение",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                using (var db = MultiUserSecurityManager.CreateCompanyContext())
                {
                    var expense = db.Expenses.FirstOrDefault(ex =>
                        ex.Title == transaction.Title &&
                        ex.Amount == transaction.Amount);

                    if (expense != null)
                    {
                        db.Expenses.Remove(expense);
                        db.SaveChanges();
                        LoadFinancesData();
                    }
                }
            }
        }
        private void EditExpense_Click(object sender, RoutedEventArgs e)
        {
            var transaction = (sender as Button).DataContext as FinanceTransaction;

            if (transaction == null || transaction.TypeName == "ДОХОД")
            {
                MessageBox.Show("Для изменения дохода перейдите в раздел 'Сделки'.");
                return;
            }

            using (var db = MultiUserSecurityManager.CreateCompanyContext())
            {
                // Ищем расход в базе
                var expense = db.Expenses.FirstOrDefault(ex =>
                    ex.Title == transaction.Title &&
                    ex.Amount == transaction.Amount);

                if (expense != null)
                {
                    var editWin = new AddExpenseWindow();
                    editWin.Owner = this;
                    editWin.WindowHeader.Text = "Редактировать расход";

                    // Заполняем поля данными
                    editWin.TitleInput.Text = expense.Title;
                    editWin.AmountInput.Text = expense.Amount.ToString();
                    // CategoryInput.Text работает, если ComboBox IsEditable="True", 
                    // иначе нужно искать через Items или просто присвоить текст
                    editWin.CategoryInput.Text = expense.Category;

                    if (editWin.ShowDialog() == true)
                    {
                        // После успешного сохранения в окне (там создается новый расход),
                        // удаляем старую версию расхода здесь
                        db.Expenses.Remove(expense);
                        db.SaveChanges();
                        LoadFinancesData(); // Теперь этот метод будет найден, так как мы в MainWindow
                    }
                }
            }
        }

        #region Уведомления

        private void CheckAndUpdateNotifications()
        {
            try
            {
                // Проверяем сделки на напоминания
                NotificationManager.CheckDealReminders();

                // Обновляем счетчик уведомлений
                UpdateNotificationBadge();

                // Очищаем старые уведомления
                NotificationManager.CleanOldNotifications();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка обновления уведомлений: {ex.Message}");
            }
        }

        private void UpdateNotificationBadge()
        {
            int unreadCount = NotificationManager.GetUnreadCount();

            if (unreadCount > 0)
            {
                NotificationBadge.Visibility = Visibility.Visible;
                NotificationCountText.Text = unreadCount > 9 ? "9+" : unreadCount.ToString();

                // Анимация для бейджа
                var animation = new DoubleAnimation(0.5, 1, TimeSpan.FromSeconds(0.3));
                animation.AutoReverse = true;
                animation.RepeatBehavior = new RepeatBehavior(2);
                NotificationBadge.BeginAnimation(OpacityProperty, animation);
            }
            else
            {
                NotificationBadge.Visibility = Visibility.Collapsed;
            }
        }

        private void NotificationsButton_Click(object sender, RoutedEventArgs e)
        {
            var notificationsWindow = new NotificationsWindow();
            notificationsWindow.Owner = this;
            notificationsWindow.ShowDialog();

            // Обновляем счетчик после закрытия окна уведомлений
            UpdateNotificationBadge();
        }

        private void MessagesButton_Click(object sender, RoutedEventArgs e)
        {
            var messagesWindow = new MessagesWindow();
            messagesWindow.Owner = this;
            messagesWindow.ShowDialog();
        }

        // Обработчик клика по телефону
        private void Phone_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string phone && !string.IsNullOrEmpty(phone))
            {
                var messagesWindow = new MessagesWindow(phone: phone);
                messagesWindow.Owner = this;
                messagesWindow.ShowDialog();
            }
        }

        // Обработчик клика по email
        private void Email_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string email && !string.IsNullOrEmpty(email))
            {
                var messagesWindow = new MessagesWindow(email: email);
                messagesWindow.Owner = this;
                messagesWindow.ShowDialog();
            }
        }

        // Вызывайте этот метод при добавлении новой сделки
        private void CreateDealNotification(Deal deal)
        {
            NotificationManager.CreateDealCompletedNotification(deal);
            UpdateNotificationBadge();
        }

        // Вызывайте этот метод при добавлении нового клиента
        private void CreateClientNotification(Client client)
        {
            NotificationManager.CreateNewClientNotification(client);
            UpdateNotificationBadge();
        }

        #endregion



        // Вспомогательный метод
        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T)
                        yield return (T)child;

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                        yield return childOfChild;
                }
            }
        }
        private void SaveYandexSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var settings = AppSettings.Load();
                settings.YandexDiskToken = YandexTokenTextBox.Text.Trim();
                settings.EmployeeName = EmployeeNameTextBox.Text.Trim();
                settings.Save();
                MessageBox.Show("Настройки Яндекс.Диска сохранены!", "Успешно",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void LoadYandexSettings()
        {
            var settings = AppSettings.Load();
            YandexTokenTextBox.Text = settings.YandexDiskToken;
            EmployeeNameTextBox.Text = settings.EmployeeName;
        }

        private async void SendToYandexDisk_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var settings = AppSettings.Load();
                
                // Проверяем наличие токена
                if (string.IsNullOrWhiteSpace(settings.YandexDiskToken))
                {
                    MessageBox.Show("Сначала сохраните OAuth-токен Яндекс.Диска в настройках!", 
                                  "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Проверяем наличие ФИО сотрудника
                if (string.IsNullOrWhiteSpace(settings.EmployeeName))
                {
                    MessageBox.Show("Сначала укажите ФИО сотрудника в настройках!", 
                                  "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Подтверждение выгрузки
                var result = MessageBox.Show(
                    "Выполнить полную выгрузку всех данных на Яндекс.Диск?\n\n" +
                    "Будут отправлены:\n" +
                    "• Клиенты\n" +
                    "• Сделки\n" +
                    "• Расходы\n" +
                    "• Календарь событий\n" +
                    "• Статистика\n" +
                    "• Информация об аккаунте",
                    "Подтверждение", 
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                    return;

                // Показываем индикатор загрузки
                var originalContent = (sender as Button).Content;
                (sender as Button).Content = "Загрузка...";
                (sender as Button).IsEnabled = false;

                // Выполняем выгрузку
                var success = await YandexDiskDataExporter.ExportAllDataToYandexDisk(
                    settings.YandexDiskToken, 
                    settings.EmployeeName);

                // Восстанавливаем кнопку
                (sender as Button).Content = originalContent;
                (sender as Button).IsEnabled = true;

                if (success)
                {
                    MessageBox.Show("Все данные успешно отправлены на Яндекс.Диск!", 
                                  "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                // Восстанавливаем кнопку в случае ошибки
                (sender as Button).Content = "Отправить на Яндекс.Диск";
                (sender as Button).IsEnabled = true;

                MessageBox.Show($"Ошибка при отправке данных: {ex.Message}", 
                              "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        #region Управление окном
        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) this.DragMove();
        }
        private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void MaximizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        private void CloseButton_Click(object sender, RoutedEventArgs e) { _context?.Dispose(); Application.Current.Shutdown(); }

        // Обработчики изменения размера окна
        private void ResizeTopLeft_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (Width - e.HorizontalChange >= MinWidth) Width -= e.HorizontalChange;
            if (Height + e.VerticalChange >= MinHeight) Height += e.VerticalChange;
        }

        private void ResizeTop_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (Height + e.VerticalChange >= MinHeight) Height += e.VerticalChange;
        }

        private void ResizeTopRight_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (Width + e.HorizontalChange >= MinWidth) Width += e.HorizontalChange;
            if (Height + e.VerticalChange >= MinHeight) Height += e.VerticalChange;
        }

        private void ResizeLeft_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (Width - e.HorizontalChange >= MinWidth) Width -= e.HorizontalChange;
        }

        private void ResizeRight_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (Width + e.HorizontalChange >= MinWidth) Width += e.HorizontalChange;
        }

        private void ResizeBottomLeft_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (Width - e.HorizontalChange >= MinWidth) Width -= e.HorizontalChange;
            if (Height + e.VerticalChange >= MinHeight) Height += e.VerticalChange;
        }

        private void ResizeBottom_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (Height + e.VerticalChange >= MinHeight) Height += e.VerticalChange;
        }

        private void ResizeBottomRight_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (Width + e.HorizontalChange >= MinWidth) Width += e.HorizontalChange;
            if (Height + e.VerticalChange >= MinHeight) Height += e.VerticalChange;
        }
        #endregion

        #region Навигация
        private void MainMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // Добавили NavCalendar в массив
            var buttons = new[] { NavDashboard, NavClients, NavDeals, NavFinances, NavCalendar, NavReports, NavSettings };
            foreach (var button in buttons)
            {
                if (button != null) button.Style = (Style)FindResource("NavButton");
            }

            var clickedButton = sender as Button;
            if (clickedButton != null)
            {
                clickedButton.Style = (Style)FindResource("ActiveNavButton");
                string tag = clickedButton.Tag?.ToString() ?? "";

                // Для календаря открываем отдельное окно
                if (tag == "Calendar")
                {
                    OpenCalendarWindow();
                    return;
                }

                // Скрываем все страницы
                if (ClientsPage != null) ClientsPage.Visibility = Visibility.Collapsed;
                if (DashboardPage != null) DashboardPage.Visibility = Visibility.Collapsed;
                if (DealsPage != null) DealsPage.Visibility = Visibility.Collapsed;
                if (FinancesPage != null) FinancesPage.Visibility = Visibility.Collapsed;
                if (ReportsPage != null) ReportsPage.Visibility = Visibility.Collapsed;
                if (SettingsPage != null) SettingsPage.Visibility = Visibility.Collapsed;
                if (CalendarPage != null) CalendarPage.Visibility = Visibility.Collapsed;

                switch (tag)
                {
                    case "Main":
                        if (DashboardPage != null) DashboardPage.Visibility = Visibility.Visible;
                        LoadDashboardData();
                        break;
                    case "Clients":
                        if (ClientsPage != null) ClientsPage.Visibility = Visibility.Visible;
                        break;
                    case "Deals":
                        if (DealsPage != null) DealsPage.Visibility = Visibility.Visible;
                        LoadDealsData();
                        break;
                    case "Finances":
                        if (FinancesPage != null) FinancesPage.Visibility = Visibility.Visible;
                        LoadFinancesData();
                        break;
                    case "Reports":
                        if (ReportsPage != null) ReportsPage.Visibility = Visibility.Visible;
                        break;
                    case "Settings":
                        if (SettingsPage != null) SettingsPage.Visibility = Visibility.Visible;
                        LoadYandexSettings();
                        break;
                }
            }
        }
        #endregion

        #region Календарь
        private void OpenCalendarWindow()
        {
            try
            {
                var calendarWindow = new CalendarWindow();
                calendarWindow.Owner = this;
                calendarWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка открытия календаря: {ex.Message}", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Управление клиентами
        private void LoadClients()
        {
            try
            {
                InitializeDbContext();

                var clientsList = _context.Clients
                    .OrderByDescending(c => c.CreatedAt)
                    .ToList();

                _allClients = new ObservableCollection<Client>(clientsList);
                _filteredClients = new ObservableCollection<Client>(clientsList);

                ApplyPagination();
                UpdateStats();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки клиентов: {ex.Message}\n{ex.InnerException?.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RecreateDatabaseButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Пересоздать базу данных?\nВсе данные будут удалены!",
                "Подтверждение",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    _context?.Dispose();

                    string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    string dbPath = Path.Combine(appDataPath, "MyFirstCRM", "database.db");

                    if (File.Exists(dbPath))
                    {
                        File.Delete(dbPath);
                    }

                    InitializeDbContext();
                    LoadClients();
                    LoadDealsData();

                    MessageBox.Show("База данных успешно пересоздана!", "Успех");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка");
                }
            }
        }

        private void ApplyPagination()
        {
            if (_filteredClients == null) return;

            // Вычисляем общее количество записей
            int totalRecords = _filteredClients.Count;

            var clientsToShow = _filteredClients
                .Skip((_currentPage - 1) * _pageSize)
                .Take(_pageSize)
                  .Select((client, index) => new ClientViewModel
                  {
                      // Здесь используем ОБРАТНУЮ нумерацию: последняя запись = 1
                      DisplayId = totalRecords - ((_currentPage - 1) * _pageSize + index),
                      RealId = client.Id,
                      Name = client.Name ?? "",
                      Phone = client.Phone ?? "",
                      Email = client.Email ?? "",
                      CreatedAt = client.CreatedAt,
                      Notes = client.Notes ?? ""
                  })
                                .ToList();

            ClientsDataGrid.ItemsSource = clientsToShow;
            UpdatePaginationDisplay();
        }

        private void UpdatePaginationDisplay()
        {
            if (_filteredClients == null) return;

            _totalPages = (int)Math.Ceiling(_filteredClients.Count / (double)_pageSize);
            if (_totalPages == 0) _totalPages = 1;
            if (_currentPage > _totalPages) _currentPage = _totalPages;
            if (_currentPage < 1) _currentPage = 1;

            // Обновляем информацию о странице
            if (PageInfoText != null)
                PageInfoText.Text = $"Страница {_currentPage} из {_totalPages}";

            if (RecordsInfoText != null)
                RecordsInfoText.Text = $"{_filteredClients.Count} записей";

            // Обновляем кнопки навигации
            if (FirstPageButton != null) FirstPageButton.IsEnabled = _currentPage > 1;
            if (PrevPageButton != null) PrevPageButton.IsEnabled = _currentPage > 1;
            if (NextPageButton != null) NextPageButton.IsEnabled = _currentPage < _totalPages;
            if (LastPageButton != null) LastPageButton.IsEnabled = _currentPage < _totalPages;

            // Создаем кнопки страниц
            CreatePageButtons();
        }

        private void CreatePageButtons()
        {
            if (PageButtonsContainer == null) return;

            PageButtonsContainer.Items.Clear();

            int startPage = Math.Max(1, _currentPage - 2);
            int endPage = Math.Min(_totalPages, startPage + 4);

            // Корректируем startPage, если нужно
            if (endPage - startPage < 4)
            {
                startPage = Math.Max(1, endPage - 4);
            }

            for (int i = startPage; i <= endPage; i++)
            {
                var button = new Button
                {
                    Content = i.ToString(),
                    Tag = i,
                    Margin = new Thickness(2, 0, 2, 0),
                    Padding = new Thickness(10, 6, 10, 6),
                    FontSize = 12
                };

                if (i == _currentPage)
                {
                    button.Style = (Style)FindResource("ActivePageButton");
                }
                else
                {
                    button.Style = (Style)FindResource("PaginationButton");
                }

                button.Click += PageButton_Click;
                PageButtonsContainer.Items.Add(button);
            }
        }

        private void PageButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int pageNumber)
            {
                _currentPage = pageNumber;
                ApplyPagination();
            }
        }

        private void FirstPageButton_Click(object sender, RoutedEventArgs e)
        {
            _currentPage = 1;
            ApplyPagination();
        }

        private void PrevPageButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                ApplyPagination();
            }
        }

        private void NextPageButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage < _totalPages)
            {
                _currentPage++;
                ApplyPagination();
            }
        }

        private void LastPageButton_Click(object sender, RoutedEventArgs e)
        {
            _currentPage = _totalPages;
            ApplyPagination();
        }

        private void UpdateStats()
        {
            int totalClients = _allClients?.Count ?? 0;
            if (StatsSidebar != null)
                StatsSidebar.Text = totalClients.ToString();

            // Также обновляем статистику в верхней панели клиентов
            if (PageInfoText != null && _filteredClients != null)
            {
                PageInfoText.Text = $"Страница {_currentPage} из {_totalPages}";
                RecordsInfoText.Text = $"{_filteredClients.Count} записей";
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadClients();
            ClearSearch();
            UpdateStats();
        }
        #endregion

        #region Поиск
        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = SearchTextBox.Text.Trim();
            _isSearching = !string.IsNullOrWhiteSpace(searchText);

            if (ClearSearchInnerButton != null)
                ClearSearchInnerButton.Visibility = _isSearching ? Visibility.Visible : Visibility.Collapsed;

            if (string.IsNullOrWhiteSpace(searchText))
            {
                _filteredClients = new ObservableCollection<Client>(_allClients);
            }
            else
            {
                searchText = searchText.ToLower();
                var filtered = _allClients
                    .Where(c =>
                        (c.Name?.ToLower().Contains(searchText) ?? false) ||
                        (c.Phone?.ToLower().Contains(searchText) ?? false) ||
                        (c.Email?.ToLower().Contains(searchText) ?? false) ||
                        (c.Notes?.ToLower().Contains(searchText) ?? false))
                    .ToList();

                _filteredClients = new ObservableCollection<Client>(filtered);
            }

            _currentPage = 1;
            ApplyPagination();
        }

        private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                ClearSearch();
            }
        }

        private void ClearSearchInnerButton_Click(object sender, RoutedEventArgs e)
        {
            ClearSearch();
        }

        private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
        {
            ClearSearch();
        }

        private void ClearSearch()
        {
            if (SearchTextBox != null)
                SearchTextBox.Text = "";

            _isSearching = false;

            if (ClearSearchInnerButton != null)
                ClearSearchInnerButton.Visibility = Visibility.Collapsed;

            _filteredClients = new ObservableCollection<Client>(_allClients);
            _currentPage = 1;
            ApplyPagination();
        }
        #endregion

        #region Форма клиента
        private void AddClientButton_Click(object sender, RoutedEventArgs e)
        {
            OpenClientForm(0); // 0 = новый клиент
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int clientId)
            {
                OpenClientForm(clientId);
            }
        }

        private void OpenClientForm(int clientId)
        {
            _currentClientId = clientId;

            if (clientId > 0)
            {
                // Редактирование существующего клиента
                var client = _context.Clients
                    .Include(c => c.CreatedByUser)
                    .Include(c => c.UpdatedByUser)
                    .FirstOrDefault(c => c.Id == clientId);
                if (client != null)
                {
                    if (FormTitle != null) FormTitle.Text = "✏ Редактировать клиента";
                    if (NameTextBox != null) NameTextBox.Text = client.Name;
                    if (PhoneTextBox != null) PhoneTextBox.Text = client.Phone;
                    if (EmailTextBox != null) EmailTextBox.Text = client.Email;
                    if (NotesTextBox != null) NotesTextBox.Text = client.Notes;
                    if (AddButton != null) AddButton.Content = "Сохранить изменения";
                    
                    // Отображение информации о пользователе
                    if (ClientCreatedByInfo != null) 
                        ClientCreatedByInfo.Text = FormatCreatedByInfo(client.CreatedByUserId, client.CreatedAt);
                    if (ClientUpdatedInfo != null) 
                        ClientUpdatedInfo.Text = FormatUpdatedInfo(client.UpdatedByUserId, null);
                }
            }
            else
            {
                // Добавление нового клиента
                if (FormTitle != null) FormTitle.Text = "➕ Добавить клиента";
                if (NameTextBox != null) NameTextBox.Text = "";
                if (PhoneTextBox != null) PhoneTextBox.Text = "";
                if (EmailTextBox != null) EmailTextBox.Text = "";
                if (NotesTextBox != null) NotesTextBox.Text = "";
                if (AddButton != null) AddButton.Content = "Добавить клиента";
                
                // Очистка информации о пользователе для нового клиента
                if (ClientCreatedByInfo != null) ClientCreatedByInfo.Text = "";
                if (ClientUpdatedInfo != null) ClientUpdatedInfo.Text = "";
            }

            if (ClientFormOverlay != null)
                ClientFormOverlay.Visibility = Visibility.Visible;

            if (NameTextBox != null)
                NameTextBox.Focus();
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Валидация
                if (string.IsNullOrWhiteSpace(NameTextBox?.Text))
                {
                    MessageBox.Show("Поле 'ФИО' обязательно для заполнения!", "Внимание",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    NameTextBox?.Focus();
                    return;
                }

                Client savedClient;

                // ИСПОЛЬЗУЕМ СУЩЕСТВУЮЩИЙ КОНТЕКСТ вместо создания нового
                if (_currentClientId > 0)
                {
                    // Редактирование существующего клиента
                    var client = _context.Clients
                        .Include(c => c.CreatedByUser)
                        .Include(c => c.UpdatedByUser)
                        .FirstOrDefault(c => c.Id == _currentClientId);
                    if (client != null)
                    {
                        client.Name = NameTextBox?.Text?.Trim() ?? "";
                        client.Phone = PhoneTextBox?.Text?.Trim();
                        client.Email = EmailTextBox?.Text?.Trim();
                        client.Notes = NotesTextBox?.Text?.Trim();
                        // Явно устанавливаем UpdatedByUserId
                        client.UpdatedByUserId = MultiUserSecurityManager.CurrentUser.Id;

                        savedClient = client;
                    }
                    else
                    {
                        MessageBox.Show("Клиент не найден!", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
                else
                {
                    // Добавление нового клиента
                    var newClient = new Client
                    {
                        Name = NameTextBox?.Text?.Trim() ?? "",
                        Phone = PhoneTextBox?.Text?.Trim(),
                        Email = EmailTextBox?.Text?.Trim(),
                        Notes = NotesTextBox?.Text?.Trim(),
                        CreatedAt = DateTime.Now,
                        CreatedByUserId = MultiUserSecurityManager.CurrentUser.Id
                    };

                    _context.Clients.Add(newClient);
                    savedClient = newClient;
                }

                // СОХРАНЯЕМ ИЗМЕНЕНИЯ В БАЗЕ
                try
                {
                    _context.SaveChanges();
                    
                    // Обновляем информацию о пользователе в форме после сохранения
                    if (_currentClientId > 0)
                    {
                        // Обновляем отображение информации о пользователе
                        if (ClientCreatedByInfo != null) 
                            ClientCreatedByInfo.Text = FormatCreatedByInfo(savedClient.CreatedByUserId, savedClient.CreatedAt);
                        if (ClientUpdatedInfo != null) 
                            ClientUpdatedInfo.Text = FormatUpdatedInfo(savedClient.UpdatedByUserId, DateTime.Now);
                    }
                }
                catch (DbUpdateException dbEx)
                {
                    // Детальная информация об ошибке базы данных
                    string errorDetails = $"Ошибка базы данных: {dbEx.Message}";
                    if (dbEx.InnerException != null)
                    {
                        errorDetails += $"\nВнутренняя ошибка: {dbEx.InnerException.Message}";
                    }
                    throw new Exception(errorDetails, dbEx);
                }

                MessageBox.Show(_currentClientId > 0 ? "Клиент успешно обновлен!" : "Клиент успешно добавлен!",
                    "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                // Обновляем данные
                LoadClients();

                // Создаем уведомление о новом клиенте
                if (_currentClientId == 0)
                {
                    CreateClientNotification(savedClient);
                }

                // Закрываем форму
                CloseFormButton_Click(sender, e);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}\n\nПодробности:\n{ex.InnerException?.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

                // Логируем ошибку для отладки
                Debug.WriteLine($"Ошибка сохранения клиента: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Debug.WriteLine($"Внутренняя ошибка: {ex.InnerException.Message}");
                }
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int clientId)
            {
                var result = MessageBox.Show("Вы уверены, что хотите удалить этого клиента?\nЭто действие нельзя отменить.",
                    "Подтверждение удаления",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        // Удаление через отдельный контекст
                        using (var deleteContext = MultiUserSecurityManager.CreateCompanyContext())
                        {
                            var client = deleteContext.Clients.Find(clientId);
                            if (client != null)
                            {
                                deleteContext.Clients.Remove(client);
                                deleteContext.SaveChanges();

                                // Обновляем основной контекст
                                InitializeDbContext();
                                LoadClients();
                                UpdateStats();

                                MessageBox.Show("Клиент успешно удален!", "Успех",
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка удаления: {ex.Message}", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void CloseFormButton_Click(object sender, RoutedEventArgs e) => ClientFormOverlay.Visibility = Visibility.Collapsed;
        #endregion

        #region Импорт из Excel
        private void ImportExcelButton_Click(object sender, RoutedEventArgs e)
        {
            ImportExcelOverlay.Visibility = Visibility.Visible;
            ImportLogText.Text = "Выберите файл для начала импорта...";
            ImportFilePathText.Text = "Файл не выбран";
            StartImportButton.IsEnabled = false;
        }

        private void BrowseFileButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFileDialog();
                dialog.Filter = "Excel Files|*.xlsx;*.xls";
                dialog.Title = "Выберите Excel файл с клиентами";

                if (dialog.ShowDialog() == true)
                {
                    ImportFilePathText.Text = dialog.FileName;
                    StartImportButton.IsEnabled = true;
                    ImportLogText.Text = "Файл выбран. Нажмите 'Начать импорт'.";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка выбора файла: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StartImportButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(ImportFilePathText.Text) ||
                ImportFilePathText.Text == "Файл не выбран")
            {
                MessageBox.Show("Сначала выберите файл!", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                StartImportButton.IsEnabled = false;
                ImportLogText.Text = "Начинаем импорт...\n";

                // Используем отдельный контекст для импорта
                using (var importContext = MultiUserSecurityManager.CreateCompanyContext())
                {
                    // Читаем Excel файл
                    var fileInfo = new FileInfo(ImportFilePathText.Text);
                    using (var package = new ExcelPackage(fileInfo))
                    {
                        var worksheet = package.Workbook.Worksheets[0]; // Первый лист
                        int rowCount = worksheet.Dimension?.Rows ?? 0;
                        int colCount = worksheet.Dimension?.Columns ?? 0;

                        ImportLogText.Text += $"Найдено строк: {rowCount}, колонок: {colCount}\n";

                        if (rowCount <= 1) // Только заголовок
                        {
                            ImportLogText.Text += "Файл пуст или содержит только заголовки!\n";
                            return;
                        }

                        // Ищем строку с заголовками (пропускаем пустые строки и заголовки экспорта)
                        int headerRow = FindHeaderRow(worksheet, rowCount);
                        if (headerRow == -1)
                        {
                            ImportLogText.Text += "Не найдена строка с заголовками!\n";
                            return;
                        }

                        ImportLogText.Text += $"Строка заголовков найдена: {headerRow}\n";

                        // Ищем колонки по заголовкам
                        int nameCol = -1, phoneCol = -1, emailCol = -1, notesCol = -1;

                        for (int col = 1; col <= colCount; col++)
                        {
                            var header = worksheet.Cells[headerRow, col].Text?.Trim().ToLower() ?? "";

                            // Расширенный поиск заголовков с учетом формата экспорта
                            if (header.Contains("фио") || header.Contains("имя") || header.Contains("название") || 
                                header == "фио" || header == "name")
                                nameCol = col;
                            else if (header.Contains("тел") || header.Contains("phone") || 
                                    header == "телефон" || header == "phone")
                                phoneCol = col;
                            else if (header.Contains("email") || header.Contains("почта") || 
                                    header.Contains("e-mail") || header == "email")
                                emailCol = col;
                            else if (header.Contains("примеч") || header.Contains("заметк") || header.Contains("note") ||
                                    header == "примечания" || header == "notes")
                                notesCol = col;
                        }

                        ImportLogText.Text += $"Колонки найдены: ФИО={nameCol}, Телефон={phoneCol}, Email={emailCol}, Примечания={notesCol}\n";

                        if (nameCol == -1)
                        {
                            ImportLogText.Text += "ОШИБКА: Не найдена колонка с ФИО клиента!\n";
                            return;
                        }

                        int added = 0, updated = 0, skipped = 0, errors = 0;

                        // Читаем данные построчно, начиная со строки после заголовков
                        for (int row = headerRow + 1; row <= rowCount; row++)
                        {
                            try
                            {
                                // Проверяем, что строка не пустая
                                bool rowIsEmpty = true;
                                for (int col = 1; col <= colCount; col++)
                                {
                                    if (!string.IsNullOrWhiteSpace(worksheet.Cells[row, col].Text))
                                    {
                                        rowIsEmpty = false;
                                        break;
                                    }
                                }

                                if (rowIsEmpty)
                                {
                                    ImportLogText.Text += $"Строка {row}: пропущена (пустая строка)\n";
                                    skipped++;
                                    continue;
                                }

                                string name = nameCol > 0 ? worksheet.Cells[row, nameCol].Text?.Trim() ?? "" : "";
                                string phone = phoneCol > 0 ? worksheet.Cells[row, phoneCol].Text?.Trim() ?? "" : "";
                                string email = emailCol > 0 ? worksheet.Cells[row, emailCol].Text?.Trim() ?? "" : "";
                                string notes = notesCol > 0 ? worksheet.Cells[row, notesCol].Text?.Trim() ?? "" : "";

                                // Дополнительная очистка данных
                                name = CleanImportedText(name) ?? "";
                                phone = CleanImportedText(phone) ?? "";
                                email = CleanImportedText(email) ?? "";
                                notes = CleanImportedText(notes) ?? "";

                                // Проверяем обязательное поле
                                if (string.IsNullOrWhiteSpace(name))
                                {
                                    ImportLogText.Text += $"Строка {row}: пропущена (нет ФИО)\n";
                                    skipped++;
                                    continue;
                                }

                                var newClient = new Client
                                {
                                    Name = name,
                                    Phone = phone,
                                    Email = email,
                                    Notes = notes,
                                    CreatedAt = DateTime.Now
                                };

                                bool skipDuplicates = SkipDuplicatesCheckBox.IsChecked ?? true;
                                bool updateExisting = UpdateExistingCheckBox.IsChecked ?? true;

                                // Улучшенная проверка на дубликаты
                                Client? existingClient = null;
                                if (skipDuplicates || updateExisting)
                                {
                                    existingClient = importContext.Clients.FirstOrDefault(c =>
                                        (!string.IsNullOrEmpty(c.Phone) && !string.IsNullOrEmpty(phone) && 
                                         c.Phone.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "") == 
                                         phone.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "")) ||
                                        (!string.IsNullOrEmpty(c.Email) && !string.IsNullOrEmpty(email) &&
                                         c.Email.ToLower().Trim() == email.ToLower().Trim()));
                                }

                                if (existingClient != null)
                                {
                                    if (skipDuplicates && !updateExisting)
                                    {
                                        ImportLogText.Text += $"Строка {row}: пропущена (дубликат - {name})\n";
                                        skipped++;
                                        continue;
                                    }
                                    else if (updateExisting)
                                    {
                                        // Обновляем существующего клиента
                                        existingClient.Name = name;
                                        existingClient.Phone = phone;
                                        existingClient.Email = email;
                                        existingClient.Notes = notes;
                                        updated++;
                                        ImportLogText.Text += $"Строка {row}: обновлен клиент - {name}\n";
                                    }
                                }
                                else
                                {
                                    // Добавляем нового клиента
                                    importContext.Clients.Add(newClient);
                                    added++;
                                    ImportLogText.Text += $"Строка {row}: добавлен клиент - {name}\n";
                                }

                                // Сохраняем каждые 10 записей
                                if ((added + updated) % 10 == 0)
                                {
                                    importContext.SaveChanges();
                                }
                            }
                            catch (Exception ex)
                            {
                                errors++;
                                ImportLogText.Text += $"Строка {row}: ошибка - {ex.Message}\n";
                            }
                        }

                        // Сохраняем оставшиеся изменения
                        importContext.SaveChanges();

                        ImportLogText.Text += $"\n✅ ИМПОРТ ЗАВЕРШЕН!\n";
                        ImportLogText.Text += $"Добавлено: {added}\n";
                        ImportLogText.Text += $"Обновлено: {updated}\n";
                        ImportLogText.Text += $"Пропущено: {skipped}\n";
                        ImportLogText.Text += $"Ошибок: {errors}\n";
                    }
                }

                // Обновляем данные в таблице
                InitializeDbContext(); // Обновляем основной контекст
                LoadClients();
                UpdateStats();
            }
            catch (Exception ex)
            {
                ImportLogText.Text += $"\n❌ ОШИБКА ИМПОРТА: {ex.Message}\n";
                MessageBox.Show($"Ошибка импорта: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                StartImportButton.IsEnabled = true;
            }
        }

        private int FindHeaderRow(ExcelWorksheet worksheet, int maxRow)
        {
            // Ищем строку с заголовками, проверяя первые 10 строк
            for (int row = 1; row <= Math.Min(10, maxRow); row++)
            {
                bool hasHeader = false;
                for (int col = 1; col <= worksheet.Dimension.Columns; col++)
                {
                    var cellValue = worksheet.Cells[row, col].Text?.Trim().ToLower() ?? "";
                    if (cellValue.Contains("фио") || cellValue.Contains("имя") || cellValue.Contains("название") ||
                        cellValue.Contains("тел") || cellValue.Contains("phone") || cellValue.Contains("email") ||
                        cellValue.Contains("почта") || cellValue.Contains("примеч") || cellValue.Contains("заметк"))
                    {
                        hasHeader = true;
                        break;
                    }
                }
                if (hasHeader)
                    return row;
            }
            return -1; // Не найдено
        }

        private string CleanImportedText(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return text ?? "";

            // Удаляем лишние пробелы и спецсимволы
            text = text.Trim();
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " "); // Заменяем множественные пробелы на один
            text = text.Replace("\n", " ").Replace("\r", " "); // Заменяем переносы строк на пробелы
            
            return text;
        }

        private void CloseImportButton_Click(object sender, RoutedEventArgs e)
        {
            ImportExcelOverlay.Visibility = Visibility.Collapsed;
        }
        #endregion

        #region Экспорт в Excel
        private void ExportExcelButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog();
                dialog.Filter = "Excel Files|*.xlsx";
                dialog.Title = "Экспорт клиентов в Excel";
                dialog.FileName = $"Клиенты_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

                if (dialog.ShowDialog() == true)
                {
                    // Показываем прогресс
                    var exportWindow = new ExportProgressWindow();
                    exportWindow.Show();

                    // Экспортируем в отдельном потоке
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        try
                        {
                            ExcelExporter.ExportClientsToExcel(_allClients.ToList(), dialog.FileName);

                            // Обновляем UI из основного потока
                            Dispatcher.Invoke(() =>
                            {
                                exportWindow.Close();
                                MessageBox.Show($"Экспорт успешно завершен!\nФайл сохранен: {dialog.FileName}",
                                    "Экспорт завершен",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information);
                            });
                        }
                        catch (Exception ex)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                exportWindow.Close();
                                MessageBox.Show($"Ошибка экспорта: {ex.Message}",
                                    "Ошибка",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                            });
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region Дашборд
        private void LoadDashboardData()
        {
            try
            {
                // Обновляем контекст перед загрузкой
                InitializeDbContext();

                // Загружаем статистику клиентов
                int totalClients = _context.Clients.Count();
                DashboardTotalClients.Text = totalClients.ToString();

                // Загружаем статистику сделок
                var dealsList = _context.Deals
                    .Include(d => d.Client)
                    .OrderByDescending(d => d.CreatedAt)
                    .ToList();

                _allDeals = new ObservableCollection<Deal>(dealsList);

                int totalDeals = _allDeals.Count;
                DashboardTotalDeals.Text = totalDeals.ToString();

                // Загружаем объем продаж
                decimal totalAmount = _allDeals
                    .Where(d => d.Status == DealStatus.Successful)
                    .Sum(d => d.Amount);
                DashboardTotalAmount.Text = totalAmount.ToString("N0") + " ₽";

                // Рассчитываем конверсию (успешные / все сделки)
                int successfulDeals = _allDeals.Count(d => d.Status == DealStatus.Successful);
                double conversionRate = totalDeals > 0 ?
                    (double)successfulDeals / totalDeals * 100 : 0;
                DashboardConversion.Text = $"{conversionRate:F1}%";

                // Финансовые KPI (расходы/прибыль/рентабельность) — берём расходы из раздела "Финансы"
                decimal totalExpenses = 0;
                try
                {
                    // SQLite не всегда умеет SUM по decimal на стороне БД — суммируем на клиенте
                    totalExpenses = _context.Expenses
                        .Select(e => e.Amount)
                        .ToList()
                        .Sum();
                }
                catch
                {
                    totalExpenses = 0;
                }

                decimal netProfit = totalAmount - totalExpenses;
                decimal profitMargin = totalAmount > 0 ? (netProfit / totalAmount) * 100 : 0;

                if (DashboardTotalExpenses != null)
                    DashboardTotalExpenses.Text = $"{totalExpenses:N0} ₽";
                if (DashboardNetProfit != null)
                    DashboardNetProfit.Text = $"{netProfit:N0} ₽";
                if (DashboardProfitMargin != null)
                    DashboardProfitMargin.Text = $"{profitMargin:F1}%";

                if (DashboardNetProfit != null)
                {
                    DashboardNetProfit.Foreground = netProfit >= 0
                        ? (SolidColorBrush)FindResource("SuccessColor")
                        : (SolidColorBrush)FindResource("DangerColor");
                }

                // Заполняем воронку продаж
                var funnelNew = _allDeals.Count(d => d.Status == DealStatus.New);
                var funnelInProgress = _allDeals.Count(d => d.Status == DealStatus.InProgress);
                var funnelSuccessful = _allDeals.Count(d => d.Status == DealStatus.Successful);
                var funnelFailed = _allDeals.Count(d => d.Status == DealStatus.Failed);

                FunnelNew.Text = funnelNew.ToString();
                FunnelInProgress.Text = funnelInProgress.ToString();
                FunnelSuccessful.Text = funnelSuccessful.ToString();
                FunnelFailed.Text = funnelFailed.ToString();

                // Рассчитываем конверсию между этапами (исправленные формулы)
                // Конверсия из новых в работу: из новых / новые * 100
                double newToProgress = funnelNew > 0 ?
                    (double)funnelInProgress / funnelNew * 100 : 0;

                // Конверсия из работы в успешные: успешные / в работе * 100
                double progressToSuccess = funnelInProgress > 0 ?
                    (double)funnelSuccessful / funnelInProgress * 100 : 0;

                // Общая конверсия: успешные / все сделки * 100
                double totalConversion = totalDeals > 0 ?
                    (double)funnelSuccessful / totalDeals * 100 : 0;

                FunnelNewToProgress.Text = $"{newToProgress:F1}%";
                FunnelProgressToSuccess.Text = $"{progressToSuccess:F1}%";
                FunnelTotalConversion.Text = $"{totalConversion:F1}%";

                // Устанавливаем значения прогресс-баров
                FunnelProgress1.Value = newToProgress;
                FunnelProgress2.Value = progressToSuccess;
                FunnelProgress3.Value = totalConversion;

                // Загружаем последние сделки (5 последних)
                var recentDeals = _allDeals
                    .Take(5)
                    .ToList();
                DashboardRecentDealsGrid.ItemsSource = recentDeals;

                // Загружаем последних клиентов (5 последних)
                var recentClients = _context.Clients
                    .OrderByDescending(c => c.CreatedAt)
                    .Take(5)
                    .ToList();
                DashboardRecentClientsGrid.ItemsSource = recentClients;

                // Обновляем боковую панель
                UpdateStats();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных дашборда: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }
        #endregion

        #region Управление сделками
        private void LoadDealsData()
        {
            try
            {
                // Обновляем контекст перед загрузкой
                InitializeDbContext();

                // Загружаем из базы с сортировкой по дате создания (новые сверху)
                var dealsList = _context.Deals
                    .Include(d => d.Client)
                    .OrderByDescending(d => d.CreatedAt)
                    .ToList();

                _allDeals = new ObservableCollection<Deal>(dealsList);
                _filteredDeals = new ObservableCollection<Deal>(dealsList);

                _currentDealPage = 1; // Сбрасываем на первую страницу
                ApplyDealsPagination();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки сделок: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // МЕТОД ЗАГРУЗКИ ДАННЫХ ДЛЯ ВКЛАДКИ ФИНАНСЫ
        private void LoadFinancesData()
        {
            try
            {
                using (var db = MultiUserSecurityManager.CreateCompanyContext())
                {
                    // 1. Сбор данных
                    var incomes = db.Deals
                        .Where(d => d.Status == DealStatus.Successful)
                        .ToList()
                        .Select(d => new FinanceTransaction
                        {
                            Date = d.ClosedAt ?? d.CreatedAt,
                            Title = $"Сделка: {d.Title}",
                            Category = string.IsNullOrWhiteSpace(d.Category) ? "Продажи" : d.Category!,
                            Amount = d.Amount,
                            TypeName = "ДОХОД",
                            TypeColor = "#10B981", // Тот самый зеленый
                            AmountColor = "#059669"
                        }).ToList();

                    var expenses = db.Expenses
                        .ToList()
                        .Select(e => new FinanceTransaction
                        {
                            Date = e.Date,
                            Title = e.Title,
                            Category = e.Category,
                            Amount = e.Amount,
                            TypeName = "РАСХОД",
                            TypeColor = "#F43F5E", // Тот самый красный
                            AmountColor = "#E11D48"
                        }).ToList();

                    _allFinanceTransactions = incomes.Concat(expenses).OrderByDescending(t => t.Date).ToList();
                    FinancesLedgerGrid.ItemsSource = _allFinanceTransactions;

                    // 2. Математика
                    decimal totalInc = incomes.Sum(i => i.Amount);
                    decimal totalExp = expenses.Sum(e => e.Amount);
                    decimal netProf = totalInc - totalExp;

                    // Расчет рентабельности БЕЗ повторного объявления переменных
                    decimal finalMargin = (totalInc > 0) ? (netProf / totalInc) * 100 : 0;

                    // 3. Обновление UI (без ExpenseRatioBar, так как мы его убрали из XAML)
                    FinanceTotalIncome.Text = $"{totalInc:N0} ₽";
                    FinanceTotalExpense.Text = $"{totalExp:N0} ₽";
                    FinanceNetProfit.Text = $"{netProf:N0} ₽";
                    ProfitMarginLabel.Text = $"{finalMargin:F1}%";

                    // Цвет для рентабельности
                    ProfitMarginLabel.Foreground = finalMargin >= 0 ?
                        (SolidColorBrush)FindResource("SuccessColor") :
                        (SolidColorBrush)FindResource("DangerColor");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка обновления данных: " + ex.Message);
            }
        }

        // ОБРАБОТЧИКИ КНОПОК
        private void RefreshFinancesButton_Click(object sender, RoutedEventArgs e)
        {
            LoadFinancesData();
            // Визуальное подтверждение
            
        }

        private void DeleteExpenseButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int id)
            {
                var result = MessageBox.Show("Удалить запись о расходе?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    using (var db = MultiUserSecurityManager.CreateCompanyContext())
                    {
                        var expense = db.Expenses.Find(id);
                        if (expense != null)
                        {
                            db.Expenses.Remove(expense);
                            db.SaveChanges();
                            LoadFinancesData(); // Обновляем таблицу
                        }
                    }
                }
            }
        }

        private void ApplyDealsPagination()
        {
            if (_filteredDeals == null) return;

            var dealsToShow = _filteredDeals
                .Skip((_currentDealPage - 1) * _dealsPageSize)
                .Take(_dealsPageSize)
                .ToList();

            DealsDataGrid.ItemsSource = dealsToShow;
            UpdateDealsPaginationDisplay();
        }
        // Глобальный список для хранения всех транзакций (нужен для фильтрации)
        private List<FinanceTransaction> _allFinanceTransactions = new List<FinanceTransaction>();
        private ObservableCollection<CalendarEvent> _calendarEvents = new();
        private DateTime _calendarSelectedDate = DateTime.Today;

        // 1. Обработка поиска (фильтрация в реальном времени)
        private void FinanceSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_allFinanceTransactions == null) return;

            string filter = FinanceSearchBox.Text.ToLower();
            var filteredList = _allFinanceTransactions.Where(t =>
                (t.Title?.ToLower().Contains(filter) ?? false) ||
                (t.Category?.ToLower().Contains(filter) ?? false) ||
                (t.TypeName?.ToLower().Contains(filter) ?? false)
            ).ToList();

            FinancesLedgerGrid.ItemsSource = filteredList;
        }

        // 2. Кнопка Добавить Доход
        private void AddIncomeButton_Click(object sender, RoutedEventArgs e)
        {
            AddExpenseWindow incomeWin = new AddExpenseWindow();
            incomeWin.Owner = this;
            incomeWin.SetAsIncome(); // Превращаем окно расхода в окно дохода!

            if (incomeWin.ShowDialog() == true)
            {
                LoadFinancesData(); // Обновляем таблицу после закрытия
            }
        }

        private void UpdateDealsPaginationDisplay()
        {
            if (_filteredDeals == null) return;

            _dealsTotalPages = (int)Math.Ceiling(_filteredDeals.Count / (double)_dealsPageSize);
            if (_dealsTotalPages == 0) _dealsTotalPages = 1;
            if (_currentDealPage > _dealsTotalPages) _currentDealPage = _dealsTotalPages;
            if (_currentDealPage < 1) _currentDealPage = 1;

            // Обновляем информацию о странице
            DealsPageInfoText.Text = $"Страница {_currentDealPage} из {_dealsTotalPages}";
            DealsRecordsInfoText.Text = $"{_filteredDeals.Count} сделок";

            // Обновляем кнопки навигации
            DealsFirstPageButton.IsEnabled = _currentDealPage > 1;
            DealsPrevPageButton.IsEnabled = _currentDealPage > 1;
            DealsNextPageButton.IsEnabled = _currentDealPage < _dealsTotalPages;
            DealsLastPageButton.IsEnabled = _currentDealPage < _dealsTotalPages;

            // Создаем кнопки страниц
            CreateDealsPageButtons();
        }

        private void CreateDealsPageButtons()
        {
            DealsPageButtonsContainer.Items.Clear();

            int startPage = Math.Max(1, _currentDealPage - 2);
            int endPage = Math.Min(_dealsTotalPages, startPage + 4);

            // Корректируем startPage, если нужно
            if (endPage - startPage < 4)
            {
                startPage = Math.Max(1, endPage - 4);
            }

            for (int i = startPage; i <= endPage; i++)
            {
                var button = new Button
                {
                    Content = i.ToString(),
                    Tag = i,
                    Margin = new Thickness(2, 0, 2, 0),
                    Padding = new Thickness(10, 6, 10, 6),
                    FontSize = 12
                };

                if (i == _currentDealPage)
                {
                    button.Style = (Style)FindResource("ActivePageButton");
                }
                else
                {
                    button.Style = (Style)FindResource("PaginationButton");
                }

                button.Click += DealsPageButton_Click;
                DealsPageButtonsContainer.Items.Add(button);
            }
        }

        private void DealsPageButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int pageNumber)
            {
                _currentDealPage = pageNumber;
                ApplyDealsPagination();
            }
        }

        private void DealsFirstPageButton_Click(object sender, RoutedEventArgs e)
        {
            _currentDealPage = 1;
            ApplyDealsPagination();
        }

        private void DealsPrevPageButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentDealPage > 1)
            {
                _currentDealPage--;
                ApplyDealsPagination();
            }
        }

        private void DealsNextPageButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentDealPage < _dealsTotalPages)
            {
                _currentDealPage++;
                ApplyDealsPagination();
            }
        }

        private void DealsLastPageButton_Click(object sender, RoutedEventArgs e)
        {
            _currentDealPage = _dealsTotalPages;
            ApplyDealsPagination();
        }

        private void AddDealButton_Click(object sender, RoutedEventArgs e)
        {
            OpenDealForm(0); // 0 = новая сделка
        }

        private void RefreshDealsButton_Click(object sender, RoutedEventArgs e)
        {
            LoadDealsData();
        }

        private void EditDealButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int dealId)
            {
                OpenDealForm(dealId);
            }
        }

        private void DeleteDealButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int dealId)
            {
                var result = MessageBox.Show("Удалить эту сделку?", "Подтверждение",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        // Удаление через отдельный контекст
                        using (var deleteContext = MultiUserSecurityManager.CreateCompanyContext())
                        {
                            var deal = deleteContext.Deals.Find(dealId);
                            if (deal != null)
                            {
                                deleteContext.Deals.Remove(deal);
                                deleteContext.SaveChanges();

                                // Обновляем основной контекст
                                InitializeDbContext();
                                LoadDealsData();
                                LoadDashboardData();

                                MessageBox.Show("Сделка удалена!", "Успех",
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка удаления: {ex.Message}", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void DealFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyDealsFilters();
        }

        private void DealSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyDealsFilters();
        }

        private void ApplyDealsFilters()
        {
            if (_allDeals == null) return;

            var statusFilter = (DealStatusFilter.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            var priorityFilter = (DealPriorityFilter.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            var searchText = DealSearchTextBox?.Text?.Trim().ToLower() ?? "";

            var filtered = _allDeals.AsQueryable();

            // Фильтр по статусу
            if (statusFilter != "All" && !string.IsNullOrEmpty(statusFilter))
            {
                var status = (DealStatus)Enum.Parse(typeof(DealStatus), statusFilter);
                filtered = filtered.Where(d => d.Status == status);
            }

            // Фильтр по приоритету
            if (priorityFilter != "All" && !string.IsNullOrEmpty(priorityFilter))
            {
                var priority = (Priority)Enum.Parse(typeof(Priority), priorityFilter);
                filtered = filtered.Where(d => d.Priority == priority);
            }

            // Поиск по названию
            if (!string.IsNullOrEmpty(searchText))
            {
                filtered = filtered.Where(d =>
                    d.Title.ToLower().Contains(searchText) ||
                    (d.Description != null && d.Description.ToLower().Contains(searchText)) ||
                    (d.Category != null && d.Category.ToLower().Contains(searchText)) ||
                    (d.Client != null && d.Client.Name.ToLower().Contains(searchText)));
            }

            var filteredList = filtered.ToList();
            _filteredDeals = new ObservableCollection<Deal>(filteredList);
            _currentDealPage = 1;
            ApplyDealsPagination();
        }

        private void DealsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DealsDataGrid.SelectedItem is Deal selectedDeal)
            {
                MessageBox.Show($"📋 Детали сделки:\n\n" +
                               $"🏷 Название: {selectedDeal.Title}\n" +
                               $"👥 Клиент: {selectedDeal.Client?.Name}\n" +
                               $"💰 Сумма: {selectedDeal.Amount:N0} ₽\n" +
                               $"📊 Статус: {selectedDeal.Status}\n" +
                               $"🎯 Приоритет: {selectedDeal.Priority}\n" +
                               $"📈 Вероятность: {selectedDeal.Probability}%\n" +
                               $"📅 Срок: {selectedDeal.Deadline:dd.MM.yyyy}\n" +
                               $"🏷 Категория: {selectedDeal.Category ?? "Не указана"}\n" +
                               $"📝 Описание: {selectedDeal.Description ?? "Нет описания"}",
                               "Детали сделки",
                               MessageBoxButton.OK,
                               MessageBoxImage.Information);
            }
        }

        private void OpenDealForm(int dealId)
        {
            _currentDealId = dealId;

            // Обновляем контекст перед загрузкой клиентов
            InitializeDbContext();

            // Загружаем клиентов в ComboBox
            DealClientComboBox.ItemsSource = _context.Clients.OrderBy(c => c.Name).ToList();

            if (dealId > 0)
            {
                // Редактирование существующей сделки
                var deal = _context.Deals
                    .Include(d => d.Client)
                    .Include(d => d.CreatedByUser)
                    .Include(d => d.UpdatedByUser)
                    .Include(d => d.AssignedToUser)
                    .FirstOrDefault(d => d.Id == dealId);

                if (deal != null)
                {
                    DealFormTitle.Text = "✏ Редактировать сделку";
                    DealTitleTextBox.Text = deal.Title;
                    DealClientComboBox.SelectedItem = deal.Client;
                    DealAmountTextBox.Text = deal.Amount.ToString();
                    DealProbabilitySlider.Value = deal.Probability;
                    DealProbabilityText.Text = $"{deal.Probability}%";
                    DealCategoryTextBox.Text = deal.Category ?? "";
                    DealDescriptionTextBox.Text = deal.Description ?? "";
                    DealDeadlineDatePicker.SelectedDate = deal.Deadline;
                    DealCreatedDatePicker.SelectedDate = deal.CreatedAt;

                    // Устанавливаем статус
                    foreach (ComboBoxItem item in DealStatusComboBox.Items)
                    {
                        if (item.Tag.ToString() == deal.Status.ToString())
                        {
                            DealStatusComboBox.SelectedItem = item;
                            break;
                        }
                    }

                    // Устанавливаем приоритет
                    foreach (ComboBoxItem item in DealPriorityComboBox.Items)
                    {
                        if (item.Tag.ToString() == deal.Priority.ToString())
                        {
                            DealPriorityComboBox.SelectedItem = item;
                            break;
                        }
                    }

                    SaveDealButton.Content = "Сохранить изменения";
                    
                    // Отображение информации о пользователе
                    if (DealCreatedByInfo != null) 
                        DealCreatedByInfo.Text = FormatCreatedByInfo(deal.CreatedByUserId, deal.CreatedAt);
                    if (DealUpdatedInfo != null) 
                        DealUpdatedInfo.Text = FormatUpdatedInfo(deal.UpdatedByUserId, null);
                    if (DealAssignedToInfo != null) 
                        DealAssignedToInfo.Text = FormatAssignedToInfo(deal.AssignedToUserId);
                }
            }
            else
            {
                // Добавление новой сделки
                DealFormTitle.Text = "➕ Новая сделка";
                DealTitleTextBox.Text = "";
                DealClientComboBox.SelectedIndex = -1;
                DealAmountTextBox.Text = "10000";
                DealProbabilitySlider.Value = 50;
                DealProbabilityText.Text = "50%";
                DealCategoryTextBox.Text = "";
                DealDescriptionTextBox.Text = "";
                DealDeadlineDatePicker.SelectedDate = DateTime.Now.AddDays(30);
                DealCreatedDatePicker.SelectedDate = DateTime.Now;
                DealStatusComboBox.SelectedIndex = 0;
                DealPriorityComboBox.SelectedIndex = 1;
                SaveDealButton.Content = "Добавить сделку";
                
                // Очистка информации о пользователе для новой сделки
                if (DealCreatedByInfo != null) DealCreatedByInfo.Text = "";
                if (DealUpdatedInfo != null) DealUpdatedInfo.Text = "";
                if (DealAssignedToInfo != null) DealAssignedToInfo.Text = "";
            }

            DealFormOverlay.Visibility = Visibility.Visible;
            DealTitleTextBox.Focus();
        }

        private void DealProbabilitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (DealProbabilityText != null)
                DealProbabilityText.Text = $"{(int)DealProbabilitySlider.Value}%";
        }

        private void OpenAdvancedReports_Click(object sender, RoutedEventArgs e)
        {
            var reportsWindow = new AdvancedReportsWindow();
            reportsWindow.Owner = this;
            reportsWindow.ShowDialog();
        }

        private void SaveDealButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Валидация
                if (string.IsNullOrWhiteSpace(DealTitleTextBox.Text))
                {
                    MessageBox.Show("Название сделки обязательно!", "Внимание",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    DealTitleTextBox.Focus();
                    return;
                }

                if (DealClientComboBox.SelectedItem == null)
                {
                    MessageBox.Show("Выберите клиента!", "Внимание",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    DealClientComboBox.Focus();
                    return;
                }

                if (!decimal.TryParse(DealAmountTextBox.Text, out decimal amount) || amount <= 0)
                {
                    MessageBox.Show("Введите корректную сумму!", "Внимание",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    DealAmountTextBox.Focus();
                    return;
                }

                if (DealDeadlineDatePicker.SelectedDate == null)
                {
                    MessageBox.Show("Выберите срок сделки!", "Внимание",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    DealDeadlineDatePicker.Focus();
                    return;
                }

                Deal savedDeal;
                bool isNewDeal = _currentDealId == 0; // Сохраняем флаг новой сделки

                // ИСПОЛЬЗУЕМ ОСНОВНОЙ КОНТЕКСТ
                if (_currentDealId > 0)
                {
                    // Обновление существующей сделки
                    var deal = _context.Deals
                        .Include(d => d.CreatedByUser)
                        .Include(d => d.UpdatedByUser)
                        .Include(d => d.AssignedToUser)
                        .FirstOrDefault(d => d.Id == _currentDealId);
                    if (deal != null)
                    {
                        deal.Title = DealTitleTextBox.Text.Trim();
                        deal.ClientId = ((Client)DealClientComboBox.SelectedItem).Id;
                        deal.Amount = amount;
                        deal.Probability = (int)DealProbabilitySlider.Value;
                        deal.Category = DealCategoryTextBox.Text.Trim();
                        deal.Description = DealDescriptionTextBox.Text.Trim();
                        deal.Deadline = DealDeadlineDatePicker.SelectedDate.Value;
                        // Явно устанавливаем UpdatedByUserId
                        deal.UpdatedByUserId = _context.CurrentUser?.Id;

                        var statusTag = ((ComboBoxItem)DealStatusComboBox.SelectedItem).Tag.ToString();
                        deal.Status = (DealStatus)Enum.Parse(typeof(DealStatus), statusTag);

                        var priorityTag = ((ComboBoxItem)DealPriorityComboBox.SelectedItem).Tag.ToString();
                        deal.Priority = (Priority)Enum.Parse(typeof(Priority), priorityTag);

                        // Если сделка перешла в завершенный статус, устанавливаем дату закрытия
                        if ((deal.Status == DealStatus.Successful || deal.Status == DealStatus.Failed) && deal.ClosedAt == null)
                        {
                            deal.ClosedAt = DateTime.Now;
                        }

                        savedDeal = deal;
                    }
                    else
                    {
                        MessageBox.Show("Сделка не найдена!", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
                else
                {
                    // Добавление новой сделки
                    var newDeal = new Deal
                    {
                        Title = DealTitleTextBox.Text.Trim(),
                        ClientId = ((Client)DealClientComboBox.SelectedItem).Id,
                        Amount = amount,
                        Probability = (int)DealProbabilitySlider.Value,
                        Category = DealCategoryTextBox.Text.Trim(),
                        Description = DealDescriptionTextBox.Text.Trim(),
                        CreatedAt = DateTime.Now,
                        Deadline = DealDeadlineDatePicker.SelectedDate.Value,
                        CompanyId = MultiUserSecurityManager.CurrentUser.CompanyId,
                        CreatedByUserId = MultiUserSecurityManager.CurrentUser.Id
                    };

                    var statusTag = ((ComboBoxItem)DealStatusComboBox.SelectedItem).Tag.ToString();
                    newDeal.Status = (DealStatus)Enum.Parse(typeof(DealStatus), statusTag);

                    var priorityTag = ((ComboBoxItem)DealPriorityComboBox.SelectedItem).Tag.ToString();
                    newDeal.Priority = (Priority)Enum.Parse(typeof(Priority), priorityTag);

                    _context.Deals.Add(newDeal);
                    savedDeal = newDeal;
                }

                // СОХРАНЯЕМ В БАЗУ
                try
                {
                    _context.SaveChanges();
                    
                    // Обновляем информацию о пользователе в форме после сохранения
                    if (_currentDealId > 0)
                    {
                        // Обновляем отображение информации о пользователе
                        if (DealCreatedByInfo != null) 
                            DealCreatedByInfo.Text = FormatCreatedByInfo(savedDeal.CreatedByUserId, savedDeal.CreatedAt);
                        if (DealUpdatedInfo != null) 
                            DealUpdatedInfo.Text = FormatUpdatedInfo(savedDeal.UpdatedByUserId, DateTime.Now);
                        if (DealAssignedToInfo != null) 
                            DealAssignedToInfo.Text = FormatAssignedToInfo(savedDeal.AssignedToUserId);
                    }

                    // Создаем событие в календаре для новой сделки
                    if (isNewDeal && savedDeal != null)
                    {
                        CreateCalendarEventForDeal(savedDeal);
                    }
                }
                catch (DbUpdateException dbEx)
                {
                    string errorDetails = $"Ошибка базы данных: {dbEx.Message}";
                    if (dbEx.InnerException != null)
                    {
                        errorDetails += $"\nВнутренняя ошибка: {dbEx.InnerException.Message}";
                    }
                    throw new Exception(errorDetails, dbEx);
                }

                MessageBox.Show(_currentDealId > 0 ? "Сделка обновлена!" : "Сделка добавлена!",
                    "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                // Обновляем данные
                LoadDealsData();
                LoadDashboardData();
                
                // Обновляем календарь, если он открыт
                RefreshCalendarIfOpen();

                // Создаем уведомление о сделке
                if (savedDeal.Status == DealStatus.Successful || savedDeal.Status == DealStatus.Failed)
                {
                    CreateDealNotification(savedDeal);
                }

                // Закрываем форму
                CloseDealFormButton_Click(sender, e);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения: {ex.Message}\n\nПодробности:\n{ex.InnerException?.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);

                // Логируем ошибку
                Debug.WriteLine($"Ошибка сохранения сделки: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Debug.WriteLine($"Внутренняя ошибка: {ex.InnerException.Message}");
                }
            }
        }

        // Обновляем календарь, если он открыт
        private void RefreshCalendarIfOpen()
        {
            try
            {
                // Проверяем, открыт ли CalendarPage в главном окне
                if (CalendarPage != null && CalendarPage.Visibility == Visibility.Visible)
                {
                    var calendarPage = CalendarPage.Children.Count > 0 ? CalendarPage.Children[0] as CalendarPage : null;
                    if (calendarPage != null)
                    {
                        calendarPage.LoadEvents();
                        calendarPage.UpdateCalendar();
                        Debug.WriteLine("✅ CalendarPage обновлен после создания сделки");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Ошибка обновления календаря: {ex.Message}");
            }
        }

        private void CloseDealFormButton_Click(object sender, RoutedEventArgs e)
        {
            DealFormOverlay.Visibility = Visibility.Collapsed;
        }

        // Создание события в календаре для сделки
        private void CreateCalendarEventForDeal(Deal deal)
        {
            try
            {
                Debug.WriteLine($"=== Начинаем создание события в календаре для сделки #{deal.Id} ===");
                
                // Проверяем, что сделка имеет ID
                if (deal.Id == 0)
                {
                    Debug.WriteLine("❌ Ошибка: У сделки нет ID");
                    return;
                }
                
                // Проверяем авторизацию
                if (!MultiUserSecurityManager.IsAuthenticated)
                {
                    Debug.WriteLine("❌ Ошибка: Пользователь не авторизован");
                    return;
                }
                
                Debug.WriteLine($"✅ Пользователь авторизован: {MultiUserSecurityManager.CurrentUser.Username}");
                Debug.WriteLine($"✅ CompanyId: {MultiUserSecurityManager.CurrentUser.CompanyId}");
                
                // Получаем информацию о клиенте
                var client = _context.Clients.FirstOrDefault(c => c.Id == deal.ClientId);
                var clientName = client?.Name ?? "Неизвестный клиент";
                
                Debug.WriteLine($"✅ Клиент найден: {clientName}");

                var calendarEvent = new CalendarEvent
                {
                    Title = $"Дедлайн: {deal.Title}",
                    Description = $"Сделка с {clientName}\nСумма: {deal.Amount:N0} ₽\nСтатус: {deal.Status}\n\n{deal.Description}",
                    StartDate = deal.Deadline,
                    EndDate = deal.Deadline.AddHours(1),
                    IsAllDay = false,
                    EventType = CalendarEventType.Deadline,
                    Priority = deal.Priority,
                    Color = deal.Status == DealStatus.Successful ? "#10B981" : 
                           deal.Status == DealStatus.Failed ? "#EF4444" : 
                           deal.Status == DealStatus.InProgress ? "#3B82F6" : "#F59E0B",
                    ReminderMinutes = 1440, // За сутки
                    DealId = deal.Id,
                    ClientId = deal.ClientId,
                    CompanyId = MultiUserSecurityManager.CurrentUser.CompanyId,
                    CreatedByUserId = MultiUserSecurityManager.CurrentUser.Id,
                    Status = deal.Status == DealStatus.Successful ? EventStatus.Completed :
                             deal.Status == DealStatus.Failed ? EventStatus.Cancelled : EventStatus.Scheduled,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                Debug.WriteLine($"✅ Событие создано в памяти:");
                Debug.WriteLine($"   - Title: {calendarEvent.Title}");
                Debug.WriteLine($"   - StartDate: {calendarEvent.StartDate}");
                Debug.WriteLine($"   - CompanyId: {calendarEvent.CompanyId}");
                Debug.WriteLine($"   - CreatedByUserId: {calendarEvent.CreatedByUserId}");

                _context.CalendarEvents.Add(calendarEvent);
                var saveResult = _context.SaveChanges();
                
                Debug.WriteLine($"✅ Событие сохранено в базу. SaveChanges() вернул: {saveResult}");
                Debug.WriteLine($"✅ ID созданного события: {calendarEvent.Id}");
                Debug.WriteLine($"=== Событие в календаре успешно создано для сделки #{deal.Id}: {deal.Title} ===");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ Ошибка создания события в календаре: {ex.Message}");
                Debug.WriteLine($"❌ StackTrace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Debug.WriteLine($"❌ InnerException: {ex.InnerException.Message}");
                }
                // Не показываем ошибку пользователю, чтобы не прерывать сохранение сделки
            }
        }

        private void ExportDealsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog();
                dialog.Filter = "Excel Files|*.xlsx";
                dialog.Title = "Экспорт сделок в Excel";
                dialog.FileName = $"Сделки_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

                if (dialog.ShowDialog() == true)
                {
                    // Показываем прогресс
                    var exportWindow = new ExportProgressWindow();
                    exportWindow.Show();

                    // Экспортируем в отдельном потоке
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        try
                        {
                            // Используем существующий экспортер или создаем новый для сделок
                            using (var package = new OfficeOpenXml.ExcelPackage())
                            {
                                var worksheet = package.Workbook.Worksheets.Add("Сделки");

                                // Заголовки
                                worksheet.Cells[1, 1].Value = "ID";
                                worksheet.Cells[1, 2].Value = "Название";
                                worksheet.Cells[1, 3].Value = "Клиент";
                                worksheet.Cells[1, 4].Value = "Сумма";
                                worksheet.Cells[1, 5].Value = "Статус";
                                worksheet.Cells[1, 6].Value = "Приоритет";
                                worksheet.Cells[1, 7].Value = "Вероятность";
                                worksheet.Cells[1, 8].Value = "Срок";
                                worksheet.Cells[1, 9].Value = "Дата создания";

                                // Данные
                                int row = 2;
                                foreach (var deal in _allDeals)
                                {
                                    worksheet.Cells[row, 1].Value = deal.Id;
                                    worksheet.Cells[row, 2].Value = deal.Title;
                                    worksheet.Cells[row, 3].Value = deal.Client?.Name;
                                    worksheet.Cells[row, 4].Value = deal.Amount;
                                    worksheet.Cells[row, 5].Value = deal.Status;
                                    worksheet.Cells[row, 6].Value = deal.Priority;
                                    worksheet.Cells[row, 7].Value = deal.Probability;
                                    worksheet.Cells[row, 8].Value = deal.Deadline.ToString("dd.MM.yyyy");
                                    worksheet.Cells[row, 9].Value = deal.CreatedAt.ToString("dd.MM.yyyy HH:mm");
                                    row++;
                                }

                                package.SaveAs(new System.IO.FileInfo(dialog.FileName));
                            }

                            // Обновляем UI из основного потока
                            Dispatcher.Invoke(() =>
                            {
                                exportWindow.Close();
                                MessageBox.Show($"Экспорт успешно завершен!\nФайл сохранен: {dialog.FileName}",
                                    "Экспорт завершен",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information);
                            });
                        }
                        catch (Exception ex)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                exportWindow.Close();
                                MessageBox.Show($"Ошибка экспорта: {ex.Message}",
                                    "Ошибка",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                            });
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Сортировка по ID
        private void ClientsDataGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            if (_filteredClients == null) return;

            if (e.Column.Header.ToString() == "ID")
            {
                e.Handled = true;

                var direction = e.Column.SortDirection != System.ComponentModel.ListSortDirection.Ascending
                    ? System.ComponentModel.ListSortDirection.Ascending
                    : System.ComponentModel.ListSortDirection.Descending;

                e.Column.SortDirection = direction;

                if (direction == System.ComponentModel.ListSortDirection.Ascending)
                {
                    var sorted = _filteredClients.OrderBy(c => c.Id).ToList();
                    _filteredClients = new ObservableCollection<Client>(sorted);
                }
                else
                {
                    var sorted = _filteredClients.OrderByDescending(c => c.Id).ToList();
                    _filteredClients = new ObservableCollection<Client>(sorted);
                }

                ApplyPagination();
            }
        }

        private void DealsDataGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            if (_filteredDeals == null) return;

            if (e.Column.Header.ToString() == "ID")
            {
                e.Handled = true;

                var direction = e.Column.SortDirection != System.ComponentModel.ListSortDirection.Ascending
                    ? System.ComponentModel.ListSortDirection.Ascending
                    : System.ComponentModel.ListSortDirection.Descending;

                e.Column.SortDirection = direction;

                if (direction == System.ComponentModel.ListSortDirection.Ascending)
                {
                    var sorted = _filteredDeals.OrderBy(d => d.Id).ToList();
                    _filteredDeals = new ObservableCollection<Deal>(sorted);
                }
                else
                {
                    var sorted = _filteredDeals.OrderByDescending(d => d.Id).ToList();
                    _filteredDeals = new ObservableCollection<Deal>(sorted);
                }

                ApplyDealsPagination();
            }
        }
        #endregion

        #region Новые методы для импорта и отчетов

        // Импорт сделок из Excel
        private void ImportDealsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFileDialog();
                dialog.Filter = "Excel Files|*.xlsx;*.xls";
                dialog.Title = "Импорт сделок из Excel";
                dialog.FileName = "";

                if (dialog.ShowDialog() == true)
                {
                    // Проверяем файл
                    var validationErrors = DealExcelImporter.ValidateExcelFile(dialog.FileName);

                    if (validationErrors.Any())
                    {
                        string errorMessage = "Файл не соответствует требованиям:\n\n";
                        foreach (var error in validationErrors)
                        {
                            errorMessage += $"• {error}\n";
                        }
                        errorMessage += "\nСкачайте шаблон и заполните по образцу.";

                        MessageBox.Show(errorMessage, "Ошибка валидации",
                                      MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // Показываем окно опций
                    var optionsWindow = new ImportOptionsWindow(dialog.FileName);
                    if (optionsWindow.ShowDialog() != true)
                        return;

                    // Показываем окно прогресса
                    var progressWindow = new ImportProgressWindow("Импорт сделок");
                    progressWindow.Show();

                    // Запускаем импорт в отдельном потоке
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        try
                        {
                            var result = DealExcelImporter.ImportDealsFromExcel(
                                dialog.FileName,
                                optionsWindow.UpdateExisting,
                                optionsWindow.CreateClients);

                            Dispatcher.Invoke(() =>
                            {
                                progressWindow.Close();

                                string message;
                                MessageBoxImage icon;

                                if (result.errors > 0)
                                {
                                    message = $"Импорт завершен с ошибками!\n\n" +
                                             $"✅ Добавлено: {result.added} сделок\n" +
                                             $"♻ Обновлено: {result.updated} сделок\n" +
                                             $"⏭ Пропущено: {result.skipped} сделок\n" +
                                             $"❌ Ошибок: {result.errors}";
                                    icon = MessageBoxImage.Warning;
                                }
                                else
                                {
                                    message = $"✅ Импорт успешно завершен!\n\n" +
                                             $"Добавлено: {result.added} сделок\n" +
                                             $"Обновлено: {result.updated} сделок" +
                                             (result.skipped > 0 ? $"\nПропущено: {result.skipped} сделок" : "");
                                    icon = MessageBoxImage.Information;
                                }

                                var resultWindow = MessageBox.Show(
                                    $"{message}\n\nОбновить данные на экране?",
                                    "Результат импорта",
                                    MessageBoxButton.YesNo,
                                    icon);

                                if (resultWindow == MessageBoxResult.Yes)
                                {
                                    // Обновляем данные
                                    LoadDealsData();
                                    LoadDashboardData();

                                    // Если импортировали клиентов, обновляем и их
                                    if (result.added > 0)
                                        LoadClients();
                                }
                            });
                        }
                        catch (Exception ex)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                progressWindow.Close();
                                MessageBox.Show($"Критическая ошибка импорта:\n{ex.Message}",
                                              "Ошибка",
                                              MessageBoxButton.OK,
                                              MessageBoxImage.Error);
                            });
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}\n\nУбедитесь, что файл Excel не поврежден и не открыт в другой программе.",
                               "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Метод для показа детальной информации о файле
        private void ShowFileInfo(string filePath)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                using (var package = new ExcelPackage(fileInfo))
                {
                    var worksheet = package.Workbook.Worksheets[0];
                    int rowCount = worksheet.Dimension?.Rows ?? 0;
                    int colCount = worksheet.Dimension?.Columns ?? 0;

                    string message = $"📊 Информация о файле:\n\n" +
                                   $"Имя файла: {Path.GetFileName(filePath)}\n" +
                                   $"Размер: {fileInfo.Length / 1024} КБ\n" +
                                   $"Строк: {rowCount - 1}\n" +
                                   $"Колонок: {colCount}\n\n" +
                                   $"📋 Заголовки:\n";

                    for (int col = 1; col <= colCount; col++)
                    {
                        message += $"{col}. {worksheet.Cells[1, col].Text}\n";
                    }

                    MessageBox.Show(message, "Информация о файле",
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch { }
        }

        // Метод для быстрого просмотра данных
        private void PreviewExcelData(string filePath, int previewRows = 5)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                using (var package = new ExcelPackage(fileInfo))
                {
                    var worksheet = package.Workbook.Worksheets[0];
                    int rowCount = Math.Min(worksheet.Dimension?.Rows ?? 0, previewRows + 1);
                    int colCount = worksheet.Dimension?.Columns ?? 0;

                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine("👁 ПРЕВЬЮ ДАННЫХ (первые 5 строк):");
                    sb.AppendLine(new string('═', 50));

                    for (int row = 1; row <= rowCount; row++)
                    {
                        for (int col = 1; col <= colCount; col++)
                        {
                            sb.Append($"{worksheet.Cells[row, col].Text?.Trim() ?? ""}\t");
                        }
                        sb.AppendLine();
                        if (row == 1) sb.AppendLine(new string('─', 50));
                    }

                    MessageBox.Show(sb.ToString(), "Превью данных",
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch { }
        }

        // Создать тестовый отчет
        private void GenerateTestReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var context = MultiUserSecurityManager.CreateCompanyContext())
                {
                    var sb = new System.Text.StringBuilder();

                    sb.AppendLine("📊 ТЕСТОВЫЙ ОТЧЕТ");
                    sb.AppendLine($"Дата: {DateTime.Now:dd.MM.yyyy HH:mm}");
                    sb.AppendLine("=".PadRight(40, '='));
                    sb.AppendLine();

                    sb.AppendLine("📈 СТАТИСТИКА:");
                    sb.AppendLine($"• Всего клиентов: {context.Clients.Count()}");
                    sb.AppendLine($"• Всего сделок: {context.Deals.Count()}");

                    var successfulDeals = context.Deals.Count(d => d.Status == DealStatus.Successful);
                    var totalAmount = context.Deals
                        .Where(d => d.Status == DealStatus.Successful)
                        .Sum(d => d.Amount);

                    sb.AppendLine($"• Успешных сделок: {successfulDeals}");
                    sb.AppendLine($"• Общая сумма: {totalAmount:N0} ₽");
                    sb.AppendLine();

                    sb.AppendLine("👥 ПОСЛЕДНИЕ КЛИЕНТЫ:");
                    var lastClients = context.Clients
                        .OrderByDescending(c => c.CreatedAt)
                        .Take(3)
                        .ToList();

                    foreach (var client in lastClients)
                    {
                        sb.AppendLine($"• {client.Name} ({client.CreatedAt:dd.MM.yyyy})");
                    }

                    MessageBox.Show(sb.ToString(), "Тестовый отчет",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка");
            }
        }

        // Скачать шаблон Excel (простой)
        private void DownloadTemplateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog();
                dialog.Filter = "Excel Files|*.xlsx";
                dialog.Title = "Скачать шаблон";
                dialog.FileName = $"Шаблон_CRM_{DateTime.Now:yyyyMMdd}.xlsx";

                if (dialog.ShowDialog() == true)
                {
                    // Простое создание Excel файла
                    using (var package = new OfficeOpenXml.ExcelPackage())
                    {
                        var worksheet = package.Workbook.Worksheets.Add("Шаблон");

                        worksheet.Cells["A1"].Value = "ФИО";
                        worksheet.Cells["B1"].Value = "Телефон";
                        worksheet.Cells["C1"].Value = "Email";
                        worksheet.Cells["D1"].Value = "Примечания";

                        // Примеры
                        worksheet.Cells["A2"].Value = "Иванов Иван Иванович";
                        worksheet.Cells["B2"].Value = "+7 (999) 123-45-67";
                        worksheet.Cells["C2"].Value = "ivanov@example.com";
                        worksheet.Cells["D2"].Value = "Постоянный клиент";

                        worksheet.Cells["A1:D2"].Style.Font.Bold = true;
                        worksheet.Cells["A1:D2"].AutoFitColumns();

                        package.SaveAs(new System.IO.FileInfo(dialog.FileName));
                    }

                    MessageBox.Show($"Шаблон создан!\n{dialog.FileName}",
                        "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Открыть файл
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = dialog.FileName,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка");
            }
        }

        #endregion

        // Генерация отчета по продажам
        private void GenerateSalesReport_Click(object sender, RoutedEventArgs e)
        {
            if (ReportStartDate.SelectedDate == null || ReportEndDate.SelectedDate == null)
            {
                MessageBox.Show("Выберите период!", "Внимание");
                return;
            }

            var startDate = ReportStartDate.SelectedDate.Value;
            var endDate = ReportEndDate.SelectedDate.Value;

            if (startDate > endDate)
            {
                MessageBox.Show("Начальная дата не может быть больше конечной!", "Ошибка");
                return;
            }

            try
            {
                using (var context = MultiUserSecurityManager.CreateCompanyContext())
                {
                    var deals = context.Deals
                        .Include(d => d.Client)
                        .Where(d => d.CreatedAt >= startDate && d.CreatedAt <= endDate)
                        .ToList();

                    if (deals.Count == 0)
                    {
                        MessageBox.Show($"В период с {startDate:dd.MM.yyyy} по {endDate:dd.MM.yyyy} нет сделок!\n" +
                            "Выберите другой период или добавьте сделки.",
                            "Нет данных", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine($"=== ОТЧЕТ ПО ПРОДАЖАМ ===");
                    sb.AppendLine($"Период: {startDate:dd.MM.yyyy} - {endDate:dd.MM.yyyy}");
                    sb.AppendLine($"Дата формирования: {DateTime.Now:dd.MM.yyyy HH:mm}");
                    sb.AppendLine();

                    // Общая статистика
                    var totalDeals = deals.Count;
                    var successfulDeals = deals.Count(d => d.Status == DealStatus.Successful);
                    var inProgressDeals = deals.Count(d => d.Status == DealStatus.InProgress);
                    var failedDeals = deals.Count(d => d.Status == DealStatus.Failed);
                    var totalRevenue = deals.Where(d => d.Status == DealStatus.Successful).Sum(d => d.Amount);
                    var conversionRate = totalDeals > 0 ? (successfulDeals * 100.0 / totalDeals) : 0;

                    sb.AppendLine("--- ОБЩАЯ СТАТИСТИКА ---");
                    sb.AppendLine($"Всего сделок: {totalDeals}");
                    sb.AppendLine($"Успешных: {successfulDeals} ({conversionRate:F1}%)");
                    sb.AppendLine($"В работе: {inProgressDeals}");
                    sb.AppendLine($"Проваленных: {failedDeals}");
                    sb.AppendLine($"Общая выручка: {totalRevenue:N0} ₽");
                    var averageCheck = successfulDeals > 0 ? (totalRevenue / successfulDeals) : 0;
sb.AppendLine($"Средний чек: {averageCheck:N0} ₽");
                    sb.AppendLine();

                    // Детализация по статусам
                    sb.AppendLine("--- РАСПРЕДЕЛЕНИЕ ПО СТАТУСАМ ---");
                    foreach (DealStatus status in Enum.GetValues(typeof(DealStatus)))
                    {
                        var count = deals.Count(d => d.Status == status);
                        var amount = deals.Where(d => d.Status == status).Sum(d => d.Amount);
                        sb.AppendLine($"{status}: {count} сделок на сумму {amount:N0} ₽");
                    }
                    sb.AppendLine();

                    // Топ сделок
                    sb.AppendLine("--- ТОП-5 СДЕЛОК ПО СУММЕ ---");
                    var topDeals = deals.OrderByDescending(d => d.Amount).Take(5).ToList();
                    for (int i = 0; i < topDeals.Count; i++)
                    {
                        var deal = topDeals[i];
                        sb.AppendLine($"{i + 1}. {deal.Title}");
                        sb.AppendLine($"   Клиент: {deal.Client?.Name ?? "Не указан"}");
                        sb.AppendLine($"   Сумма: {deal.Amount:N0} ₽");
                        sb.AppendLine($"   Статус: {deal.Status}");
                        sb.AppendLine();
                    }

                    // Статистика по клиентам
                    sb.AppendLine("--- ТОП-5 КЛИЕНТОВ ПО ВЫРУЧКЕ ---");
                    var clientStats = deals
                        .Where(d => d.Status == DealStatus.Successful)
                        .GroupBy(d => d.Client)
                        .Select(g => new
                        {
                            Client = g.Key,
                            DealCount = g.Count(),
                            TotalAmount = g.Sum(d => d.Amount)
                        })
                        .OrderByDescending(c => c.TotalAmount)
                        .Take(5)
                        .ToList();

                    for (int i = 0; i < clientStats.Count; i++)
                    {
                        var client = clientStats[i];
                        sb.AppendLine($"{i + 1}. {client.Client?.Name ?? "Неизвестный"}");
                        sb.AppendLine($"   Сделок: {client.DealCount}");
                        sb.AppendLine($"   Выручка: {client.TotalAmount:N0} ₽");
                        sb.AppendLine();
                    }

                    ReportTitle.Text = "Отчет по продажам за период";
                    ReportContent.Text = sb.ToString();
                    ReportOutputArea.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка формирования отчета: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Генерация отчета по прибыли и расходам (учитывает расходы из раздела "Финансы")
        private void GenerateProfitReport_Click(object sender, RoutedEventArgs e)
        {
            if (ProfitReportStartDate?.SelectedDate == null || ProfitReportEndDate?.SelectedDate == null)
            {
                MessageBox.Show("Выберите период!", "Внимание");
                return;
            }

            var startDate = ProfitReportStartDate.SelectedDate.Value;
            var endDate = ProfitReportEndDate.SelectedDate.Value;

            if (startDate > endDate)
            {
                MessageBox.Show("Начальная дата не может быть больше конечной!", "Ошибка");
                return;
            }

            try
            {
                var reportText = BusinessAnalytics.GenerateProfitReport(startDate, endDate);
                ReportTitle.Text = $"Отчет по прибыли и расходам за {startDate:dd.MM.yyyy} - {endDate:dd.MM.yyyy}";
                ReportContent.Text = reportText;
                ReportOutputArea.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка формирования отчета: {ex.Message}", "Ошибка");
            }
        }

        // Отчет по клиентам
        private void GenerateClientsReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var context = MultiUserSecurityManager.CreateCompanyContext())
                {
                    // Простой отчет по клиентам
                    var clients = context.Clients
                        .OrderByDescending(c => c.CreatedAt)
                        .Take(20)
                        .ToList();

                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine("👥 ОТЧЕТ ПО КЛИЕНТАМ");
                    sb.AppendLine($"Всего клиентов: {context.Clients.Count()}");
                    sb.AppendLine("=".PadRight(50, '='));
                    sb.AppendLine();

                    sb.AppendLine("📋 ПОСЛЕДНИЕ КЛИЕНТЫ:");
                    foreach (var client in clients)
                    {
                        var dealsCount = context.Deals.Count(d => d.ClientId == client.Id);
                        sb.AppendLine($"• {client.Name}");
                        sb.AppendLine($"  Телефон: {client.Phone ?? "Не указан"}");
                        sb.AppendLine($"  Email: {client.Email ?? "Не указан"}");
                        sb.AppendLine($"  Добавлен: {client.CreatedAt:dd.MM.yyyy}");
                        sb.AppendLine($"  Сделок: {dealsCount}");
                        sb.AppendLine();
                    }

                    ReportTitle.Text = "Отчет по клиентам";
                    ReportContent.Text = sb.ToString();
                    ReportOutputArea.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка");
            }
        }

        // Экспорт всех данных
        private void ExportAllData_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog();
                dialog.Filter = "Excel Files|*.xlsx";
                dialog.Title = "Экспорт всех данных";
                dialog.FileName = $"CRM_Экспорт_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

                if (dialog.ShowDialog() == true)
                {
                    using (var context = MultiUserSecurityManager.CreateCompanyContext())
                    {
                        var allClients = context.Clients.ToList();
                        var allDeals = context.Deals.Include(d => d.Client).ToList();
                        var allExpenses = context.Expenses.OrderByDescending(e => e.Date).ToList();
                        var allEvents = context.CalendarEvents.Include(e => e.Client).ToList();

                        ExcelExporter.ExportComprehensiveReport(allClients, allDeals, allExpenses, allEvents, dialog.FileName);

                        MessageBox.Show($"Экспорт завершен!\nФайл: {dialog.FileName}",
                            "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка экспорта: {ex.Message}", "Ошибка");
            }
        }

        // Копировать отчет в буфер
        private void CopyReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Windows.Clipboard.SetText(ReportContent.Text);
                MessageBox.Show("Отчет скопирован в буфер обмена!", "Успешно",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка копирования: {ex.Message}", "Ошибка");
            }
        }

        // Печать отчета
        private void PrintReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var printDialog = new PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    var document = new FlowDocument(new Paragraph(new Run(ReportContent.Text)))
                    {
                        FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                        FontSize = 12
                    };

                    printDialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, "Отчет CRM");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка печати: {ex.Message}", "Ошибка");
            }
        }

        // Закрыть отчет
        private void CloseReport_Click(object sender, RoutedEventArgs e)
        {
            ReportOutputArea.Visibility = Visibility.Collapsed;
        }

        // Загрузка настроек при открытии
        private void LoadSettings()
        {
            try
            {
                // Старые настройки
                if (AutoBackupCheck != null)
                    AutoBackupCheck.IsChecked = _settings.AutoBackup;

                if (BackupDaysText != null)
                    BackupDaysText.Text = _settings.BackupDays.ToString();

                if (ShowNotificationsCheck != null)
                    ShowNotificationsCheck.IsChecked = _settings.ShowNotifications;

                // Звуковые уведомления (если есть в XAML)
                if (SoundNotificationsCheck != null)
                    SoundNotificationsCheck.IsChecked = _settings.SoundNotifications;

                // Отчеты / Дашборд
                if (ShowFinanceOnDashboardCheck != null)
                    ShowFinanceOnDashboardCheck.IsChecked = _settings.ShowFinanceOnDashboard;
                if (ShowFinanceReportsCheck != null)
                    ShowFinanceReportsCheck.IsChecked = _settings.ShowFinanceReports;

                ApplyFinanceVisibilityFromSettings();
                LoadSettingsFinanceSummary();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка загрузки настроек: {ex.Message}");
            }
        }

        private void LoadSettingsFinanceSummary()
        {
            try
            {
                using (var db = MultiUserSecurityManager.CreateCompanyContext())
                {
                    // SQLite не всегда умеет SUM по decimal на стороне БД — суммируем на клиенте
                    decimal revenue = db.Deals
                        .Where(d => d.Status == DealStatus.Successful)
                        .Select(d => d.Amount)
                        .ToList()
                        .Sum();

                    decimal expenses = 0;
                    try
                    {
                        // SQLite не всегда умеет SUM по decimal на стороне БД — суммируем на клиенте
                        expenses = db.Expenses.Select(e => e.Amount).ToList().Sum();
                    }
                    catch
                    {
                        expenses = 0;
                    }

                    decimal netProfit = revenue - expenses;
                    decimal margin = revenue > 0 ? (netProfit / revenue * 100) : 0;

                    if (SettingsTotalExpenses != null)
                        SettingsTotalExpenses.Text = $"{expenses:N0} ₽";
                    if (SettingsNetProfit != null)
                        SettingsNetProfit.Text = $"{netProfit:N0} ₽";
                    if (SettingsProfitMargin != null)
                        SettingsProfitMargin.Text = $"{margin:F1}%";

                    if (SettingsNetProfit != null)
                    {
                        SettingsNetProfit.Foreground = netProfit >= 0
                            ? (SolidColorBrush)FindResource("SuccessColor")
                            : (SolidColorBrush)FindResource("DangerColor");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка сводки финансов в настройках: {ex.Message}");
            }
        }

        private void ApplyFinanceVisibilityFromSettings()
        {
            try
            {
                var dashboardVisibility = _settings.ShowFinanceOnDashboard ? Visibility.Visible : Visibility.Collapsed;
                if (DashboardExpensesCard != null) DashboardExpensesCard.Visibility = dashboardVisibility;
                if (DashboardNetProfitCard != null) DashboardNetProfitCard.Visibility = dashboardVisibility;
                if (DashboardProfitMarginCard != null) DashboardProfitMarginCard.Visibility = dashboardVisibility;

                var reportsVisibility = _settings.ShowFinanceReports ? Visibility.Visible : Visibility.Collapsed;
                if (ProfitReportCard != null) ProfitReportCard.Visibility = reportsVisibility;
            }
            catch
            {
                // не критично
            }
        }





        // Сохранить настройки
        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Старые настройки
                if (AutoBackupCheck != null)
                    _settings.AutoBackup = AutoBackupCheck.IsChecked == true;

                if (BackupDaysText != null && int.TryParse(BackupDaysText.Text, out int days) && days > 0)
                    _settings.BackupDays = days;

                if (ShowNotificationsCheck != null)
                    _settings.ShowNotifications = ShowNotificationsCheck.IsChecked == true;

                // Звуковые уведомления
                if (SoundNotificationsCheck != null)
                    _settings.SoundNotifications = SoundNotificationsCheck.IsChecked == true;

                // Отчеты / Дашборд
                if (ShowFinanceOnDashboardCheck != null)
                    _settings.ShowFinanceOnDashboard = ShowFinanceOnDashboardCheck.IsChecked == true;
                if (ShowFinanceReportsCheck != null)
                    _settings.ShowFinanceReports = ShowFinanceReportsCheck.IsChecked == true;

                _settings.Save();
                ApplyFinanceVisibilityFromSettings();
                
                // Перезапускаем автоматический бэкап с новыми настройками
                BackupManager.Instance.RestartAutoBackup();

                MessageBox.Show("✅ Настройки сохранены!", "Успешно",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Ошибка сохранения: {ex.Message}", "Ошибка");
            }
        }

        // Создать резервную копию
        private async void CreateBackup_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog();
                dialog.Filter = "Backup Files|*.zip|All Files|*.*";
                dialog.Title = "Создать резервную копию";
                dialog.FileName = $"CRM_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.zip";

                if (dialog.ShowDialog() == true)
                {
                    // Показываем прогресс
                    MessageBox.Show("Создание резервной копии...\nПожалуйста, подождите.", 
                        "Бэкап", MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    bool success = await BackupManager.Instance.CreateFullBackup(dialog.FileName);
                    
                    if (success)
                    {
                        MessageBox.Show($"Резервная копия успешно создана!\n\nФайл: {dialog.FileName}\n\nРазмер: {new FileInfo(dialog.FileName).Length / 1024} КБ",
                            "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Не удалось создать резервную копию.\n\nВозможные причины:\n• Базы данных заблокированы другим процессом\n• Недостаточно места на диске\n• Отсутствуют права доступа", 
                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Критическая ошибка при создании бэкапа:\n{ex.Message}\n\nПопробуйте закрыть приложение и создать бэкап заново.", 
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Восстановить из копии
        private async void RestoreBackup_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFileDialog();
                dialog.Filter = "Backup Files|*.zip|All Files|*.*";
                dialog.Title = "Восстановить из резервной копии";

                if (dialog.ShowDialog() == true)
                {
                    var result = MessageBox.Show(
                        "ВНИМАНИЕ! Восстановление перезапишет ВСЕ текущие данные:\n\n" +
                        "• Клиенты и сделки\n" +
                        "• Финансовые записи\n" +
                        "• Задачи и события\n" +
                        "• Пользователи компании\n\n" +
                        "Это действие НЕВОЗМОЖНО отменить!\n\n" +
                        "Продолжить восстановление?",
                        "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Yes)
                    {
                        // Показываем прогресс
                        MessageBox.Show("Восстановление данных...\nПожалуйста, подождите.", 
                            "Восстановление", MessageBoxButton.OK, MessageBoxImage.Information);
                        
                        bool success = await BackupManager.Instance.RestoreFromBackup(dialog.FileName);
                        
                        if (success)
                        {
                            MessageBox.Show(
                                "Данные успешно восстановлены!\n\n" +
                                "Что было восстановлено:\n" +
                                "• Базы данных компаний\n" +
                                "• Глобальная база данных\n" +
                                "• Настройки приложения\n" +
                                "• Файлы безопасности\n\n" +
                                "ВАЖНО: После перезапуска приложения:\n" +
                                "1. Используйте код компании из бэкапа\n" +
                                "2. Введите логин и пароль из бэкапа\n" +
                                "3. Если вход не работает - проверьте данные\n\n" +
                                "ОБЯЗАТЕЛЬНО перезапустите приложение сейчас!",
                                "Восстановление завершено", MessageBoxButton.OK, MessageBoxImage.Information);
                            
                            // Перезапускаем приложение после успешного восстановления
                            RestartApplication();
                        }
                        else
                        {
                            MessageBox.Show(
                                "Не удалось восстановить данные!\n\n" +
                                "Возможные причины:\n" +
                                "• Файл бэкапа поврежден\n" +
                                "• Базы данных заблокированы\n" +
                                "• Недостаточно прав доступа\n" +
                                "• Несовместимая версия бэкапа\n\n" +
                                "Попробуйте закрыть приложение и повторить попытку.", 
                                "Ошибка восстановления", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка");
            }
        }
        
        /// <summary>
        /// Перезапустить приложение
        /// </summary>
        private void RestartApplication()
        {
            try
            {
                // Получаем путь к исполняемому файлу
                string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                string exeDir = Path.GetDirectoryName(exePath);
                string mainExe = Path.Combine(exeDir, "MyFirstCRM.exe");
                
                if (!File.Exists(mainExe))
                {
                    // Ищем .exe файл в директории
                    var exeFiles = Directory.GetFiles(exeDir, "*.exe");
                    if (exeFiles.Length > 0)
                    {
                        mainExe = exeFiles[0];
                    }
                }
                
                if (File.Exists(mainExe))
                {
                    // Запускаем новый экземпляр приложения
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = mainExe,
                        UseShellExecute = true
                    };
                    
                    Process.Start(startInfo);
                    
                    // Закрываем текущее приложение
                    Application.Current.Shutdown();
                }
                else
                {
                    MessageBox.Show("Не удалось найти исполняемый файл приложения. Пожалуйста, перезапустите приложение вручную.", 
                        "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при перезапуске приложения: {ex.Message}\n\nПожалуйста, перезапустите приложение вручную.", 
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

                // Бизнес-аналитика
                private void GenerateBusinessAnalytics_Click(object sender, RoutedEventArgs e)
                {
                    try
                    {
                        var report = BusinessAnalytics.GenerateAnalyticsReport();

                        ReportTitle.Text = "Бизнес-аналитика";
                        ReportContent.Text = report;
                        ReportOutputArea.Visibility = Visibility.Visible;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка генерации анализа: {ex.Message}", "Ошибка");
                    }
                }

                // Скачать шаблон для клиентов
                private void DownloadClientTemplate_Click(object sender, RoutedEventArgs e)
                {
                    try
                    {
                        var dialog = new Microsoft.Win32.SaveFileDialog();
                        dialog.Filter = "Excel Files|*.xlsx";
                        dialog.Title = "Скачать шаблон для клиентов";
                        dialog.FileName = $"Шаблон_клиентов_{DateTime.Now:yyyyMMdd}.xlsx";

                        if (dialog.ShowDialog() == true)
                        {
                            ExcelTemplates.CreateClientTemplate(dialog.FileName);

                            MessageBox.Show($"Шаблон успешно создан!\n{dialog.FileName}\n\n" +
                                          "Откройте файл и заполните данные по образцу.",
                                          "Шаблон готов",
                                          MessageBoxButton.OK,
                                          MessageBoxImage.Information);

                            // Открыть файл
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = dialog.FileName,
                                UseShellExecute = true
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка создания шаблона: {ex.Message}", "Ошибка");
                    }
                }

                // Скачать шаблон для сделок
                private void DownloadDealTemplate_Click(object sender, RoutedEventArgs e)
                {
                    try
                    {
                        var dialog = new Microsoft.Win32.SaveFileDialog();
                        dialog.Filter = "Excel Files|*.xlsx";
                        dialog.Title = "Скачать шаблон для сделок";
                        dialog.FileName = $"Шаблон_сделок_{DateTime.Now:yyyyMMdd}.xlsx";

                        if (dialog.ShowDialog() == true)
                        {
                            ExcelTemplates.CreateDealTemplate(dialog.FileName);

                            MessageBox.Show($"Шаблон успешно создан!\n{dialog.FileName}\n\n" +
                                          "ВАЖНО: Клиенты должны быть сначала добавлены в систему.",
                                          "Шаблон готов",
                                          MessageBoxButton.OK,
                                          MessageBoxImage.Information);

                            // Открыть файл
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = dialog.FileName,
                                UseShellExecute = true
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка создания шаблона: {ex.Message}", "Ошибка");
                    }
                }

                // Скачать комбинированный шаблон (новый метод)
                private void DownloadCombinedTemplate_Click(object sender, RoutedEventArgs e)
                {
                    try
                    {
                        var dialog = new Microsoft.Win32.SaveFileDialog();
                        dialog.Filter = "Excel Files|*.xlsx";
                        dialog.Title = "Скачать полный шаблон";
                        dialog.FileName = $"Полный_шаблон_CRM_{DateTime.Now:yyyyMMdd}.xlsx";

                        if (dialog.ShowDialog() == true)
                        {
                            ExcelTemplates.CreateCombinedTemplate(dialog.FileName);

                            MessageBox.Show($"Полный шаблон создан!\n{dialog.FileName}\n\n" +
                                          "Файл содержит:\n" +
                                          "• Шаблон для клиентов\n" +
                                          "• Шаблон для сделок\n" +
                                          "• Подробную инструкцию",
                                          "Шаблон готов",
                                          MessageBoxButton.OK,
                                          MessageBoxImage.Information);

                            // Открыть файл
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = dialog.FileName,
                                UseShellExecute = true
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка");
                    }
                }

                // Цветовые темы


                private void SaveAndExitButton_Click(object sender, RoutedEventArgs e)
                {
                    try
                    {
                        // Сохраняем настройки
                        SaveSettings_Click(sender, e);

                        // Подтверждение
                        var result = MessageBox.Show("Настройки сохранены. Закрыть программу?",
                                                    "Выход",
                                                    MessageBoxButton.YesNo,
                                                    MessageBoxImage.Question);

                        if (result == MessageBoxResult.Yes)
                        {
                            // Сохраняем настройки еще раз на всякий случай
                            _settings.Save();

                            // Закрываем приложение
                            Application.Current.Shutdown();
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка при выходе: {ex.Message}", "Ошибка");
                    }
                }

                // Анализ системы - ПЕРЕДЕЛАННЫЙ МЕТОД
                private void RunSystemAnalysis_Click(object sender, RoutedEventArgs e)
                {
                    try
                    {
                        using (var context = MultiUserSecurityManager.CreateCompanyContext())
                        {
                            var analysis = new StringBuilder();
                            analysis.AppendLine("🔍 АНАЛИЗ СИСТЕМЫ");
                            analysis.AppendLine($"Время анализа: {DateTime.Now:dd.MM.yyyy HH:mm}");
                            analysis.AppendLine("=".PadRight(40, '='));

                            // Анализ базы данных
                            int clientCount = context.Clients.Count();
                            int dealCount = context.Deals.Count();

                            // Загружаем успешные сделки в память перед агрегацией
                            var successfulDeals = context.Deals
                                .Where(d => d.Status == DealStatus.Successful)
                                .ToList();

                            int successfulDealsCount = successfulDeals.Count;
                            decimal totalRevenue = successfulDeals.Sum(d => d.Amount);

                            analysis.AppendLine("\n📊 ОСНОВНАЯ СТАТИСТИКА:");
                            analysis.AppendLine($"• Клиентов в базе: {clientCount}");
                            analysis.AppendLine($"• Всего сделок: {dealCount}");
                            analysis.AppendLine($"• Успешных сделок: {successfulDealsCount}");
                            analysis.AppendLine($"• Общая выручка: {totalRevenue:N0} ₽");

                            // Процент успешных сделок
                            double successRate = dealCount > 0 ? (double)successfulDealsCount / dealCount * 100 : 0;

                            // Анализ производительности
                            analysis.AppendLine("\n⚡ ПРОИЗВОДИТЕЛЬНОСТЬ СИСТЕМЫ:");

                            // Проверка размера БД
                            string dbPath = GetDatabasePath();
                            if (File.Exists(dbPath))
                            {
                                var fileInfo = new FileInfo(dbPath);
                                double sizeMB = fileInfo.Length / (1024.0 * 1024.0);
                                analysis.AppendLine($"• Размер базы данных: {sizeMB:F2} MB");

                                if (sizeMB > 10)
                                    analysis.AppendLine($"⚠  База данных большая, рекомендуется архивация");
                            }

                            // Рекомендации
                            analysis.AppendLine("\n💡 РЕКОМЕНДАЦИИ:");

                            if (clientCount == 0)
                                analysis.AppendLine("• Добавьте первых клиентов через импорт или вручную");

                            if (dealCount == 0)
                                analysis.AppendLine("• Начните добавлять сделки для отслеживания продаж");

                            if (successfulDealsCount == 0 && dealCount > 0)
                                analysis.AppendLine("• Проанализируйте причины неудачных сделок");

                            // Проверка на наличие недавней активности
                            var lastClient = context.Clients.OrderByDescending(c => c.CreatedAt).FirstOrDefault();
                            if (lastClient != null)
                            {
                                var daysSinceLastClient = (DateTime.Now - lastClient.CreatedAt).Days;
                                if (daysSinceLastClient > 7)
                                    analysis.AppendLine($"• Не было новых клиентов {daysSinceLastClient} дней");
                            }

                            analysis.AppendLine("\n✅ Система работает нормально");

                            // Показать анализ
                            MessageBox.Show(analysis.ToString(), "Анализ системы",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка анализа: {ex.Message}\n\nУбедитесь, что база данных доступна.", "Ошибка анализа");
                    }
                }

                // Статистика базы данных
                // Статистика базы данных
                private void ShowDatabaseStats_Click(object sender, RoutedEventArgs e)
                {
                    try
                    {
                        // Диагностика - выводим информацию о текущей компании
                        var diagnosticInfo = new StringBuilder();
                        diagnosticInfo.AppendLine("🔍 ДИАГНОСТИКА БАЗЫ ДАННЫХ");
                        
                        if (MultiUserSecurityManager.CurrentCompany != null)
                        {
                            diagnosticInfo.AppendLine($"Текущая компания: {MultiUserSecurityManager.CurrentCompany.Name}");
                            diagnosticInfo.AppendLine($"ID компании: {MultiUserSecurityManager.CurrentCompany.Id}");
                            diagnosticInfo.AppendLine($"CompanyCode: {MultiUserSecurityManager.CurrentCompany.CompanyCode}");
                            diagnosticInfo.AppendLine($"DatabasePath из компании: {MultiUserSecurityManager.CurrentCompany.DatabasePath}");
                        }
                        else
                        {
                            diagnosticInfo.AppendLine("❌ Нет активной компании!");
                        }
                        
                        // Получаем путь через наш метод
                        string dbPath = GetDatabasePath();
                        diagnosticInfo.AppendLine($"Путь от GetDatabasePath(): {dbPath}");
                        diagnosticInfo.AppendLine($"Файл существует: {File.Exists(dbPath)}");
                        
                        if (File.Exists(dbPath))
                        {
                            var fileInfo = new FileInfo(dbPath);
                            diagnosticInfo.AppendLine($"Размер файла: {fileInfo.Length} байт");
                            diagnosticInfo.AppendLine($"Дата изменения: {fileInfo.LastWriteTime}");
                        }
                        
                        MessageBox.Show(diagnosticInfo.ToString(), "Диагностика базы данных",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        
                        // Если файл существует, показываем статистику
                        if (File.Exists(dbPath))
                        {
                            var fileInfo = new FileInfo(dbPath);
                            var sizeMB = fileInfo.Length / (1024.0 * 1024.0);

                            using (var context = MultiUserSecurityManager.CreateCompanyContext())
                            {
                                var stats = new StringBuilder();
                                stats.AppendLine("📊 СТАТИСТИКА БАЗЫ ДАННЫХ");
                                stats.AppendLine($"• Размер файла: {sizeMB:F2} MB");
                                stats.AppendLine($"• Дата создания: {fileInfo.CreationTime:dd.MM.yyyy}");
                                stats.AppendLine($"• Последнее изменение: {fileInfo.LastWriteTime:dd.MM.yyyy}");
                                stats.AppendLine($"\n📈 СОДЕРЖИМОЕ:");
                                stats.AppendLine($"• Клиентов: {context.Clients.Count()}");
                                stats.AppendLine($"• Сделок: {context.Deals.Count()}");

                                // Загружаем все сделки в память для агрегации
                                var allDeals = context.Deals
                                    .Include(d => d.Client)
                                    .ToList();

                                // Статистика по статусам сделок
                                var dealsByStatus = allDeals
                                    .GroupBy(d => d.Status)
                                    .Select(g => new { Status = g.Key, Count = g.Count() })
                                    .ToList();

                                if (dealsByStatus.Any())
                                {
                                    stats.AppendLine($"\n📊 СДЕЛКИ ПО СТАТУСАМ:");
                                    foreach (var statusGroup in dealsByStatus)
                                    {
                                        stats.AppendLine($"  {statusGroup.Status}: {statusGroup.Count}");
                                    }
                                }

                                // Самые активные клиенты
                                var topClients = allDeals
                                    .GroupBy(d => d.ClientId)
                                    .Select(g => new {
                                        ClientId = g.Key,
                                        DealCount = g.Count(),
                                        ClientName = context.Clients.FirstOrDefault(c => c.Id == g.Key)?.Name
                                    })
                                    .Where(x => x.ClientName != null)
                                    .OrderByDescending(x => x.DealCount)
                                    .Take(5)
                                    .ToList();

                                if (topClients.Any())
                                {
                                    stats.AppendLine($"\n👥 ТОП-5 КЛИЕНТОВ ПО СДЕЛКАМ:");
                                    foreach (var client in topClients)
                                    {
                                        stats.AppendLine($"  {client.ClientName}: {client.DealCount} сделок");
                                    }
                                }

                                MessageBox.Show(stats.ToString(), "Статистика базы данных",
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                        }
                        else
                        {
                            MessageBox.Show("База данных не найдена!", "Ошибка");
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка получения статистики: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}", "Ошибка");
                    }
                }

        // Оптимизация базы данных - РАБОЧАЯ ВЕРСИЯ
        private void OptimizeDatabase_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show("Оптимизация базы данных может занять некоторое время.\nПродолжить?",
                    "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    _context?.Dispose();
                    using var optimizeContext = MultiUserSecurityManager.CreateCompanyContext(); // Сокращено
                    optimizeContext.Database.ExecuteSqlRaw("VACUUM;");
                    InitializeDbContext();
                    MessageBox.Show("✅ База данных успешно оптимизирована!",
                        "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка оптимизации: {ex.Message}", "Ошибка");
            }
        }

        private string GetDatabasePath()
        {
            try
            {
                // Используем тот же подход, что и AppDbContext
                if (MultiUserSecurityManager.CurrentCompany != null)
                {
                    string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    string basePath = Path.Combine(appDataPath, "MyFirstCRM");
                    
                    // Сначала проверяем, есть ли у компании указанный путь к базе данных
                    if (!string.IsNullOrEmpty(MultiUserSecurityManager.CurrentCompany.DatabasePath))
                    {
                        return MultiUserSecurityManager.CurrentCompany.DatabasePath;
                    }
                    
                    // Иначе используем путь по умолчанию для компании через CompanyCode (как в MultiUserSecurityManager)
                    return Path.Combine(basePath, "Companies", $"company_{MultiUserSecurityManager.CurrentCompany.CompanyCode}.db");
                }
                
                // Если нет активной компании, возвращаем путь к глобальной базе
                string appDataPathGlobal = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(appDataPathGlobal, "MyFirstCRM", "global.db");
            }
            catch (Exception)
            {
                // В случае ошибки возвращаем старый путь для совместимости
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(appDataPath, "MyFirstCRM", "database.db");
            }
        }

        // Сброс всех фильтров - РАБОЧАЯ ВЕРСИЯ


        // Экспорт статистики - РАБОЧАЯ ВЕРСИЯ


        // Быстрая оптимизация - РАБОЧАЯ ВЕРСИЯ

        // RFM Анализ
        private void GenerateRFMAnalysis_Click(object sender, RoutedEventArgs e)
        {
            if (RFMStartDate.SelectedDate == null || RFMEndDate.SelectedDate == null)
            {
                MessageBox.Show("Выберите период для RFM анализа!", "Внимание");
                return;
            }

            var startDate = RFMStartDate.SelectedDate.Value;
            var endDate = RFMEndDate.SelectedDate.Value;

            if (startDate > endDate)
            {
                MessageBox.Show("Начальная дата не может быть больше конечной!", "Ошибка");
                return;
            }

            try
            {
                var analysis = AdvancedAnalytics.PerformRFMAnalysis(startDate, endDate);

                var sb = new StringBuilder();
                sb.AppendLine("🏆 RFM АНАЛИЗ КЛИЕНТОВ");
                sb.AppendLine($"Период: {startDate:dd.MM.yyyy} - {endDate:dd.MM.yyyy}");
                sb.AppendLine("=".PadRight(60, '='));
                sb.AppendLine();

                sb.AppendLine("🔝 ТОП-20 КЛИЕНТОВ ПО RFM БАЛЛУ:");
                sb.AppendLine("(R - давность, F - частота, M - сумма)");
                sb.AppendLine();

                int rank = 1;
                foreach (var client in analysis.TopClients)
                {
                    sb.AppendLine($"{rank}. {client.ClientName}");
                    sb.AppendLine($"   Сегмент: {client.Segment}");
                    sb.AppendLine($"   RFM Балл: {client.RFMScore}/12");
                    sb.AppendLine($"   Последняя покупка: {client.RecencyDays} дней назад");
                    sb.AppendLine($"   Всего покупок: {client.Frequency}");
                    sb.AppendLine($"   Общая сумма: {client.Monetary:N0} ₽");
                    sb.AppendLine();
                    rank++;
                }

                sb.AppendLine("📊 РАСПРЕДЕЛЕНИЕ ПО СЕГМЕНТАМ:");
                foreach (var segment in analysis.SegmentDistribution.OrderByDescending(x => x.Value))
                {
                    sb.AppendLine($"   {segment.Key}: {segment.Value} клиентов");
                }

                sb.AppendLine();
                sb.AppendLine($"💰 ПОТЕНЦИАЛЬНАЯ ДОПОЛНИТЕЛЬНАЯ ВЫРУЧКА:");
                sb.AppendLine($"   От VIP клиентов: {analysis.TotalPotentialRevenue:N0} ₽");

                sb.AppendLine();
                sb.AppendLine("💡 РЕКОМЕНДАЦИИ:");
                if (analysis.TopClients.Any(c => c.Segment == "VIP Клиенты"))
                {
                    sb.AppendLine("• VIP клиенты - ваша основа. Уделяйте им особое внимание");
                    sb.AppendLine("• Предлагайте эксклюзивные условия и персональный сервис");
                }

                if (analysis.TopClients.Any(c => c.Segment == "Спящие"))
                {
                    sb.AppendLine("• У вас есть спящие клиенты. Запустите ремаркетинг кампанию");
                }

                sb.AppendLine();
                sb.AppendLine($"📅 Сформировано: {DateTime.Now:dd.MM.yyyy HH:mm}");

                ReportTitle.Text = "RFM Анализ клиентов";
                ReportContent.Text = sb.ToString();
                ReportOutputArea.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка RFM анализа: {ex.Message}", "Ошибка");
            }
        }

        // Прогноз доходов
        private void GenerateRevenueForecast_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var forecast = AdvancedAnalytics.ForecastNextMonthRevenue();

                var sb = new StringBuilder();
                sb.AppendLine("📈 ПРОГНОЗ ДОХОДОВ НА СЛЕДУЮЩИЙ МЕСЯЦ");
                sb.AppendLine($"Дата анализа: {DateTime.Now:dd.MM.yyyy}");
                sb.AppendLine("=".PadRight(60, '='));
                sb.AppendLine();

                if (forecast.ForecastConfidence != "Недостаточно данных")
                {
                    sb.AppendLine($"💰 ПРОГНОЗИРУЕМАЯ ВЫРУЧКА:");
                    sb.AppendLine($"   {forecast.NextMonthForecast:N0} ₽");
                    sb.AppendLine($"   Ожидаемый рост: {forecast.GrowthPercentage:+#;-#;0}%");
                    sb.AppendLine($"   Уверенность прогноза: {forecast.ForecastConfidence}");
                    sb.AppendLine();

                    sb.AppendLine("📊 ФАКТОРЫ, ВЛИЯЮЩИЕ НА ПРОГНОЗ:");
                    foreach (var factor in forecast.Factors)
                    {
                        sb.AppendLine($"   • {factor.Key}: {factor.Value:N1}");
                    }

                    sb.AppendLine();
                    sb.AppendLine("💡 РЕКОМЕНДАЦИИ:");
                    if (forecast.GrowthPercentage > 10)
                        sb.AppendLine("• Высокий рост! Рассмотрите расширение команды");
                    else if (forecast.GrowthPercentage < 0)
                        sb.AppendLine("• Отрицательный рост. Анализируйте причины и корректируйте стратегию");
                    else
                        sb.AppendLine("• Стабильный рост. Продолжайте текущую стратегию");

                    if (forecast.NextMonthForecast < 100000)
                        sb.AppendLine("• Прогнозируется низкая выручка. Усильте активность в продажах");
                }
                else
                {
                    sb.AppendLine("⚠️ Недостаточно данных для точного прогноза");
                    sb.AppendLine("   Для построения прогноза необходимо:");
                    sb.AppendLine("   • Минимум 3 месяца исторических данных");
                    sb.AppendLine("   • Успешные сделки в системе");
                    sb.AppendLine();
                    sb.AppendLine("   Совет: Продолжайте работу с CRM");
                    sb.AppendLine("   После накопления данных прогноз станет доступен");
                }

                sb.AppendLine();
                sb.AppendLine($"📅 Сформировано: {DateTime.Now:dd.MM.yyyy HH:mm}");

                ReportTitle.Text = "Прогноз доходов";
                ReportContent.Text = sb.ToString();
                ReportOutputArea.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка прогнозирования: {ex.Message}", "Ошибка");
            }
        }

        // Анализ эффективности
        private void GenerateEfficiencyAnalysis_Click(object sender, RoutedEventArgs e)
        {
            if (EfficiencyStartDate.SelectedDate == null || EfficiencyEndDate.SelectedDate == null)
            {
                MessageBox.Show("Выберите период для анализа!", "Внимание");
                return;
            }

            var startDate = EfficiencyStartDate.SelectedDate.Value;
            var endDate = EfficiencyEndDate.SelectedDate.Value;

            if (startDate > endDate)
            {
                MessageBox.Show("Начальная дата не может быть больше конечной!", "Ошибка");
                return;
            }

            try
            {
                var analysis = AdvancedAnalytics.AnalyzeTimeEfficiency(startDate, endDate);

                var sb = new StringBuilder();
                sb.AppendLine("⏱️ АНАЛИЗ ЭФФЕКТИВНОСТИ ПРОДАЖ");
                sb.AppendLine($"Период: {startDate:dd.MM.yyyy} - {endDate:dd.MM.yyyy}");
                sb.AppendLine("=".PadRight(60, '='));
                sb.AppendLine();

                sb.AppendLine("📊 ОСНОВНЫЕ ПОКАЗАТЕЛИ:");
                sb.AppendLine($"• Средняя продолжительность сделки: {analysis.AverageDealDurationDays:F1} дней");

                if (analysis.DurationByCategory.Any())
                {
                    sb.AppendLine();
                    sb.AppendLine("⏱️ ПРОДОЛЖИТЕЛЬНОСТЬ ПО КАТЕГОРИЯМ:");
                    foreach (var category in analysis.DurationByCategory.OrderBy(x => x.Value))
                    {
                        sb.AppendLine($"   • {category.Key}: {category.Value:F1} дней");
                    }
                }

                if (analysis.SuccessRateByWeekday.Any())
                {
                    sb.AppendLine();
                    sb.AppendLine("📅 УСПЕШНОСТЬ ПО ДНЯМ НЕДЕЛИ:");
                    foreach (var day in analysis.SuccessRateByWeekday.OrderByDescending(x => x.Value))
                    {
                        sb.AppendLine($"   • {day.Key}: {day.Value:F1}% успеха");
                    }
                }

                if (analysis.Recommendations.Any())
                {
                    sb.AppendLine();
                    sb.AppendLine("💡 РЕКОМЕНДАЦИИ ПО ОПТИМИЗАЦИИ:");
                    foreach (var rec in analysis.Recommendations)
                    {
                        sb.AppendLine($"   • {rec}");
                    }
                }
                else
                {
                    sb.AppendLine();
                    sb.AppendLine("✅ Эффективность на хорошем уровне!");
                }

                sb.AppendLine();
                sb.AppendLine($"📅 Сформировано: {DateTime.Now:dd.MM.yyyy HH:mm}");

                ReportTitle.Text = "Анализ эффективности";
                ReportContent.Text = sb.ToString();
                ReportOutputArea.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка анализа эффективности: {ex.Message}", "Ошибка");
            }
        }

        private void DebugButton_Click(object sender, RoutedEventArgs e)
        {
            
            MessageBox.Show("Отладочные функции временно отключены", "Информация", MessageBoxButton.OK, MessageBoxImage.Information); 
        }

        // Метод для безопасного перехода между окнами
        private void SafeWindowTransition(Window currentWindow, Action transitionAction)
        {
            try
            {
                // Останавливаем все анимации
                currentWindow.BeginAnimation(OpacityProperty, null);

                // Даем время для очистки
                currentWindow.Dispatcher.Invoke(() => { },
                    System.Windows.Threading.DispatcherPriority.ApplicationIdle);

                // Выполняем переход
                transitionAction.Invoke();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка перехода: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // CSAT Анализ
        private void GenerateCSATAnalysis_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var csat = AdvancedAnalytics.CalculateCSAT(DateTime.Now.AddDays(-30), DateTime.Now);

                var sb = new StringBuilder();
                sb.AppendLine("😊 ИНДЕКС УДОВЛЕТВОРЕННОСТИ КЛИЕНТОВ (CSAT)");
                sb.AppendLine($"Дата анализа: {DateTime.Now:dd.MM.yyyy}");
                sb.AppendLine("=".PadRight(60, '='));
                sb.AppendLine();

                sb.AppendLine("📊 ОСНОВНЫЕ ПОКАЗАТЕЛИ:");
                sb.AppendLine($"• Общий CSAT балл: {csat.CSATScore:F1}/100");
                sb.AppendLine($"• Количество респондентов: {csat.RespondentCount}");

                // Визуализация CSAT
                int stars = (int)(csat.CSATScore / 20);
                sb.AppendLine($"• Рейтинг: {new string('★', stars)}{new string('☆', 5 - stars)}");

                if (csat.CSATScore >= 85)
                    sb.AppendLine("• Оценка: Отлично! 🎉");
                else if (csat.CSATScore >= 70)
                    sb.AppendLine("• Оценка: Хорошо 👍");
                else
                    sb.AppendLine("• Оценка: Требует улучшения ⚠️");

                sb.AppendLine();
                sb.AppendLine("📈 ОЦЕНКИ ПО КАТЕГОРИЯМ:");
                foreach (var category in csat.FeedbackByCategory)
                {
                    int categoryStars = category.Value;
                    sb.AppendLine($"   • {category.Key}: {category.Value}/10 {new string('★', categoryStars)}{new string('☆', 10 - categoryStars)}");
                }

                if (csat.ImprovementAreas.Any())
                {
                    sb.AppendLine();
                    sb.AppendLine("🎯 ОБЛАСТИ ДЛЯ УЛУЧШЕНИЯ:");
                    foreach (var area in csat.ImprovementAreas)
                    {
                        sb.AppendLine($"   • {area}");
                    }
                }

                sb.AppendLine();
                sb.AppendLine("💡 РЕКОМЕНДАЦИИ:");
                if (csat.CSATScore < 70)
                {
                    sb.AppendLine("• Внедрите регулярные опросы клиентов");
                    sb.AppendLine("• Улучшите службу поддержки");
                    sb.AppendLine("• Проводите разбор отрицательных отзывов");
                }
                else if (csat.CSATScore < 85)
                {
                    sb.AppendLine("• Продолжайте собирать обратную связь");
                    sb.AppendLine("• Внедрите программу лояльности");
                    sb.AppendLine("• Оптимизируйте слабые места");
                }
                else
                {
                    sb.AppendLine("• Отличные результаты! Делитесь успехами с командой");
                    sb.AppendLine("• Используйте положительные отзывы в маркетинге");
                    sb.AppendLine("• Поддерживайте высокий уровень сервиса");
                }

                sb.AppendLine();
                sb.AppendLine($"📅 Сформировано: {DateTime.Now:dd.MM.yyyy HH:mm}");

                ReportTitle.Text = "Индекс удовлетворенности (CSAT)";
                ReportContent.Text = sb.ToString();
                ReportOutputArea.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка CSAT анализа: {ex.Message}", "Ошибка");
            }
        }

        // Загрузка событий календаря
        private void LoadCalendarEvents(DateTime? date = null)
        {
            using var db = MultiUserSecurityManager.CreateCompanyContext();
            var query = db.CalendarEvents
                .Include(e => e.Deal)
                .OrderBy(e => e.StartDate)
                .AsQueryable();

            if (date.HasValue)
            {
                var day = date.Value.Date;
                query = query.Where(e => e.StartDate.Date == day);
            }

            _calendarEvents = new ObservableCollection<CalendarEvent>(query.ToList());
            CalendarEventsGrid.ItemsSource = _calendarEvents;
        }

        // Открытие формы добавления/редактирования события
        private void AddCalendarEvent_Click(object sender, RoutedEventArgs e)
        {
            var addWin = new AddEventWindow(_calendarSelectedDate);
            addWin.Owner = this;
            if (addWin.ShowDialog() == true)
                LoadCalendarEvents(_calendarSelectedDate);
        }

        private void CalendarEventsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (CalendarEventsGrid.SelectedItem is CalendarEvent selectedEvent)
            {
                var editWin = new AddEventWindow(selectedEvent.StartDate, selectedEvent);
                editWin.Owner = this;
                if (editWin.ShowDialog() == true)
                    LoadCalendarEvents(_calendarSelectedDate);
            }
        }

        private void CalendarDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CalendarDatePicker.SelectedDate.HasValue)
            {
                _calendarSelectedDate = CalendarDatePicker.SelectedDate.Value;
                LoadCalendarEvents(_calendarSelectedDate);
            }
        }

        #region Развертывание и синхронизация

        /// <summary>
        /// Открытие настроек развертывания
        /// </summary>
        private void OpenDeploymentSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var deploymentSettingsWindow = new DeploymentSettingsWindow();
                deploymentSettingsWindow.Owner = this;
                deploymentSettingsWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка открытия настроек развертывания: {ex.Message}", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Открытие журнала синхронизации
        /// </summary>
        private void OpenSyncLog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var syncLogWindow = new SyncLogWindow();
                syncLogWindow.Owner = this;
                syncLogWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка открытия журнала синхронизации: {ex.Message}", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void SyncData_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var deploymentManager = DeploymentManager.Instance;
                var config = deploymentManager.Config;

                if (config.Mode == DeploymentMode.Local)
                {
                    MessageBox.Show("Синхронизация недоступна в локальном режиме.\n\n" +
                                  "Переключитесь в облачный режим в настройках развертывания.",
                                  "Синхронизация", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Показываем индикатор синхронизации
                var button = sender as Button;
                if (button != null)
                {
                    button.Content = "⏳ Загрузка данных...";
                    button.IsEnabled = false;
                }

                MessageBox.Show("Начинаю загрузку данных с облачного сервера...\n" +
                              "Это может занять несколько секунд.",
                              "Загрузка данных", MessageBoxButton.OK, MessageBoxImage.Information);

                // 1. Сначала загружаем данные из облака
                bool loadSuccess = await deploymentManager.LoadFromCloud();
                
                if (loadSuccess)
                {
                    MessageBox.Show("Данные успешно загружены из облака.\n\n" +
                                  "Теперь начинаю синхронизацию локальных изменений...",
                                  "Загрузка завершена", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    var result = MessageBox.Show("Не удалось загрузить данные из облака.\n\n" +
                                               "Продолжить синхронизацию локальных данных?",
                                               "Ошибка загрузки", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (result != MessageBoxResult.Yes)
                    {
                        // Восстанавливаем кнопку
                        if (button != null)
                        {
                            button.Content = "🔄 Синхронизировать";
                            button.IsEnabled = true;
                        }
                        return;
                    }
                }

                // Обновляем текст кнопки для синхронизации
                if (button != null)
                {
                    button.Content = "⏳ Синхронизация...";
                }

                MessageBox.Show("Начинаю синхронизацию данных с облачным сервером...\n" +
                              "Это может занять несколько секунд.",
                              "Синхронизация", MessageBoxButton.OK, MessageBoxImage.Information);

                // 2. Потом синхронизируем локальные данные в облако
                bool syncSuccess = await deploymentManager.SyncAllCompanies();

                // Показываем результат
                string message;
                if (loadSuccess && syncSuccess)
                {
                    message = "✅ Двусторонняя синхронизация завершена успешно!\n\n" +
                             "• Данные загружены из облака\n" +
                             "• Локальные изменения отправлены в облако";
                }
                else if (loadSuccess && !syncSuccess)
                {
                    message = "⚠️ Синхронизация завершена с предупреждениями:\n\n" +
                             "✅ Данные загружены из облака\n" +
                             "❌ Не удалось отправить локальные изменения";
                }
                else if (!loadSuccess && syncSuccess)
                {
                    message = "⚠️ Синхронизация завершена с предупреждениями:\n\n" +
                             "❌ Не удалось загрузить данные из облака\n" +
                             "✅ Локальные изменения отправлены в облако";
                }
                else
                {
                    message = "❌ Синхронизация завершилась с ошибками:\n\n" +
                             "❌ Не удалось загрузить данные из облака\n" +
                             "❌ Не удалось отправить локальные изменения";
                }

                MessageBox.Show(message, "Результат синхронизации", MessageBoxButton.OK, 
                              loadSuccess && syncSuccess ? MessageBoxImage.Information : MessageBoxImage.Warning);

                // Восстанавливаем кнопку
                if (button != null)
                {
                    button.Content = "🔄 Синхронизировать";
                    button.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                // Восстанавливаем кнопку при ошибке
                var button = sender as Button;
                if (button != null)
                {
                    button.Content = "🔄 Синхронизировать";
                    button.IsEnabled = true;
                }

                MessageBox.Show($"❌ Критическая ошибка синхронизации:\n\n{ex.Message}\n\n" +
                              "Проверьте подключение к интернету и повторите попытку.",
                              "Критическая ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Воронка продаж (улучшенная)
        private void GenerateSalesFunnel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var startDate = ReportStartDate.SelectedDate ?? DateTime.Now.AddMonths(-1);
                var endDate = ReportEndDate.SelectedDate ?? DateTime.Now;
                
                var deals = _context.Deals
                    .Where(d => d.CreatedAt >= startDate && d.CreatedAt <= endDate)
                    .Include(d => d.Client)
                    .ToList();

                if (!deals.Any())
                {
                    ReportTitle.Text = "Воронка продаж";
                    ReportContent.Text = "За выбранный период сделки не найдены.";
                    ReportOutputArea.Visibility = Visibility.Visible;
                    return;
                }

                var report = new StringBuilder();
                report.AppendLine("🔺 ВОРОНКА ПРОДАЖ");
                report.AppendLine($"Период: {startDate:dd.MM.yyyy} - {endDate:dd.MM.yyyy}");
                report.AppendLine($"Сформировано: {DateTime.Now:dd.MM.yyyy HH:mm}");
                report.AppendLine("=".PadRight(70, '='));
                report.AppendLine();

                // 1. Распределение по статусам
                var funnelData = new List<FunnelStage>
                {
                    new FunnelStage { Status = "Новые", Count = deals.Count(d => d.Status == DealStatus.New), Icon = "🆕" },
                    new FunnelStage { Status = "В работе", Count = deals.Count(d => d.Status == DealStatus.InProgress), Icon = "⚡" },
                    new FunnelStage { Status = "Успешные", Count = deals.Count(d => d.Status == DealStatus.Successful), Icon = "✅" },
                    new FunnelStage { Status = "Проваленные", Count = deals.Count(d => d.Status == DealStatus.Failed), Icon = "❌" }
                };

                var totalDeals = deals.Count;
                var successfulDeals = deals.Count(d => d.Status == DealStatus.Successful);
                
                report.AppendLine("📊 РАСПРЕДЕЛЕНИЕ СДЕЛОК ПО ЭТАПАМ:");
                foreach (var stage in funnelData.Where(s => s.Count > 0))
                {
                    var percentage = totalDeals > 0 ? (stage.Count * 100.0 / totalDeals) : 0;
                    report.AppendLine($"  {stage.Icon} {stage.Status}: {stage.Count} сделок ({percentage:F1}%)");
                }
                report.AppendLine();

                // 2. Анализ конверсии между этапами
                report.AppendLine("📈 АНАЛИЗ КОНВЕРСИИ:");
                var conversionPairs = new List<(string From, string To, int FromCount, int ToCount, double Rate)>
                {
                    ("Новые", "В работе", funnelData[0].Count, funnelData[1].Count, 0),
                    ("В работе", "Успешные", funnelData[1].Count, funnelData[2].Count, 0)
                };

                for (int i = 0; i < conversionPairs.Count; i++)
                {
                    var pair = conversionPairs[i];
                    pair.Rate = pair.FromCount > 0 ? (pair.ToCount * 100.0 / pair.FromCount) : 0;
                    conversionPairs[i] = pair;
                    
                    var status = pair.Rate switch
                    {
                        >= 70 => "🟢 Отлично",
                        >= 50 => "🟡 Хорошо",
                        >= 30 => "🟠 Нормально",
                        _ => "🔴 Требует внимания"
                    };
                    
                    report.AppendLine($"  {pair.From} → {pair.To}: {pair.Rate:F1}% {status}");
                }
                report.AppendLine();

                // 3. Общая конверсия
                var overallConversion = totalDeals > 0 ? (successfulDeals * 100.0 / totalDeals) : 0;
                var conversionStatus = overallConversion switch
                {
                    >= 40 => "🏆 Отличная",
                    >= 30 => "✅ Хорошая", 
                    >= 20 => "📊 Средняя",
                    >= 10 => "⚠️ Низкая",
                    _ => "🔴 Критическая"
                };
                
                report.AppendLine($"🎯 ОБЩАЯ КОНВЕРСИЯ: {overallConversion:F1}% ({conversionStatus})");
                report.AppendLine($"   Успешных сделок: {successfulDeals} из {totalDeals}");
                report.AppendLine();

                // 4. Финансовые показатели воронки
                var totalValue = deals.Where(d => d.Amount > 0).Sum(d => d.Amount);
                var successfulValue = deals.Where(d => d.Status == DealStatus.Successful && d.Amount > 0).Sum(d => d.Amount);
                var pipelineValue = deals.Where(d => d.Status == DealStatus.InProgress && d.Amount > 0)
                    .Sum(d => d.Amount * d.Probability / 100m);

                report.AppendLine("💰 ФИНАНСОВЫЕ ПОКАЗАТЕЛИ:");
                report.AppendLine($"   Общая стоимость воронки: {totalValue:N0} ₽");
                report.AppendLine($"   Заработано: {successfulValue:N0} ₽");
                report.AppendLine($"   Потенциал воронки: {pipelineValue:N0} ₽");
                report.AppendLine($"   Средний чек: {deals.Where(d => d.Amount > 0).DefaultIfEmpty().Average(d => d?.Amount ?? 0):N0} ₽");
                report.AppendLine();

                // 5. Анализ по категориям
                var categoryAnalysis = deals
                    .Where(d => !string.IsNullOrEmpty(d.Category))
                    .GroupBy(d => d.Category)
                    .Select(g => new
                    {
                        Category = g.Key,
                        Total = g.Count(),
                        Successful = g.Count(d => d.Status == DealStatus.Successful),
                        Value = g.Sum(d => d.Amount),
                        SuccessRate = g.Count() > 0 ? (g.Count(d => d.Status == DealStatus.Successful) * 100.0 / g.Count()) : 0
                    })
                    .OrderByDescending(x => x.SuccessRate)
                    .Take(5)
                    .ToList();

                if (categoryAnalysis.Any())
                {
                    report.AppendLine("📂 АНАЛИЗ ПО КАТЕГОРИЯМ:");
                    foreach (var cat in categoryAnalysis)
                    {
                        var icon = cat.SuccessRate >= 40 ? "🟢" : cat.SuccessRate >= 25 ? "🟡" : "🔴";
                        report.AppendLine($"   {icon} {cat.Category}: {cat.Successful}/{cat.Total} ({cat.SuccessRate:F1}%) - {cat.Value:N0} ₽");
                    }
                    report.AppendLine();
                }

                // 6. Временной анализ
                var avgDealDuration = deals
                    .Where(d => d.Status == DealStatus.Successful && d.ClosedAt.HasValue)
                    .Select(d => (d.ClosedAt.Value - d.CreatedAt).TotalDays)
                    .DefaultIfEmpty(0)
                    .Average();

                report.AppendLine("⏱️ ВРЕМЕННЫЕ ПОКАЗАТЕЛИ:");
                report.AppendLine($"   Средняя длительность сделки: {avgDealDuration:F1} дней");
                
                var fastestDeals = deals
                    .Where(d => d.Status == DealStatus.Successful && d.ClosedAt.HasValue)
                    .OrderBy(d => d.ClosedAt.Value - d.CreatedAt)
                    .Take(3)
                    .ToList();

                if (fastestDeals.Any())
                {
                    report.AppendLine("   Самые быстрые сделки:");
                    foreach (var deal in fastestDeals)
                    {
                        var duration = (deal.ClosedAt.Value - deal.CreatedAt).TotalDays;
                        report.AppendLine($"     • {deal.Title} - {duration:F0} дн. ({deal.Amount:N0} ₽)");
                    }
                }
                report.AppendLine();

                // 7. Рекомендации
                report.AppendLine("💡 РЕКОМЕНДАЦИИ:");
                var recommendations = new List<string>();

                if (overallConversion < 20)
                    recommendations.Add("🔴 Критически низкая конверсия! Проанализируйте весь процесс продаж");
                else if (overallConversion < 30)
                    recommendations.Add("🟡 Низкая конверсия. Улучшите квалификацию лидов и скрипты продаж");

                var worstConversion = conversionPairs.OrderBy(p => p.Rate).FirstOrDefault();
                if (worstConversion.Rate < 30)
                {
                    recommendations.Add($"📊 Наибольшие потери на этапе '{worstConversion.From} → {worstConversion.To}' ({worstConversion.Rate:F1}%)");
                }

                if (avgDealDuration > 45)
                    recommendations.Add("⏰ Сделки длятся слишком долго. Ускорите процесс согласования");

                if (pipelineValue > totalValue * 0.7m)
                    recommendations.Add("💰 Большая часть воронки в работе. Сфокусируйтесь на закрытии текущих сделок");

                if (!recommendations.Any())
                    recommendations.Add("✅ Воронка работает эффективно. Продолжайте в том же духе!");

                foreach (var rec in recommendations)
                    report.AppendLine($"   {rec}");

                ReportTitle.Text = "Воронка продаж (детальный анализ)";
                ReportContent.Text = report.ToString();
                ReportOutputArea.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при формировании воронки продаж: {ex.Message}", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Вспомогательный класс для этапов воронки
        private class FunnelStage
        {
            public string Status { get; set; } = "";
            public int Count { get; set; }
            public string Icon { get; set; } = "";
        }

        // ABC Анализ 
        private void GenerateABCAnalysis_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var analysis = AdvancedAnalytics.AdvancedABCAnalysis.PerformAdvancedABCAnalysis();

                var report = new StringBuilder();
                report.AppendLine("🚀 ПРОДВИНУТЫЙ ABC-АНАЛИЗ КЛИЕНТОВ");
                report.AppendLine($"Сформировано: {DateTime.Now:dd.MM.yyyy HH:mm}");
                report.AppendLine($"Период анализа: {DateTime.Now.AddMonths(-12):dd.MM.yyyy} - {DateTime.Now:dd.MM.yyyy}");
                report.AppendLine("=".PadRight(80, '='));
                report.AppendLine();

                // Общая статистика
                report.AppendLine("📊 ОБЩАЯ СТАТИСТИКА:");
                report.AppendLine($"  Всего клиентов: {analysis.TotalClients:N0}");
                report.AppendLine($"  Общая выручка: {analysis.TotalRevenue:N0} ₽");
                report.AppendLine($"  Средний доход на клиента: {analysis.AverageRevenuePerClient:N0} ₽");
                report.AppendLine($"  Потенциал роста: {analysis.PotentialRevenueIncrease:N0} ₽");
                report.AppendLine($"  Клиентов в группе риска: {analysis.AtRiskClientsCount}");
                report.AppendLine();

                // Распределение по категориям с детальной статистикой
                report.AppendLine("🎯 РАСПРЕДЕЛЕНИЕ ПО КАТЕГОРИЯМ:");
                foreach (var category in new[] { "A", "B", "C" })
                {
                    if (analysis.ClientsByCategory.ContainsKey(category))
                    {
                        var clients = analysis.ClientsByCategory[category];
                        var revenue = analysis.RevenueByCategory[category];
                        var count = analysis.ClientCountByCategory[category];
                        var efficiency = analysis.CategoryEfficiency.GetValueOrDefault(category, 0);
                        var avgRevenue = revenue / count;
                        var avgDeals = (decimal)clients.Average(c => c.DealCount);
                        var avgLoyalty = (decimal)clients.Average(c => c.LoyaltyScore);

                        report.AppendLine($"  Группа {category} ({GetCategoryTitle(category)}):");
                        report.AppendLine($"    Клиентов: {count} ({count * 100 / analysis.TotalClients:F1}%)");
                        report.AppendLine($"    Выручка: {revenue:N0} ₽ ({revenue / analysis.TotalRevenue * 100:F1}%)");
                        report.AppendLine($"    Средний чек: {avgRevenue:N0} ₽");
                        report.AppendLine($"    Средних сделок: {avgDeals:F1}");
                        report.AppendLine($"    Лояльность: {avgLoyalty:F1}/100");
                        report.AppendLine($"    Эффективность: {efficiency * 100:F1}%");
                        report.AppendLine();
                    }
                }

                // Топ-5 клиентов в каждой категории
                report.AppendLine("👑 ТОП-КЛИЕНТЫ ПО КАТЕГОРИЯМ:");
                foreach (var category in new[] { "A", "B", "C" })
                {
                    if (analysis.ClientsByCategory.ContainsKey(category))
                    {
                        var topClients = analysis.ClientsByCategory[category]
                            .Take(3)
                            .ToList();

                        if (topClients.Any())
                        {
                            report.AppendLine($"  Группа {category}:");
                            foreach (var client in topClients)
                            {
                                var riskIcon = client.RiskLevel switch
                                {
                                    "Высокий" => "🚨",
                                    "Средний" => "⚠️",
                                    _ => "✅"
                                };

                                report.AppendLine($"    {riskIcon} {client.ClientName}");
                                report.AppendLine($"       💰 {client.TotalRevenue:N0} ₽ | 📈 {client.DealCount} сделок | ⭐ {client.LoyaltyScore}/100");
                                report.AppendLine($"       📅 Последняя сделка: {client.LastDealDate:dd.MM.yyyy} ({client.DaysSinceLastDeal} дн. назад)");
                                
                                if (client.Recommendations.Any())
                                {
                                    report.AppendLine($"       💡 {string.Join("; ", client.Recommendations.Take(2))}");
                                }
                                report.AppendLine();
                            }
                        }
                    }
                }

                // Стратегические инсайты
                if (analysis.StrategicInsights.Any())
                {
                    report.AppendLine("🧠 СТРАТЕГИЧЕСКИЕ ИНСАЙТЫ:");
                    foreach (var insight in analysis.StrategicInsights)
                    {
                        report.AppendLine($"  • {insight}");
                    }
                    report.AppendLine();
                }

                // Персонализированные рекомендации
                if (analysis.ActionableRecommendations.Any())
                {
                    report.AppendLine("🎯 ПЕРСОНАЛИЗИРОВАННЫЕ РЕКОМЕНДАЦИИ:");
                    foreach (var recommendation in analysis.ActionableRecommendations)
                    {
                        report.AppendLine($"  • {recommendation}");
                    }
                    report.AppendLine();
                }

                // Обновляем категории в базе данных
                foreach (var category in analysis.ClientsByCategory)
                {
                    foreach (var client in category.Value)
                    {
                        var dbClient = _context.Clients.FirstOrDefault(c => c.Name == client.ClientName);
                        if (dbClient != null)
                        {
                            dbClient.ABC_Category = client.Category;
                        }
                    }
                }

                _context.SaveChanges();
                ReportTitle.Text = "🚀 Продвинутый ABC-Анализ";
                ReportContent.Text = report.ToString();
                ReportOutputArea.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при проведении ABC анализа: {ex.Message}", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetCategoryTitle(string category)
        {
            return category switch
            {
                "A" => "VIP-клиенты",
                "B" => "Стабильные клиенты", 
                "C" => "Массовые клиенты",
                _ => "Неизвестно"
            };
        }

        #endregion

        #region Вспомогательные методы для отображения информации о пользователях

        /// <summary>
        /// Форматирует информацию о создателе записи
        /// </summary>
        private string FormatCreatedByInfo(int? userId, DateTime createdAt)
        {
            try
            {
                if (userId.HasValue)
                {
                    // Ищем пользователя в ГЛОБАЛЬНОЙ базе данных, а не в базе компании
                    using (var globalContext = new GlobalDbContext())
                    {
                        var user = globalContext.Users.FirstOrDefault(u => u.Id == userId.Value);
                        if (user != null)
                        {
                            return $"Создал: {user.FullName} • {createdAt:dd.MM.yyyy HH:mm}";
                        }
                    }
                }
            }
            catch { }
            
            return $"Создан: {createdAt:dd.MM.yyyy HH:mm}";
        }

        /// <summary>
        /// Форматирует информацию об обновлении записи
        /// </summary>
        private string FormatUpdatedInfo(int? userId, DateTime? updatedAt)
        {
            if (!updatedAt.HasValue) return "";
            
            try
            {
                if (userId.HasValue)
                {
                    // Ищем пользователя в ГЛОБАЛЬНОЙ базе данных
                    using (var globalContext = new GlobalDbContext())
                    {
                        var user = globalContext.Users.FirstOrDefault(u => u.Id == userId.Value);
                        if (user != null)
                        {
                            return $"Изменил: {user.FullName} • {updatedAt.Value:dd.MM.yyyy HH:mm}";
                        }
                    }
                }
            }
            catch { }
            
            return $"Изменен: {updatedAt.Value:dd.MM.yyyy HH:mm}";
        }

        /// <summary>
        /// Форматирует информацию о ответственном пользователе
        /// </summary>
        private string FormatAssignedToInfo(int? userId)
        {
            if (!userId.HasValue) return "";
            
            try
            {
                // Ищем пользователя в ГЛОБАЛЬНОЙ базе данных
                using (var globalContext = new GlobalDbContext())
                {
                    var user = globalContext.Users.FirstOrDefault(u => u.Id == userId.Value);
                    if (user != null)
                    {
                        return user.FullName;
                    }
                }
            }
            catch { }
            
            return "";
        }

        /// <summary>
        /// Получает полное имя пользователя по ID
        /// </summary>
        private string GetUserName(int? userId)
        {
            if (!userId.HasValue) return "";
            
            try
            {
                var user = _context.Users.FirstOrDefault(u => u.Id == userId.Value);
                return user?.FullName ?? "";
            }
            catch { }
            
            return "";
        }

        /// <summary>
        /// Удаление всех данных программы
        /// </summary>
        private void DeleteAllProgramData_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "⚠️ ВНИМАНИЕ! Это действие удалит все данные CRM:\n\n" +
                "• Всех клиентов и сделки\n" +
                "• Все расходы и доходы\n" +
                "• Все события календаря\n" +
                "• Все задачи и уведомления\n" +
                "• Все настройки и логи\n\n" +
                "Пользователи и компании НЕ будут удалены.\n" +
                "Это действие НЕВОЗМОЖНО отменить!\n\n" +
                "Вы уверены, что хотите удалить все данные?",
                "Удаление данных CRM",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // Второе подтверждение
                    var confirmResult = MessageBox.Show(
                        "Последнее предупреждение!\n\n" +
                        "После удаления все данные будут потеряны навсегда.\n" +
                        "Программа будет перезапущена после удаления.\n" +
                        "Продолжить удаление?",
                        "Подтверждение удаления",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Exclamation);

                    if (confirmResult == MessageBoxResult.Yes)
                    {
                        DataManager.DeleteAllCRMData();
                        MessageBox.Show(
                            "✅ Все данные успешно удалены!\n\n" +
                            "Программа будет перезапущена.",
                            "Удаление завершено",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);

                        // Правильный перезапуск приложения
                        var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
                        var startInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = currentProcess.MainModule.FileName,
                            WorkingDirectory = Environment.CurrentDirectory
                        };
                        
                        System.Diagnostics.Process.Start(startInfo);
                        Application.Current.Shutdown();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"❌ Ошибка при удалении данных:\n{ex.Message}",
                        "Ошибка",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        #endregion
    }
}
    
