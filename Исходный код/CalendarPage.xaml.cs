using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace MyFirstCRM
{
    public partial class CalendarPage : UserControl
    {
        private DateTime currentMonth;
        private List<CalendarEvent> allEvents;

        public CalendarPage()
        {
            InitializeComponent();
            currentMonth = DateTime.Now;
            LoadDeals();
            LoadEvents();
            UpdateCalendar();
            SetupFilters();
        }

        private void LoadDeals()
        {
            try
            {
                using (var context = MultiUserSecurityManager.CreateCompanyContext())
                {
                    var deals = context.Deals.Include(d => d.Client).ToList();
                    DealFilterComboBox.Items.Clear();
                    DealFilterComboBox.Items.Add("Все сделки");
                    
                    foreach (var deal in deals)
                    {
                        string dealText = $"{deal.Title} ({deal.Client?.Name})";
                        DealFilterComboBox.Items.Add(dealText);
                    }
                    DealFilterComboBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки сделок: {ex.Message}", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void LoadEvents()
        {
            try
            {
                using (var context = MultiUserSecurityManager.CreateCompanyContext())
                {
                    allEvents = context.CalendarEvents
                        .Include(e => e.Deal)
                        .Include(e => e.Client)
                        .Where(e => e.CompanyId == MultiUserSecurityManager.CurrentUser.CompanyId)
                        .ToList();
                    
                    Debug.WriteLine($"=== CalendarPage: Загрузка событий ===");
                    Debug.WriteLine($"Загружено {allEvents.Count} событий для компании {MultiUserSecurityManager.CurrentUser.CompanyId}");
                    
                    foreach (var evt in allEvents)
                    {
                        Debug.WriteLine($"Событие #{evt.Id}: {evt.Title}");
                        Debug.WriteLine($"  - Дата: {evt.StartDate}");
                        Debug.WriteLine($"  - Тип: {evt.EventType}");
                        Debug.WriteLine($"  - Статус: {evt.Status}");
                        Debug.WriteLine($"  - DealId: {evt.DealId}");
                        Debug.WriteLine($"  - CompanyId: {evt.CompanyId}");
                        Debug.WriteLine($"  - CreatedByUserId: {evt.CreatedByUserId}");
                    }
                    
                    Debug.WriteLine($"=== Конец списка событий ===");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки событий: {ex.Message}", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
                allEvents = new List<CalendarEvent>();
            }
        }

        private void SetupFilters()
        {
            TypeFilterComboBox.SelectedIndex = 0;
            StatusFilterComboBox.SelectedIndex = 0;
            DealFilterComboBox.SelectedIndex = 0;

            TypeFilterComboBox.SelectionChanged += (s, e) => UpdateCalendar();
            StatusFilterComboBox.SelectionChanged += (s, e) => UpdateCalendar();
            DealFilterComboBox.SelectionChanged += (s, e) => UpdateCalendar();
        }

        public void UpdateCalendar()
        {
            // Обновляем заголовок месяца
            CurrentMonthText.Text = currentMonth.ToString("MMMM yyyy", new System.Globalization.CultureInfo("ru-RU"));
            
            // Очищаем сетку календаря
            DaysGrid.Children.Clear();
            
            // Получаем первый день месяца
            var firstDay = new DateTime(currentMonth.Year, currentMonth.Month, 1);
            var lastDay = firstDay.AddMonths(1).AddDays(-1);
            
            // Определяем день недели первого дня (0 = воскресенье, 1 = понедельник, ...)
            int startDayOfWeek = (int)firstDay.DayOfWeek;
            if (startDayOfWeek == 0) startDayOfWeek = 7; // Воскресенье = 7
            startDayOfWeek--; // Приводим к 0-6 (Пн-Вс)
            
            // Фильтруем события
            var filteredEvents = FilterEvents();
            
            // Создаем дни месяца
            int dayCounter = 1;
            for (int week = 0; week < 6; week++)
            {
                for (int day = 0; day < 7; day++)
                {
                    var dayBorder = new Border
                    {
                        BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                        BorderThickness = new Thickness(0, 0, 1, 1),
                        Background = new SolidColorBrush(Colors.White),
                        Margin = new Thickness(0)
                    };
                    
                    var dayStack = new StackPanel();
                    
                    // Определяем день месяца
                    if (week == 0 && day < startDayOfWeek)
                    {
                        // Пустые ячейки до начала месяца
                        var emptyText = new TextBlock { Text = "" };
                        dayStack.Children.Add(emptyText);
                    }
                    else if (dayCounter > lastDay.Day)
                    {
                        // Пустые ячейки после конца месяца
                        var emptyText = new TextBlock { Text = "" };
                        dayStack.Children.Add(emptyText);
                    }
                    else
                    {
                        // День месяца
                        var dayText = new TextBlock 
                        { 
                            Text = dayCounter.ToString(),
                            FontWeight = FontWeights.SemiBold,
                            Margin = new Thickness(5, 2, 0, 2)
                        };
                        
                        // Выделяем сегодняшний день
                        if (dayCounter == DateTime.Now.Day && 
                            currentMonth.Month == DateTime.Now.Month && 
                            currentMonth.Year == DateTime.Now.Year)
                        {
                            dayBorder.Background = new SolidColorBrush(Color.FromRgb(239, 246, 255));
                            dayText.Foreground = new SolidColorBrush(Color.FromRgb(59, 130, 246));
                        }
                        
                        dayStack.Children.Add(dayText);
                        
                        // Добавляем события на этот день
                        var dayEvents = filteredEvents.Where(e => 
                            e.StartDate.Day == dayCounter && 
                            e.StartDate.Month == currentMonth.Month && 
                            e.StartDate.Year == currentMonth.Year).ToList();
                        
                        if (dayEvents.Any())
                        {
                            Debug.WriteLine($"День {dayCounter}: найдено {dayEvents.Count} событий");
                            foreach (var evt in dayEvents)
                            {
                                Debug.WriteLine($"  - {evt.Title} ({evt.EventType})");
                            }
                        }
                        
                        foreach (var evt in dayEvents.Take(3)) // Максимум 3 события на день
                        {
                            var eventBorder = new Border
                            {
                                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(evt.Color ?? "#3498db")),
                                CornerRadius = new CornerRadius(2),
                                Margin = new Thickness(2, 1, 2, 1),
                                Padding = new Thickness(2, 1, 2, 1),
                                Height = 16
                            };
                            
                            var eventText = new TextBlock
                            {
                                Text = evt.Title.Length > 15 ? evt.Title.Substring(0, 15) + "..." : evt.Title,
                                FontSize = 9,
                                Foreground = new SolidColorBrush(Colors.White),
                                FontWeight = FontWeights.Medium
                            };
                            
                            eventBorder.Child = eventText;
                            dayStack.Children.Add(eventBorder);
                        }
                        
                        if (dayEvents.Count > 3)
                        {
                            var moreText = new TextBlock
                            {
                                Text = $"+{dayEvents.Count - 3} еще",
                                FontSize = 8,
                                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                                Margin = new Thickness(4, 0, 0, 0)
                            };
                            dayStack.Children.Add(moreText);
                        }
                        
                        // Добавляем обработчик клика
                        dayBorder.Cursor = System.Windows.Input.Cursors.Hand;
                        dayBorder.MouseLeftButtonUp += (s, e) => ShowDayEvents(dayCounter);
                        
                        dayCounter++;
                    }
                    
                    dayBorder.Child = dayStack;
                    Grid.SetRow(dayBorder, week);
                    Grid.SetColumn(dayBorder, day);
                    DaysGrid.Children.Add(dayBorder);
                }
                
                if (dayCounter > lastDay.Day) break;
            }
            
            // Обновляем статус
            StatusText.Text = $"События: {filteredEvents.Count}";
        }

        private List<CalendarEvent> FilterEvents()
        {
            var filtered = allEvents.AsEnumerable();
            
            Debug.WriteLine($"=== Фильтрация событий ===");
            Debug.WriteLine($"Исходное количество: {allEvents.Count}");
            
            // Фильтр по типу
            var typeFilter = TypeFilterComboBox.SelectedItem?.ToString();
            Debug.WriteLine($"Фильтр по типу: {typeFilter}");
            if (typeFilter != "Все типы" && !string.IsNullOrEmpty(typeFilter))
            {
                var eventType = typeFilter switch
                {
                    "Встреча" => CalendarEventType.Meeting,
                    "Звонок" => CalendarEventType.Call,
                    "Задача" => CalendarEventType.Task,
                    "Дедлайн" => CalendarEventType.Deadline,
                    "Презентация" => CalendarEventType.Presentation,
                    "След. действие" => CalendarEventType.FollowUp,
                    "Другое" => CalendarEventType.Other,
                    _ => (CalendarEventType?)null
                };
                if (eventType.HasValue)
                    filtered = filtered.Where(e => e.EventType == eventType.Value);
            }
            
            // Фильтр по статусу
            var statusFilter = StatusFilterComboBox.SelectedItem?.ToString();
            Debug.WriteLine($"Фильтр по статусу: {statusFilter}");
            if (statusFilter != "Все статусы" && !string.IsNullOrEmpty(statusFilter))
            {
                var status = statusFilter switch
                {
                    "Запланировано" => EventStatus.Scheduled,
                    "Выполнено" => EventStatus.Completed,
                    "Отменено" => EventStatus.Cancelled,
                    _ => (EventStatus?)null
                };
                if (status.HasValue)
                    filtered = filtered.Where(e => e.Status == status.Value);
            }
            
            // Фильтр по сделке
            var dealFilter = DealFilterComboBox.SelectedItem?.ToString();
            Debug.WriteLine($"Фильтр по сделке: {dealFilter}");
            if (dealFilter != "Все сделки" && !string.IsNullOrEmpty(dealFilter))
            {
                // Извлекаем ID сделки из строки "Название (Клиент)"
                var parts = dealFilter.Split('(');
                if (parts.Length > 0)
                {
                    var dealTitle = parts[0].Trim();
                    using (var context = MultiUserSecurityManager.CreateCompanyContext())
                    {
                        var deal = context.Deals.FirstOrDefault(d => d.Title == dealTitle);
                        if (deal != null)
                            filtered = filtered.Where(e => e.DealId == deal.Id);
                    }
                }
            }
            
            var result = filtered.ToList();
            Debug.WriteLine($"Итоговое количество после фильтрации: {result.Count}");
            Debug.WriteLine($"=== Конец фильтрации ===");
            
            return result;
        }

        private void ShowDayEvents(int day)
        {
            var selectedDate = new DateTime(currentMonth.Year, currentMonth.Month, day);
            var dayEvents = allEvents.Where(e => 
                e.StartDate.Date == selectedDate.Date).ToList();
            
            var window = new DayEventsWindow(dayEvents, selectedDate);
            window.ShowDialog();
            
            // Перезагружаем события после закрытия окна
            LoadEvents();
            UpdateCalendar();
        }

        private void PreviousMonth_Click(object sender, RoutedEventArgs e)
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
            UpdateCalendar();
        }

        private void AddEvent_Click(object sender, RoutedEventArgs e)
        {
            var window = new AddEventWindow();
            if (window.ShowDialog() == true)
            {
                LoadEvents();
                UpdateCalendar();
            }
        }
    }
}
