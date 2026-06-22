using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Data;
using Microsoft.EntityFrameworkCore;

namespace MyFirstCRM
{
    public partial class SyncLogWindow : Window
    {
        private List<SyncLog> _allSyncLogs;
        private DeploymentManager _deploymentManager;

        public SyncLogWindow()
        {
            InitializeComponent();
            _deploymentManager = DeploymentManager.Instance;
            
            LoadCompanies();
            LoadSyncLogs();
            SetupEventHandlers();
        }

        /// <summary>
        /// Загрузка списка компаний
        /// </summary>
        private void LoadCompanies()
        {
            try
            {
                using var context = new GlobalDbContext();
                var companies = context.Companies.Where(c => c.IsActive).ToList();
                
                CompanyFilterComboBox.Items.Clear();
                CompanyFilterComboBox.Items.Add(new ComboBoxItem { Content = "Все компании", Tag = "" });
                
                foreach (var company in companies)
                {
                    CompanyFilterComboBox.Items.Add(new ComboBoxItem 
                    { 
                        Content = $"{company.Name} ({company.CompanyCode})", 
                        Tag = company.Id 
                    });
                }
                
                CompanyFilterComboBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке компаний: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Загрузка журнала синхронизации
        /// </summary>
        private void LoadSyncLogs()
        {
            try
            {
                using var context = new GlobalDbContext();
                
                // Проверяем и создаем таблицу SyncLogs если она не существует
                EnsureSyncLogsTableExists(context);
                
                _allSyncLogs = context.SyncLogs
                    .ToList();

                ApplyFilters();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке журнала: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Проверяет и создает таблицу SyncLogs если она не существует
        /// </summary>
        private void EnsureSyncLogsTableExists(GlobalDbContext context)
        {
            try
            {
                var connection = context.Database.GetDbConnection();
                connection.Open();
                
                var command = connection.CreateCommand();
                command.CommandText = @"
                    CREATE TABLE IF NOT EXISTS ""SyncLogs"" (
                        ""Id"" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                        ""CompanyId"" INTEGER NOT NULL,
                        ""SyncType"" INTEGER NOT NULL,
                        ""Status"" INTEGER NOT NULL,
                        ""StartedAt"" TEXT NOT NULL DEFAULT (datetime('now')),
                        ""CompletedAt"" TEXT NULL,
                        ""RecordsSent"" INTEGER NOT NULL,
                        ""RecordsReceived"" INTEGER NOT NULL,
                        ""DataSizeBytes"" INTEGER NOT NULL,
                        ""ErrorMessage"" TEXT NULL,
                        ""Details"" TEXT NULL
                    );
                    
                    CREATE INDEX IF NOT EXISTS ""IX_SyncLogs_CompanyId_StartedAt"" 
                    ON ""SyncLogs"" (""CompanyId"", ""StartedAt"");
                ";
                
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                // Если таблица уже существует или другая ошибка, просто логируем
                System.Diagnostics.Debug.WriteLine($"Ошибка при создании таблицы SyncLogs: {ex.Message}");
            }
        }

        /// <summary>
        /// Настройка обработчиков событий
        /// </summary>
        private void SetupEventHandlers()
        {
            CompanyFilterComboBox.SelectionChanged += (s, e) => ApplyFilters();
            StatusFilterComboBox.SelectionChanged += (s, e) => ApplyFilters();
            FromDateDatePicker.SelectedDateChanged += (s, e) => ApplyFilters();
        }

        /// <summary>
        /// Применение фильтров
        /// </summary>
        private void ApplyFilters()
        {
            try
            {
                var filteredLogs = _allSyncLogs.AsEnumerable();

                // Фильтр по компании
                var selectedCompany = CompanyFilterComboBox.SelectedItem as ComboBoxItem;
                if (selectedCompany?.Tag != null && !string.IsNullOrEmpty(selectedCompany.Tag.ToString()))
                {
                    int companyId = int.Parse(selectedCompany.Tag.ToString()!);
                    filteredLogs = filteredLogs.Where(log => log.CompanyId == companyId);
                }

                // Фильтр по статусу
                var selectedStatus = StatusFilterComboBox.SelectedItem as ComboBoxItem;
                if (selectedStatus?.Tag != null && !string.IsNullOrEmpty(selectedStatus.Tag.ToString()))
                {
                    if (Enum.TryParse<SyncStatus>(selectedStatus.Tag.ToString(), out var status))
                    {
                        filteredLogs = filteredLogs.Where(log => log.Status == status);
                    }
                }

                // Фильтр по дате
                if (FromDateDatePicker.SelectedDate.HasValue)
                {
                    DateTime fromDate = FromDateDatePicker.SelectedDate.Value.Date;
                    filteredLogs = filteredLogs.Where(log => log.StartedAt.Date >= fromDate);
                }

                // Преобразование в отображаемый формат
                var displayLogs = filteredLogs.Select(log => new SyncLogDisplayItem
                {
                    Id = log.Id,
                    CompanyId = log.CompanyId,
                    CompanyName = $"Компания {log.CompanyId}",
                    SyncType = log.SyncType,
                    Status = log.Status,
                    StartedAt = log.StartedAt,
                    CompletedAt = log.CompletedAt,
                    RecordsSent = log.RecordsSent,
                    RecordsReceived = log.RecordsReceived,
                    DataSizeBytes = log.DataSizeBytes,
                    ErrorMessage = log.ErrorMessage,
                    Details = log.Details,
                    Duration = log.CompletedAt.HasValue ? log.CompletedAt.Value - log.StartedAt : TimeSpan.Zero
                }).ToList();

                SyncLogDataGrid.ItemsSource = displayLogs;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при применении фильтров: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Обновление данных
        /// </summary>
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadSyncLogs();
        }

        /// <summary>
        /// Двойной клик по строке для просмотра деталей
        /// </summary>
        private void DataGridRow_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (SyncLogDataGrid.SelectedItem is SyncLogDisplayItem selectedItem)
            {
                ShowDetails(selectedItem);
            }
        }

        /// <summary>
        /// Показать детали операции
        /// </summary>
        private void ShowDetails(SyncLogDisplayItem item)
        {
            var detailsWindow = new Window
            {
                Title = $"Детали синхронизации - {item.CompanyName}",
                Width = 600,
                Height = 500,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };

            var scrollViewer = new ScrollViewer();
            var stackPanel = new StackPanel { Margin = new Thickness(20) };

            // Основная информация
            stackPanel.Children.Add(CreateInfoRow("🏢 Компания:", item.CompanyName));
            stackPanel.Children.Add(CreateInfoRow("📅 Начало:", item.StartedAt.ToString("dd.MM.yyyy HH:mm:ss")));
            stackPanel.Children.Add(CreateInfoRow("📅 Окончание:", item.CompletedAt?.ToString("dd.MM.yyyy HH:mm:ss") ?? "Выполняется"));
            stackPanel.Children.Add(CreateInfoRow("🔄 Тип:", item.SyncType.ToString()));
            stackPanel.Children.Add(CreateInfoRow("📊 Статус:", GetStatusDisplay(item.Status)));
            stackPanel.Children.Add(CreateInfoRow("📤 Записей отправлено:", item.RecordsSent.ToString()));
            stackPanel.Children.Add(CreateInfoRow("📥 Записей получено:", item.RecordsReceived.ToString()));
            stackPanel.Children.Add(CreateInfoRow("💾 Размер данных:", FormatBytes(item.DataSizeBytes)));
            stackPanel.Children.Add(CreateInfoRow("⏱️ Длительность:", item.Duration.ToString(@"hh\:mm\:ss")));

            if (!string.IsNullOrEmpty(item.ErrorMessage))
            {
                stackPanel.Children.Add(new Separator { Margin = new Thickness(0, 10, 0, 10) });
                stackPanel.Children.Add(CreateInfoRow("❌ Ошибка:", item.ErrorMessage));
            }

            if (!string.IsNullOrEmpty(item.Details))
            {
                stackPanel.Children.Add(new Separator { Margin = new Thickness(0, 10, 0, 10) });
                var detailsLabel = new TextBlock 
                { 
                    Text = "📝 Детали:",
                    Style = (Style)FindResource("MaterialDesignSubtitle1TextBlock"),
                    Margin = new Thickness(0, 0, 0, 5)
                };
                stackPanel.Children.Add(detailsLabel);
                
                var detailsText = new TextBlock 
                { 
                    Text = item.Details,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(10, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                stackPanel.Children.Add(detailsText);
            }

            scrollViewer.Content = stackPanel;
            detailsWindow.Content = scrollViewer;
            detailsWindow.ShowDialog();
        }

        /// <summary>
        /// Создание строки информации
        /// </summary>
        private UIElement CreateInfoRow(string label, string value)
        {
            var stackPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
            
            var labelBlock = new TextBlock 
                { 
                    Text = label,
                    FontWeight = FontWeights.Bold,
                    Width = 150,
                    VerticalAlignment = VerticalAlignment.Center
                };
            
            var valueBlock = new TextBlock 
            { 
                Text = value,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            
            stackPanel.Children.Add(labelBlock);
            stackPanel.Children.Add(valueBlock);
            
            return stackPanel;
        }

        /// <summary>
        /// Получение отображения статуса
        /// </summary>
        private string GetStatusDisplay(SyncStatus status)
        {
            return status switch
            {
                SyncStatus.Success => "✅ Успешно",
                SyncStatus.Failed => "❌ Ошибка",
                SyncStatus.InProgress => "🔄 Выполняется",
                SyncStatus.Partial => "⚠️ Частично",
                SyncStatus.Conflict => "⚠️ Конфликт",
                _ => status.ToString()
            };
        }

        /// <summary>
        /// Форматирование байтов
        /// </summary>
        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        /// <summary>
        /// Экспорт журнала
        /// </summary>
        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "CSV файлы (*.csv)|*.csv|Текстовые файлы (*.txt)|*.txt",
                    FileName = $"SyncLog_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    var items = SyncLogDataGrid.ItemsSource as List<SyncLogDisplayItem>;
                    if (items != null)
                    {
                        ExportToCsv(items, saveFileDialog.FileName);
                        MessageBox.Show("✅ Журнал успешно экспортирован!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при экспорте: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Экспорт в CSV
        /// </summary>
        private void ExportToCsv(List<SyncLogDisplayItem> items, string filePath)
        {
            var lines = new List<string>();
            
            // Заголовок
            lines.Add("Дата,Компания,Тип,Статус,Отправлено,Получено,Размер,Длительность,Ошибка");
            
            // Данные
            foreach (var item in items)
            {
                lines.Add($"{item.StartedAt:dd.MM.yyyy HH:mm:ss}," +
                         $"\"{item.CompanyName}\"," +
                         $"{item.SyncType}," +
                         $"{item.Status}," +
                         $"{item.RecordsSent}," +
                         $"{item.RecordsReceived}," +
                         $"{FormatBytes(item.DataSizeBytes)}," +
                         $"{item.Duration:hh\\:mm\\:ss}," +
                         $"\"{item.ErrorMessage?.Replace("\"", "\"\"")}\"");
            }
            
            System.IO.File.WriteAllLines(filePath, lines, System.Text.Encoding.UTF8);
        }

        /// <summary>
        /// Очистка журнала
        /// </summary>
        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Вы уверены, что хотите очистить журнал синхронизации?\n\nЭто действие необратимо.",
                "Подтверждение",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    using var context = new GlobalDbContext();
                    var logsToDelete = context.SyncLogs.ToList();
                    context.SyncLogs.RemoveRange(logsToDelete);
                    context.SaveChanges();

                    LoadSyncLogs();
                    MessageBox.Show("✅ Журнал успешно очищен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при очистке журнала: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// Закрытие окна
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    /// <summary>
    /// Элемент отображения журнала синхронизации
    /// </summary>
    public class SyncLogDisplayItem
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public string CompanyName { get; set; } = "";
        public SyncType SyncType { get; set; }
        public SyncStatus Status { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int RecordsSent { get; set; }
        public int RecordsReceived { get; set; }
        public long DataSizeBytes { get; set; }
        public string? ErrorMessage { get; set; }
        public string? Details { get; set; }
        public TimeSpan Duration { get; set; }
    }

    /// <summary>
    /// Конвертер байтов в человекочитаемый размер
    /// </summary>
    public class BytesToSizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is long bytes)
            {
                string[] sizes = { "B", "KB", "MB", "GB", "TB" };
                double len = bytes;
                int order = 0;
                while (len >= 1024 && order < sizes.Length - 1)
                {
                    order++;
                    len = len / 1024;
                }
                return $"{len:0.##} {sizes[order]}";
            }
            return "0 B";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Конвертер TimeSpan в строку
    /// </summary>
    public class DurationConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TimeSpan duration)
            {
                if (duration.TotalSeconds < 60)
                    return $"{duration.TotalSeconds:F0}s";
                if (duration.TotalMinutes < 60)
                    return $"{duration.TotalMinutes:F1}m";
                return $"{duration.TotalHours:F1}h";
            }
            return "0s";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
