using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using LiveCharts;
using LiveCharts.Wpf;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.IO.Compression;
using System.Text;
using OfficeOpenXml;

namespace MyFirstCRM
{
    public partial class AdvancedReportsWindow : Window
    {
        public SeriesCollection SalesSeries { get; set; }
        public SeriesCollection FunnelSeries { get; set; }
        public SeriesCollection ClientSeries { get; set; }
        public SeriesCollection CategorySeries { get; set; }
        public List<string> Months { get; set; }
        public List<string> ClientCategories { get; set; }

        public AdvancedReportsWindow()
        {
            InitializeComponent();
            Loaded += AdvancedReportsWindow_Loaded;
            InitializeCharts();
            LoadData();
        }

        private void AdvancedReportsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Анимация появления
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.5));
            this.BeginAnimation(OpacityProperty, fadeIn);
        }

        private void InitializeCharts()
        {
            SalesSeries = new SeriesCollection();
            FunnelSeries = new SeriesCollection();
            ClientSeries = new SeriesCollection();
            CategorySeries = new SeriesCollection();

            DataContext = this;
        }

        private void LoadData()
        {
            using var context = MultiUserSecurityManager.CreateCompanyContext();

            var (startDate, endDate) = GetSelectedPeriodRange();

            Exception? firstError = null;

            void Safe(string section, Action action)
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    firstError ??= new Exception($"{section}: {ex.Message}", ex);
                }
            }

            Safe("Метрики", () => LoadMetrics(context, startDate, endDate));
            Safe("График продаж", () => LoadSalesChart(context));
            Safe("Воронка", () => LoadFunnelChart(context));
            Safe("Клиенты", () => LoadClientChart(context));
            Safe("Категории", () => LoadCategoryChart(context));
            Safe("Топ клиентов", () => LoadTopClients(context));
            LoadActiveDeals(context);
            LoadABCAnalysis(context);

            if (firstError != null)
            {
                MessageBox.Show($"Некоторые блоки отчета не загрузились.\n\n{firstError.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private (DateTime startDate, DateTime endDate) GetSelectedPeriodRange()
        {
            var endDate = DateTime.Now;

            // Защита от отсутствия элемента (например, при дизайнере)
            int idx = PeriodComboBox?.SelectedIndex ?? 0;

            DateTime startDate = idx switch
            {
                0 => endDate.AddDays(-7),   // За неделю
                1 => endDate.AddMonths(-1), // За месяц
                2 => endDate.AddMonths(-3), // За квартал
                3 => endDate.AddYears(-1),  // За год
                _ => new DateTime(2000, 1, 1) // Все время
            };

            return (startDate, endDate);
        }

        private (DateTime startDate, DateTime endDate) GetPreviousPeriodRange(DateTime startDate, DateTime endDate)
        {
            int idx = PeriodComboBox?.SelectedIndex ?? 0;

            return idx switch
            {
                0 => (startDate.AddDays(-7), endDate.AddDays(-7)),
                1 => (startDate.AddMonths(-1), endDate.AddMonths(-1)),
                2 => (startDate.AddMonths(-3), endDate.AddMonths(-3)),
                3 => (startDate.AddYears(-1), endDate.AddYears(-1)),
                _ => (new DateTime(2000, 1, 1), startDate.AddDays(-1))
            };
        }

        private void LoadMetrics(AppDbContext context, DateTime startDate, DateTime endDate)
        {
            // Выручка = сумма успешных сделок за период
            var successfulDeals = context.Deals
                .Where(d => d.Status == DealStatus.Successful && d.CreatedAt >= startDate && d.CreatedAt <= endDate)
                .ToList();

            decimal totalRevenue = successfulDeals.Sum(d => d.Amount);

            // Расходы = сумма расходов за период (из раздела "Финансы")
            decimal totalExpenses = 0;
            try
            {
                // SQLite не всегда умеет SUM по decimal на стороне БД — суммируем на клиенте
                totalExpenses = context.Expenses
                    .Where(e => e.Date >= startDate && e.Date <= endDate)
                    .Select(e => e.Amount)
                    .ToList()
                    .Sum();
            }
            catch
            {
                totalExpenses = 0;
            }

            decimal netProfit = totalRevenue - totalExpenses;
            decimal profitMargin = totalRevenue > 0 ? (netProfit / totalRevenue * 100) : 0;

            // Конверсия за период
            var totalDeals = context.Deals.Count(d => d.CreatedAt >= startDate && d.CreatedAt <= endDate);
            var successfulCount = successfulDeals.Count;
            decimal conversionRate = totalDeals > 0 ? (successfulCount * 100m / totalDeals) : 0;

            // Изменение выручки к прошлому периоду
            var (prevStart, prevEnd) = GetPreviousPeriodRange(startDate, endDate);
            // SQLite не всегда умеет SUM по decimal на стороне БД — суммируем на клиенте
            decimal prevRevenue = context.Deals
                .Where(d => d.Status == DealStatus.Successful && d.CreatedAt >= prevStart && d.CreatedAt <= prevEnd)
                .Select(d => d.Amount)
                .ToList()
                .Sum();

            decimal revenueChangePercent = prevRevenue > 0 ? ((totalRevenue - prevRevenue) / prevRevenue * 100) : 0;
            RevenueChange.Text = revenueChangePercent >= 0 ? $"+{revenueChangePercent:F1}%" : $"{revenueChangePercent:F1}%";
            RevenueChange.Foreground = new SolidColorBrush(revenueChangePercent >= 0 ? Colors.LightGreen : Colors.IndianRed);

            RevenueMetric.Text = $"{totalRevenue:N0} ₽";
            ProfitMetric.Text = $"{netProfit:N0} ₽";
            ProfitMetric.Foreground = new SolidColorBrush(netProfit >= 0 ? Colors.White : Colors.IndianRed);
            ProfitMargin.Text = $"{profitMargin:F1}% рентабельность";
            ConversionMetric.Text = $"{conversionRate:F1}%";
            
            // Расчет реального индекса удовлетворенности клиентов (CSAT)
            var csatData = AdvancedAnalytics.CalculateCSAT(startDate, endDate);
            CSATMetric.Text = $"{csatData.CSATScore:F0}/100";
            
            // Обновляем звездочки в зависимости от CSAT
            UpdateCSATStars(csatData.CSATScore);

            AnimateMetric(RevenueMetric, totalRevenue);
            AnimateMetric(ProfitMetric, netProfit);
        }

        
        /// <summary>
        /// Обновление визуального отображения звездочек рейтинга CSAT
        /// </summary>
        private void UpdateCSATStars(decimal csatScore)
        {
            try
            {
                // Находим TextBlock со звездочками и рейтингом в CSAT карточке
                // Ищем по родительским элементам от CSATMetric
                var parent = CSATMetric.Parent as StackPanel;
                if (parent?.Parent is StackPanel grandParent && grandParent.Children.Count > 1)
                {
                    var starsPanel = grandParent.Children[1] as StackPanel;
                    if (starsPanel != null && starsPanel.Children.Count >= 2)
                    {
                        var starsBlock = starsPanel.Children[0] as TextBlock;
                        var ratingBlock = starsPanel.Children[1] as TextBlock;

                        if (starsBlock != null && ratingBlock != null)
                        {
                            // Рассчитываем количество заполненных звезд (0-5)
                            int filledStars = (int)Math.Round(csatScore / 20); // 100/5 = 20
                            filledStars = Math.Max(0, Math.Min(5, filledStars));

                            // Создаем строку со звездочками
                            string stars = "";
                            for (int i = 0; i < 5; i++)
                            {
                                stars += i < filledStars ? "★" : "☆";
                            }

                            // Обновляем звездочки и числовой рейтинг
                            starsBlock.Text = stars;
                            ratingBlock.Text = $" ({csatScore / 20:F1})";

                            // Меняем цвет в зависимости от рейтинга
                            var starColor = csatScore switch
                            {
                                >= 80 => "#10B981", // Зеленый - отличный
                                >= 60 => "#F59E0B", // Желтый - хороший  
                                >= 40 => "#FB923C", // Оранжевый - средний
                                _ => "#EF4444"      // Красный - низкий
                            };

                            starsBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(starColor));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating CSAT stars: {ex.Message}");
            }
        }

        private async void UploadToYandexDisk_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var settings = AppSettings.Load();
                if (string.IsNullOrEmpty(settings.YandexDiskToken))
                {
                    MessageBox.Show("Не указан токен Яндекс.Диска. Пожалуйста, настройте интеграцию в разделе Настройки.",
                        "Требуется настройка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string employee = string.IsNullOrEmpty(settings.EmployeeName) ? "Сотрудник" : settings.EmployeeName;

                // Создаём временную папку
                string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                Directory.CreateDirectory(tempDir);

                try
                {
                    // 1. Статистика
                    string statsFile = Path.Combine(tempDir, "Статистика.xlsx");
                    StatisticsExporter.ExportStatistics(statsFile);

                    // 2. Клиенты
                    string clientsFile = Path.Combine(tempDir, "Клиенты.xlsx");
                    using (var context = MultiUserSecurityManager.CreateCompanyContext())
                    {
                        var clients = context.Clients.ToList();
                        ExcelExporter.ExportClientsToExcel(clients, clientsFile);
                    }

                    // 3. Сделки
                    string dealsFile = Path.Combine(tempDir, "Сделки.xlsx");
                    using (var context = MultiUserSecurityManager.CreateCompanyContext())
                    {
                        var deals = context.Deals.Include(d => d.Client).ToList();
                        ExportDealsToExcel(deals, dealsFile);  // метод из п.2.1
                    }

                    // 3.1 Расходы (финансы)
                    string expensesFile = Path.Combine(tempDir, "Расходы.xlsx");
                    using (var context = MultiUserSecurityManager.CreateCompanyContext())
                    {
                        var expenses = context.Expenses
                            .OrderByDescending(e => e.Date)
                            .ToList();
                        ExportExpensesToExcel(expenses, expensesFile);
                    }

                    // 4. Информация о сотруднике
                    string infoFile = Path.Combine(tempDir, "Сотрудник.txt");
                    CreateEmployeeInfoFile(infoFile, employee); // метод из п.2.2

                    // Создаём ZIP-архив
                    string zipFile = Path.Combine(Path.GetTempPath(), $"CRM_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.zip");
                    if (File.Exists(zipFile)) File.Delete(zipFile);
                    ZipFile.CreateFromDirectory(tempDir, zipFile);

                    // Путь на Яндекс.Диске
                    string folder = "/Отчеты сотрудников";
                    string remoteFileName = $"CRM_Данные_{employee}_{DateTime.Now:yyyy-MM-dd}.zip";
                    string remotePath = $"{folder}/{remoteFileName}";

                    // Создаём папку на диске (если нет)
                    await YandexDiskHelper.CreateFolderAsync(settings.YandexDiskToken, folder);

                    // Загружаем архив
                    bool success = await YandexDiskHelper.UploadFileAsync(settings.YandexDiskToken, zipFile, remotePath);

                    // Удаляем временные файлы
                    Directory.Delete(tempDir, true);
                    if (File.Exists(zipFile)) File.Delete(zipFile);

                    if (success)
                    {
                        MessageBox.Show($"Архив с данными успешно загружен на Яндекс.Диск!\nПуть: {remotePath}",
                            "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch
                {
                    // Очистка в случае ошибки
                    if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
                    throw;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        // Метод для экспорта сделок в Excel (если нет в другом месте)
        private void ExportDealsToExcel(List<Deal> deals, string filePath)
        {
            using (var package = new OfficeOpenXml.ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Сделки");
                worksheet.Cells[1, 1].Value = "ID";
                worksheet.Cells[1, 2].Value = "Название";
                worksheet.Cells[1, 3].Value = "Клиент";
                worksheet.Cells[1, 4].Value = "Сумма";
                worksheet.Cells[1, 5].Value = "Статус";
                worksheet.Cells[1, 6].Value = "Приоритет";
                worksheet.Cells[1, 7].Value = "Вероятность";
                worksheet.Cells[1, 8].Value = "Срок";
                worksheet.Cells[1, 9].Value = "Дата создания";
                worksheet.Cells[1, 10].Value = "Категория";

                int row = 2;
                foreach (var deal in deals)
                {
                    worksheet.Cells[row, 1].Value = deal.Id;
                    worksheet.Cells[row, 2].Value = deal.Title;
                    worksheet.Cells[row, 3].Value = deal.Client?.Name ?? "Не указан";
                    worksheet.Cells[row, 4].Value = deal.Amount;
                    worksheet.Cells[row, 5].Value = deal.Status.ToString();
                    worksheet.Cells[row, 6].Value = deal.Priority.ToString();
                    worksheet.Cells[row, 7].Value = deal.Probability;
                    worksheet.Cells[row, 8].Value = deal.Deadline.ToString("dd.MM.yyyy");
                    worksheet.Cells[row, 9].Value = deal.CreatedAt.ToString("dd.MM.yyyy HH:mm");
                    worksheet.Cells[row, 10].Value = deal.Category ?? "";
                    row++;
                }

                worksheet.Cells[1, 1, row - 1, 10].AutoFitColumns();
                package.SaveAs(new System.IO.FileInfo(filePath));
            }
        }

        private void ExportExpensesToExcel(List<Expense> expenses, string filePath)
        {
            using (var package = new OfficeOpenXml.ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Расходы");
                worksheet.Cells[1, 1].Value = "ID";
                worksheet.Cells[1, 2].Value = "Дата";
                worksheet.Cells[1, 3].Value = "Наименование";
                worksheet.Cells[1, 4].Value = "Категория";
                worksheet.Cells[1, 5].Value = "Сумма";
                worksheet.Cells[1, 6].Value = "Примечание";

                int row = 2;
                foreach (var e in expenses)
                {
                    worksheet.Cells[row, 1].Value = e.Id;
                    worksheet.Cells[row, 2].Value = e.Date.ToString("dd.MM.yyyy");
                    worksheet.Cells[row, 3].Value = e.Title;
                    worksheet.Cells[row, 4].Value = e.Category;
                    worksheet.Cells[row, 5].Value = e.Amount;
                    worksheet.Cells[row, 6].Value = e.Notes ?? "";
                    row++;
                }

                worksheet.Cells[1, 1, row - 1, 6].AutoFitColumns();
                package.SaveAs(new System.IO.FileInfo(filePath));
            }
        }

        // Метод для создания текстового файла с информацией о сотруднике
        private void CreateEmployeeInfoFile(string filePath, string employeeName)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Информация о сотруднике");
            sb.AppendLine("========================");
            sb.AppendLine($"ФИО: {employeeName}");
            sb.AppendLine($"Дата выгрузки: {DateTime.Now:dd.MM.yyyy HH:mm:ss}");
            sb.AppendLine($"Название CRM: DiplomCRM");
            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }


        private void LoadSalesChart(AppDbContext context)
        {
            // Загружаем в память перед группировкой
            var allDeals = context.Deals
                .Where(d => d.Status == DealStatus.Successful)
                .ToList();

            var monthlyData = allDeals
                .GroupBy(d => new { d.CreatedAt.Year, d.CreatedAt.Month })
                .Select(g => new
                {
                    Month = new DateTime(g.Key.Year, g.Key.Month, 1),
                    Revenue = g.Sum(d => d.Amount)
                })
                .OrderBy(d => d.Month)
                .Take(12)
                .ToList();

            Months = monthlyData.Select(d => d.Month.ToString("MMM yy")).ToList();
            var values = new ChartValues<decimal>(monthlyData.Select(d => d.Revenue / 1000));

            SalesSeries.Clear();
            SalesSeries.Add(new LineSeries
            {
                Title = "Выручка",
                Values = values,
                Stroke = new SolidColorBrush(Colors.White),
                Fill = Brushes.Transparent,
                PointGeometrySize = 8,
                LineSmoothness = 0.7
            });
        }

        private void LoadFunnelChart(AppDbContext context)
        {
            var funnelData = new[]
            {
                new { Stage = "Новые", Count = context.Deals.Count(d => d.Status == DealStatus.New) },
                new { Stage = "В работе", Count = context.Deals.Count(d => d.Status == DealStatus.InProgress) },
                new { Stage = "Успешные", Count = context.Deals.Count(d => d.Status == DealStatus.Successful) },
                new { Stage = "Проваленные", Count = context.Deals.Count(d => d.Status == DealStatus.Failed) }
            };

            FunnelSeries.Clear();
            foreach (var item in funnelData)
            {
                FunnelSeries.Add(new PieSeries
                {
                    Title = item.Stage,
                    Values = new ChartValues<int> { item.Count },
                    DataLabels = true,
                    LabelPoint = chartPoint => $"{chartPoint.Y} ({chartPoint.Participation:P0})"
                });
            }
        }

        private void LoadClientChart(AppDbContext context)
        {
            // EF Core не всегда умеет переводить выражения с context.Deals внутри Select по Clients.
            // Делаем безопасно через отдельную агрегацию.
            var clients = context.Clients.ToList();
            var dealCounts = context.Deals
                .GroupBy(d => d.ClientId)
                .Select(g => new { ClientId = g.Key, DealCount = g.Count() })
                .ToList()
                .ToDictionary(x => x.ClientId, x => x.DealCount);

            int GetDealCount(int clientId) => dealCounts.TryGetValue(clientId, out var c) ? c : 0;

            var vipCount = clients.Count(c => GetDealCount(c.Id) >= 10);
            var activeCount = clients.Count(c => GetDealCount(c.Id) >= 3 && GetDealCount(c.Id) <= 9);
            var periodicCount = clients.Count(c => GetDealCount(c.Id) >= 1 && GetDealCount(c.Id) <= 2);
            var newCount = clients.Count(c => GetDealCount(c.Id) == 0);

            ClientCategories = new List<string> { "VIP", "Активные", "Периодические", "Новые" };
            var counts = new List<int> { vipCount, activeCount, periodicCount, newCount };

            ClientSeries.Clear();
            ClientSeries.Add(new ColumnSeries
            {
                Title = "Клиенты",
                Values = new ChartValues<int>(counts)
            });
        }

        private void LoadCategoryChart(AppDbContext context)
        {
            // Загружаем в память перед группировкой
            var allDeals = context.Deals
                .Where(d => d.Status == DealStatus.Successful)
                .ToList();

            var categoryData = allDeals
                .GroupBy(d => d.Category != null ? d.Category : "Без категории")
                .Select(g => new
                {
                    Category = g.Key,
                    Revenue = g.Sum(d => d.Amount)
                })
                .OrderByDescending(c => c.Revenue)
                .Take(5)
                .ToList();

            CategorySeries.Clear();
            foreach (var item in categoryData)
            {
                CategorySeries.Add(new PieSeries
                {
                    Title = item.Category,
                    Values = new ChartValues<decimal> { item.Revenue },
                    DataLabels = true,
                    LabelPoint = chartPoint => $"{chartPoint.Y:N0} ₽"
                });
            }
        }

        private void LoadTopClients(AppDbContext context)
        {
            // Загружаем в память перед группировкой
            var allDeals = context.Deals
                .Where(d => d.Status == DealStatus.Successful)
                .Include(d => d.Client)
                .ToList();

            var topClients = allDeals
                .GroupBy(d => d.Client)
                .Select(g => new
                {
                    Client = g.Key,
                    DealCount = g.Count(),
                    TotalAmount = g.Sum(d => d.Amount),
                    Profit = g.Sum(d => d.Amount) * 0.25m
                })
                .OrderByDescending(c => c.TotalAmount)
                .Take(10)
                .ToList()
                .Select((c, i) => new TopClientViewModel
                {
                    Name = c.Client != null ? c.Client.Name : "Неизвестный",
                    DealCount = c.DealCount,
                    TotalAmount = c.TotalAmount,
                    Profit = c.Profit,
                    Rating = Math.Round(new Random().NextDouble() * 2 + 3, 1),
                    Rank = i + 1
                })
                .ToList();

            TopClientsGrid.ItemsSource = topClients;
        }

        private void LoadActiveDeals(AppDbContext context)
        {
            var activeDeals = context.Deals
                .Where(d => d.Status == DealStatus.InProgress || d.Status == DealStatus.New)
                .Include(d => d.Client)
                .Take(10)
                .ToList()
                .Select(d => new ActiveDealViewModel
                {
                    Title = d.Title,
                    Client = d.Client != null ? d.Client.Name : "Без клиента",
                    Amount = d.Amount,
                    Status = d.Status.ToString(),
                    Progress = d.Probability,
                    Deadline = d.Deadline
                })
                .ToList();

            ActiveDealsGrid.ItemsSource = activeDeals;
        }

        private void LoadABCAnalysis(AppDbContext context)
        {
            try
            {
                var abcAnalysis = AdvancedAnalytics.AdvancedABCAnalysis.PerformAdvancedABCAnalysis();
                
                var abcViewModels = new List<ABCClientViewModel>();
                
                foreach (var category in abcAnalysis.ClientsByCategory)
                {
                    foreach (var client in category.Value)
                    {
                        var viewModel = new ABCClientViewModel
                        {
                            ClientName = client.ClientName,
                            Category = client.Category,
                            TotalRevenue = client.TotalRevenue,
                            DealCount = client.DealCount,
                            AverageDealSize = client.AverageDealSize,
                            LoyaltyScore = client.LoyaltyScore,
                            RiskLevel = client.RiskLevel,
                            DaysSinceLastDeal = client.DaysSinceLastDeal,
                            ConversionRate = client.ConversionRate,
                            CategoryColor = GetCategoryColor(client.Category),
                            RiskColor = GetRiskColor(client.RiskLevel)
                        };
                        
                        abcViewModels.Add(viewModel);
                    }
                }
                
                // Сортируем по категории и выручке
                var sortedViewModels = abcViewModels
                    .OrderByDescending(c => c.Category == "A" ? 3 : c.Category == "B" ? 2 : 1)
                    .ThenByDescending(c => c.TotalRevenue)
                    .ToList();
                
                ABCAnalysisGrid.ItemsSource = sortedViewModels;
                ABCAnalysisTime.Text = DateTime.Now.ToString("dd.MM.yyyy HH:mm");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading ABC analysis: {ex.Message}");
            }
        }

        private void RefreshABCAnalysis_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using var context = MultiUserSecurityManager.CreateCompanyContext();
                LoadABCAnalysis(context);
                
                // Анимация кнопки
                var button = sender as Button;
                var rotation = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(0.5));
                var transform = new RotateTransform();
                button.RenderTransform = transform;
                button.RenderTransformOrigin = new Point(0.5, 0.5);
                transform.BeginAnimation(RotateTransform.AngleProperty, rotation);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении ABC-анализа: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private System.Windows.Media.Brush GetCategoryColor(string category)
        {
            return category switch
            {
                "A" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(34, 197, 94)),   // Зеленый
                "B" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(59, 130, 246)),   // Синий
                "C" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(251, 146, 60)),   // Оранжевый
                _ => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(156, 163, 175))    // Серый
            };
        }

        private System.Windows.Media.Brush GetRiskColor(string riskLevel)
        {
            return riskLevel switch
            {
                "Высокий" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68)),    // Красный
                "Средний" => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 158, 11)),  // Желтый
                _ => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(34, 197, 94))      // Зеленый
            };
        }

        private void AnimateMetric(TextBlock textBlock, decimal value)
        {
            var animation = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(1));
            textBlock.BeginAnimation(OpacityProperty, animation);
        }

        private void RefreshReports_Click(object sender, RoutedEventArgs e)
        {
            var refreshButton = sender as Button;
            var rotation = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(0.5));
            var transform = new RotateTransform();
            refreshButton.RenderTransform = transform;
            refreshButton.RenderTransformOrigin = new Point(0.5, 0.5);
            transform.BeginAnimation(RotateTransform.AngleProperty, rotation);

            LoadData();
        }

        private void CloseWindow_Click(object sender, RoutedEventArgs e)
        {
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.3));
            fadeOut.Completed += (s, _) => Close();
            this.BeginAnimation(OpacityProperty, fadeOut);
        }

        private void ExportToExcel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Excel Files|*.xlsx",
                    FileName = $"Отчет_CRM_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
                };

                if (dialog.ShowDialog() == true)
                {
                    using var context = MultiUserSecurityManager.CreateCompanyContext();
                    
                    // Собираем все данные
                    var clients = context.Clients.ToList();
                    var deals = context.Deals.Include(d => d.Client).ToList();
                    var expenses = context.Expenses.OrderByDescending(e => e.Date).ToList();
                    var events = context.CalendarEvents.Include(e => e.Client).ToList();

                    // Создаем комплексный Excel файл через общий метод
                    ExcelExporter.ExportComprehensiveReport(clients, deals, expenses, events, dialog.FileName);
                    
                    MessageBox.Show("Отчет успешно экспортирован в Excel!", "Успешно", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка экспорта: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PrintReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var printDialog = new System.Windows.Controls.PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    // Создаем визуальный элемент для печати
                    var printContent = new System.Windows.Controls.Border
                    {
                        Background = System.Windows.Media.Brushes.White,
                        Padding = new System.Windows.Thickness(20),
                        Child = CreatePrintableContent()
                    };

                    // Измеряем и располагаем контент
                    printContent.Measure(new System.Windows.Size(printDialog.PrintableAreaWidth, printDialog.PrintableAreaHeight));
                    printContent.Arrange(new System.Windows.Rect(new System.Windows.Point(0, 0), printContent.DesiredSize));

                    // Печатаем
                    printDialog.PrintVisual(printContent, $"Отчет CRM {DateTime.Now:dd.MM.yyyy HH:mm}");
                    
                    MessageBox.Show("Отчет успешно отправлен на печать!", "Успешно", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при печати: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private System.Windows.UIElement CreatePrintableContent()
        {
            var stackPanel = new System.Windows.Controls.StackPanel();

            // Заголовок
            var title = new System.Windows.Controls.TextBlock
            {
                Text = "ОТЧЕТ CRM",
                FontSize = 24,
                FontWeight = System.Windows.FontWeights.Bold,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new System.Windows.Thickness(0, 0, 0, 10)
            };
            stackPanel.Children.Add(title);

            var dateText = new System.Windows.Controls.TextBlock
            {
                Text = $"Дата формирования: {DateTime.Now:dd.MM.yyyy HH:mm}",
                FontSize = 12,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                Margin = new System.Windows.Thickness(0, 0, 0, 20)
            };
            stackPanel.Children.Add(dateText);

            // Метрики
            using var context = MultiUserSecurityManager.CreateCompanyContext();
            var (startDate, endDate) = GetSelectedPeriodRange();

            var metricsSection = CreateMetricsSection(context, startDate, endDate);
            stackPanel.Children.Add(metricsSection);

            // Добавляем разделитель
            var separator = new System.Windows.Controls.Border
            {
                Height = 1,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 200, 200)),
                Margin = new System.Windows.Thickness(0, 20, 0, 20)
            };
            stackPanel.Children.Add(separator);

            // Информация о сделках
            var dealsInfo = CreateDealsInfoSection(context);
            stackPanel.Children.Add(dealsInfo);

            // Добавляем разделитель
            var separator2 = new System.Windows.Controls.Border
            {
                Height = 1,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 200, 200)),
                Margin = new System.Windows.Thickness(0, 20, 0, 20)
            };
            stackPanel.Children.Add(separator2);

            // Топ клиентов
            var topClientsSection = CreateTopClientsSection(context);
            stackPanel.Children.Add(topClientsSection);

            return stackPanel;
        }

        private System.Windows.UIElement CreateMetricsSection(AppDbContext context, DateTime startDate, DateTime endDate)
        {
            var grid = new System.Windows.Controls.Grid();
            grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });

            // Вычисляем метрики
            var successfulDeals = context.Deals
                .Where(d => d.Status == DealStatus.Successful && d.CreatedAt >= startDate && d.CreatedAt <= endDate)
                .ToList();

            decimal totalRevenue = successfulDeals.Sum(d => d.Amount);
            decimal totalExpenses = context.Expenses
                .Where(e => e.Date >= startDate && e.Date <= endDate)
                .Select(e => e.Amount)
                .ToList()
                .Sum();

            decimal netProfit = totalRevenue - totalExpenses;
            decimal profitMargin = totalRevenue > 0 ? (netProfit / totalRevenue * 100) : 0;

            var totalDeals = context.Deals.Count(d => d.CreatedAt >= startDate && d.CreatedAt <= endDate);
            var successfulCount = successfulDeals.Count;
            decimal conversionRate = totalDeals > 0 ? (successfulCount * 100m / totalDeals) : 0;

            // Карточки метрик
            var metrics = new[]
            {
                new { Label = "Выручка", Value = $"{totalRevenue:N0} ₽", Color = System.Windows.Media.Color.FromRgb(59, 130, 246) },
                new { Label = "Прибыль", Value = $"{netProfit:N0} ₽", Color = netProfit >= 0 ? System.Windows.Media.Color.FromRgb(34, 197, 94) : System.Windows.Media.Color.FromRgb(239, 68, 68) },
                new { Label = "Рентабельность", Value = $"{profitMargin:F1}%", Color = System.Windows.Media.Color.FromRgb(168, 85, 247) },
                new { Label = "Конверсия", Value = $"{conversionRate:F1}%", Color = System.Windows.Media.Color.FromRgb(249, 115, 22) }
            };

            int row = 0;
            foreach (var metric in metrics)
            {
                var card = new System.Windows.Controls.Border
                {
                    Background = new System.Windows.Media.SolidColorBrush(metric.Color),
                    CornerRadius = new System.Windows.CornerRadius(8),
                    Padding = new System.Windows.Thickness(15),
                    Margin = new System.Windows.Thickness(5)
                };

                var cardStack = new System.Windows.Controls.StackPanel();
                
                var label = new System.Windows.Controls.TextBlock
                {
                    Text = metric.Label,
                    FontSize = 12,
                    Foreground = System.Windows.Media.Brushes.White,
                    Opacity = 0.9
                };
                cardStack.Children.Add(label);

                var value = new System.Windows.Controls.TextBlock
                {
                    Text = metric.Value,
                    FontSize = 20,
                    FontWeight = System.Windows.FontWeights.Bold,
                    Foreground = System.Windows.Media.Brushes.White,
                    Margin = new System.Windows.Thickness(0, 5, 0, 0)
                };
                cardStack.Children.Add(value);

                card.Child = cardStack;
                System.Windows.Controls.Grid.SetRow(card, row / 2);
                System.Windows.Controls.Grid.SetColumn(card, row % 2);
                grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
                grid.Children.Add(card);
                row++;
            }

            return grid;
        }

        private System.Windows.UIElement CreateDealsInfoSection(AppDbContext context)
        {
            var stackPanel = new System.Windows.Controls.StackPanel();

            var title = new System.Windows.Controls.TextBlock
            {
                Text = "Информация о сделках",
                FontSize = 16,
                FontWeight = System.Windows.FontWeights.Bold,
                Margin = new System.Windows.Thickness(0, 0, 0, 10)
            };
            stackPanel.Children.Add(title);

            var totalDeals = context.Deals.Count();
            var successfulDeals = context.Deals.Count(d => d.Status == DealStatus.Successful);
            var inProgressDeals = context.Deals.Count(d => d.Status == DealStatus.InProgress);
            var failedDeals = context.Deals.Count(d => d.Status == DealStatus.Failed);

            var infoText = $"Всего сделок: {totalDeals}\n" +
                          $"Успешных: {successfulDeals}\n" +
                          $"В работе: {inProgressDeals}\n" +
                          $"Проваленных: {failedDeals}";

            var infoBlock = new System.Windows.Controls.TextBlock
            {
                Text = infoText,
                FontSize = 14,
                TextWrapping = System.Windows.TextWrapping.Wrap
            };
            stackPanel.Children.Add(infoBlock);

            return stackPanel;
        }

        private System.Windows.UIElement CreateTopClientsSection(AppDbContext context)
        {
            var stackPanel = new System.Windows.Controls.StackPanel();

            var title = new System.Windows.Controls.TextBlock
            {
                Text = "Топ клиентов по выручке",
                FontSize = 16,
                FontWeight = System.Windows.FontWeights.Bold,
                Margin = new System.Windows.Thickness(0, 0, 0, 10)
            };
            stackPanel.Children.Add(title);

            var topClients = context.Deals
                .Where(d => d.Status == DealStatus.Successful)
                .Include(d => d.Client)
                .ToList()
                .GroupBy(d => d.Client)
                .Select(g => new
                {
                    Client = g.Key,
                    TotalAmount = g.Sum(d => d.Amount)
                })
                .OrderByDescending(c => c.TotalAmount)
                .Take(5)
                .ToList();

            foreach (var client in topClients)
            {
                var clientText = new System.Windows.Controls.TextBlock
                {
                    Text = $"• {client.Client?.Name ?? "Неизвестный"}: {client.TotalAmount:N0} ₽",
                    FontSize = 14,
                    Margin = new System.Windows.Thickness(0, 5, 0, 0)
                };
                stackPanel.Children.Add(clientText);
            }

            return stackPanel;
        }

       
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            this.DragMove();
        }
    }
}

// ViewModel классы для ABC-анализа
public class ABCClientViewModel
{
    public string ClientName { get; set; } = "";
    public string Category { get; set; } = "";
    public decimal TotalRevenue { get; set; }
    public int DealCount { get; set; }
    public decimal AverageDealSize { get; set; }
    public int LoyaltyScore { get; set; }
    public string RiskLevel { get; set; } = "";
    public int DaysSinceLastDeal { get; set; }
    public decimal ConversionRate { get; set; }
    public System.Windows.Media.Brush CategoryColor { get; set; } = System.Windows.Media.Brushes.Gray;
    public System.Windows.Media.Brush RiskColor { get; set; } = System.Windows.Media.Brushes.Gray;
}