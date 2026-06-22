using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace MyFirstCRM
{
    public partial class CalendarWindow : Window
    {
        private DateTime currentMonth;
        private DateTime selectedDate;
        private List<CalendarEvent> allEvents;
        private CalendarViewMode viewMode = CalendarViewMode.Month;
        
        public CalendarWindow()
        {
            InitializeComponent();
            InitializeCalendar();
            LoadEvents();
            SetupFilters();
            UpdateCalendar();
        }

        private void InitializeCalendar()
        {
            currentMonth = DateTime.Now;
            selectedDate = DateTime.Now;
            allEvents = new List<CalendarEvent>();
            
            // Настраиваем сетку дней
            for (int i = 0; i < 6; i++)
            {
                DaysGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            }
            for (int i = 0; i < 7; i++)
            {
                DaysGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }
        }

        private void SetupFilters()
        {
            // Фильтр типов событий
            EventTypeFilter.Items.Add("Все типы");
            EventTypeFilter.Items.Add("Встреча");
            EventTypeFilter.Items.Add("Звонок");
            EventTypeFilter.Items.Add("Задача");
            EventTypeFilter.Items.Add("Дедлайн");
            EventTypeFilter.Items.Add("Презентация");
            EventTypeFilter.Items.Add("След. действие");
            EventTypeFilter.Items.Add("Другое");
            EventTypeFilter.SelectedIndex = 0;

            // Фильтр статусов
            StatusFilter.Items.Add("Все статусы");
            StatusFilter.Items.Add("Запланировано");
            StatusFilter.Items.Add("В процессе");
            StatusFilter.Items.Add("Завершено");
            StatusFilter.Items.Add("Отменено");
            StatusFilter.SelectedIndex = 0;
        }

        private void LoadEvents()
        {
            try
            {
                // Проверяем авторизацию
                if (!MultiUserSecurityManager.IsAuthenticated)
                {
                    MessageBox.Show("Пользователь не авторизован", "Ошибка", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                using (var context = MultiUserSecurityManager.CreateCompanyContext())
                {
                    allEvents = context.CalendarEvents
                        .Include(e => e.Deal)
                        .Include(e => e.Client)
                        .Where(e => e.CompanyId == MultiUserSecurityManager.CurrentUser.CompanyId)
                        .ToList();
                    
                    Debug.WriteLine($"=== CalendarWindow: Загрузка событий ===");
                    Debug.WriteLine($"Загружено {allEvents.Count} событий для компании {MultiUserSecurityManager.CurrentUser.CompanyId}");
                    
                    foreach (var evt in allEvents)
                    {
                        Debug.WriteLine($"Событие #{evt.Id}: {evt.Title}");
                        Debug.WriteLine($"  - Дата: {evt.StartDate:dd.MM.yyyy HH:mm}");
                        Debug.WriteLine($"  - Тип: {evt.EventType}");
                        Debug.WriteLine($"  - Статус: {evt.Status}");
                        Debug.WriteLine($"  - DealId: {evt.DealId}");
                        Debug.WriteLine($"  - CompanyId: {evt.CompanyId}");
                        Debug.WriteLine($"  - CreatedByUserId: {evt.CreatedByUserId}");
                    }
                    
                    Debug.WriteLine($"=== Конец списка событий (CalendarWindow) ===");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки событий: {ex.Message}", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateCalendar()
        {
            try
            {
                // Обновляем заголовок месяца
                MonthYearText.Text = currentMonth.ToString("MMMM yyyy", new System.Globalization.CultureInfo("ru-RU"));
                
                // Очищаем предыдущие дни
                DaysGrid.Children.Clear();
                
                // Определяем первый день месяца
                DateTime firstDay = new DateTime(currentMonth.Year, currentMonth.Month, 1);
                DateTime lastDay = firstDay.AddMonths(1).AddDays(-1);
                
                // Определяем день недели для начала (с понедельника)
                int startDayOfWeek = (int)firstDay.DayOfWeek == 0 ? 6 : (int)firstDay.DayOfWeek - 1;
                
                // Создаем дни календаря
                DateTime currentDay = firstDay.AddDays(-startDayOfWeek);
                
                for (int row = 0; row < 6; row++)
                {
                    for (int col = 0; col < 7; col++)
                    {
                        if (currentDay > lastDay.AddDays(7 - startDayOfWeek))
                            break;
                            
                        var dayEvents = GetEventsForDay(currentDay);
                        
                        var dayButton = new Button
                        {
                            Content = currentDay.Day.ToString(),
                            Style = FindResource("DayBtn") as Style
                        };
                        
                        // Добавляем индикатор событий если есть
                        if (dayEvents > 0)
                        {
                            var indicator = new Border
                            {
                                Background = new SolidColorBrush(Color.FromRgb(59, 130, 246)),
                                CornerRadius = new CornerRadius(6),
                                Width = 20,
                                Height = 6,
                                HorizontalAlignment = HorizontalAlignment.Center,
                                VerticalAlignment = VerticalAlignment.Bottom,
                                Margin = new Thickness(0, 0, 0, 8)
                            };
                            
                            var grid = new Grid();
                            var dayText = new TextBlock
                            {
                                Text = currentDay.Day.ToString(),
                                HorizontalAlignment = HorizontalAlignment.Center,
                                VerticalAlignment = VerticalAlignment.Top,
                                Margin = new Thickness(0, 8, 0, 0)
                            };
                            grid.Children.Add(dayText);
                            grid.Children.Add(indicator);
                            dayButton.Content = grid;
                        }
                        
                        // Подсветка текущего дня
                        if (currentDay.Date == DateTime.Today)
                        {
                            dayButton.Background = new SolidColorBrush(Color.FromRgb(230, 240, 250));
                            dayButton.BorderBrush = new SolidColorBrush(Color.FromRgb(59, 130, 246));
                            dayButton.BorderThickness = new Thickness(2);
                        }
                        
                        // Подсветка выбранного дня
                        if (currentDay.Date == selectedDate.Date)
                        {
                            dayButton.Background = new SolidColorBrush(Color.FromRgb(219, 234, 254));
                        }
                        
                        // Серый цвет для дней из других месяцев
                        if (currentDay.Month != currentMonth.Month)
                        {
                            dayButton.Foreground = new SolidColorBrush(Colors.Gray);
                        }
                        
                        // Создаем локальную копию для избежания замыкания
                        var clickDate = currentDay;
                        dayButton.Click += (s, e) => SelectDate(clickDate);
                        
                        Grid.SetRow(dayButton, row);
                        Grid.SetColumn(dayButton, col);
                        DaysGrid.Children.Add(dayButton);
                        
                        currentDay = currentDay.AddDays(1);
                    }
                }
                
                UpdateStatusBar();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка обновления календаря: {ex.Message}", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private int GetEventsForDay(DateTime date)
        {
            var events = allEvents.Where(e => e.StartDate.Date <= date.Date && e.EndDate.Date >= date.Date)
                                 .Where(e => PassesFilter(e))
                                 .ToList();
            
            return events.Count;
        }

        private bool PassesFilter(CalendarEvent evt)
        {
            // Фильтр по типу
            if (EventTypeFilter.SelectedIndex > 0)
            {
                var selectedTypeText = EventTypeFilter.SelectedItem.ToString();
                CalendarEventType selectedType = selectedTypeText switch
                {
                    "Встреча" => CalendarEventType.Meeting,
                    "Звонок" => CalendarEventType.Call,
                    "Задача" => CalendarEventType.Task,
                    "Дедлайн" => CalendarEventType.Deadline,
                    "Презентация" => CalendarEventType.Presentation,
                    "След. действие" => CalendarEventType.FollowUp,
                    "Другое" => CalendarEventType.Other,
                    _ => CalendarEventType.Other
                };
                if (evt.EventType != selectedType) return false;
            }
            
            // Фильтр по статусу
            if (StatusFilter.SelectedIndex > 0)
            {
                var selectedStatusText = StatusFilter.SelectedItem.ToString();
                EventStatus selectedStatus = selectedStatusText switch
                {
                    "Запланировано" => EventStatus.Scheduled,
                    "В процессе" => EventStatus.InProgress,
                    "Завершено" => EventStatus.Completed,
                    "Отменено" => EventStatus.Cancelled,
                    _ => EventStatus.Scheduled
                };
                if (evt.Status != selectedStatus) return false;
            }
            
            // Фильтр по сделкам
            if (ShowDealsOnly.IsChecked == true && evt.DealId == null)
            {
                return false;
            }
            
            return true;
        }

        private void SelectDate(DateTime date)
        {
            selectedDate = date;
            UpdateCalendar();
            ShowDayEvents(date);
        }

        private void ShowDayEvents(DateTime date)
        {
            var dayEvents = allEvents.Where(e => e.StartDate.Date <= date.Date && e.EndDate.Date >= date.Date)
                                   .Where(e => PassesFilter(e))
                                   .OrderBy(e => e.StartDate)
                                   .ToList();
            
            if (dayEvents.Any())
            {
                var eventsWindow = new DayEventsWindow(dayEvents, date);
                eventsWindow.Owner = this;
                eventsWindow.ShowDialog();
                
                if (eventsWindow.EventsChanged)
                {
                    LoadEvents();
                    UpdateCalendar();
                }
            }
            else
            {
                MessageBox.Show($"На {date:dd.MM.yyyy} нет событий", "Информация", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void PrevMonth_Click(object sender, RoutedEventArgs e)
        {
            currentMonth = currentMonth.AddMonths(-1);
            UpdateCalendar();
        }

        private void NextMonth_Click(object sender, RoutedEventArgs e)
        {
            currentMonth = currentMonth.AddMonths(1);
            UpdateCalendar();
        }

        private void Today_Click(object sender, RoutedEventArgs e)
        {
            currentMonth = DateTime.Now;
            selectedDate = DateTime.Now;
            UpdateCalendar();
        }

        private void AddEvent_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var addEventWindow = new AddEventWindow(selectedDate);
                addEventWindow.Owner = this;
                
                if (addEventWindow.ShowDialog() == true)
                {
                    LoadEvents();
                    UpdateCalendar();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка добавления события: {ex.Message}", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ViewToggle_Click(object sender, RoutedEventArgs e)
        {
            viewMode = viewMode == CalendarViewMode.Month ? CalendarViewMode.Week : CalendarViewMode.Month;
            ViewToggleBtn.Content = viewMode == CalendarViewMode.Month ? "Неделя" : "Месяц";
            UpdateCalendar();
        }

        private void FilterChanged(object sender, RoutedEventArgs e)
        {
            UpdateCalendar();
        }

        private void ResetFilters_Click(object sender, RoutedEventArgs e)
        {
            EventTypeFilter.SelectedIndex = 0;
            StatusFilter.SelectedIndex = 0;
            ShowDealsOnly.IsChecked = false;
            UpdateCalendar();
        }
        
        private void SendMessages_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Получаем уникальных клиентов из отфильтрованных событий
                var filteredEvents = allEvents.Where(PassesFilter).ToList();
                var uniqueClients = filteredEvents
                    .Where(evt => evt.Client != null)
                    .Select(evt => evt.Client)
                    .Distinct()
                    .ToList();

                if (!uniqueClients.Any())
                {
                    MessageBox.Show("Нет событий с привязанными клиентами для отправки сообщений.", 
                        "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var result = MessageBox.Show(
                    $"Найдено {uniqueClients.Count} уникальных клиентов. Отправить сообщения всем?",
                    "Массовая рассылка",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question
                );

                if (result == MessageBoxResult.Yes)
                {
                    foreach (var client in uniqueClients)
                    {
                        var messagesWindow = new MessagesWindow(
                            email: client.Email, 
                            phone: client.Phone
                        );
                        messagesWindow.Owner = this;
                        messagesWindow.ShowDialog();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при отправке сообщений: {ex.Message}", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateStatusBar()
        {
            var filteredEvents = allEvents.Where(PassesFilter).ToList();
            EventCountText.Text = $"Найдено событий: {filteredEvents.Count}";
        }
    }

    public enum CalendarViewMode
    {
        Month,
        Week
    }
}
