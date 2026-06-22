using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.EntityFrameworkCore;

namespace MyFirstCRM
{
    public partial class AddEventWindow : Window
    {
        private DateTime selectedDate;
        private CalendarEvent editingEvent;
        private string selectedColor = "#3498db";

        public AddEventWindow()
        {
            InitializeComponent();
            selectedDate = DateTime.Now;
            editingEvent = null;
            InitializeControls();
            SetDefaultValues();
        }

        public AddEventWindow(DateTime date, CalendarEvent eventToEdit = null)
        {
            // Инициализация перед InitializeComponent
            selectedDate = date;
            editingEvent = eventToEdit;
            
            InitializeComponent();
            
            // Инициализация контролов после загрузки XAML
            InitializeControls();
            
            if (editingEvent != null)
            {
                LoadEventData();
                Title = "Редактировать событие";
            }
            else
            {
                SetDefaultValues();
            }
        }

        private void InitializeControls()
        {
            // Заполняем типы событий на русском
            EventTypeComboBox.Items.Add("Встреча");
            EventTypeComboBox.Items.Add("Звонок");
            EventTypeComboBox.Items.Add("Задача");
            EventTypeComboBox.Items.Add("Дедлайн");
            EventTypeComboBox.Items.Add("Презентация");
            EventTypeComboBox.SelectedIndex = 0;
            
            // Заполняем приоритеты
            PriorityComboBox.Items.Add("Низкий");
            PriorityComboBox.Items.Add("Средний");
            PriorityComboBox.Items.Add("Высокий");
            PriorityComboBox.Items.Add("Критический");
            PriorityComboBox.SelectedIndex = 1; // Средний по умолчанию
            
            // Заполняем напоминания
            ReminderComboBox.Items.Add("Без напоминания");
            ReminderComboBox.Items.Add("За 15 минут");
            ReminderComboBox.Items.Add("За 30 минут");
            ReminderComboBox.Items.Add("За 1 час");
            ReminderComboBox.Items.Add("За 2 часа");
            ReminderComboBox.Items.Add("За 1 день");
            ReminderComboBox.SelectedIndex = 3; // За 1 час по умолчанию
            
            // Загружаем сделки
            LoadDeals();

            // Устанавливаем обработчики
            IsAllDayCheckBox.Checked += (s, e) => ToggleTimeFields(false);
            IsAllDayCheckBox.Unchecked += (s, e) => ToggleTimeFields(true);
        }

        private void LoadDeals()
        {
            try
            {
                using (var context = MultiUserSecurityManager.CreateCompanyContext())
                {
                    var deals = context.Deals.Include(d => d.Client).ToList();
                    
                    DealComboBox.Items.Add("Без сделки");
                    
                    foreach (var deal in deals)
                    {
                        var clientName = deal.Client?.Name ?? "Без клиента";
                        DealComboBox.Items.Add($"{deal.Title} ({clientName})");
                    }
                    DealComboBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки сделок: {ex.Message}", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetDefaultValues()
        {
            StartDatePicker.SelectedDate = selectedDate;
            EndDatePicker.SelectedDate = selectedDate;
        }

        private void LoadEventData()
        {
            TitleTextBox.Text = editingEvent.Title;
            DescriptionTextBox.Text = editingEvent.Description;
            LocationTextBox.Text = editingEvent.Location;
            
            StartDatePicker.SelectedDate = editingEvent.StartDate;
            EndDatePicker.SelectedDate = editingEvent.EndDate;
            
            StartTimeTextBox.Text = editingEvent.StartDate.ToString("HH:mm");
            EndTimeTextBox.Text = editingEvent.EndDate.ToString("HH:mm");
            
            IsAllDayCheckBox.IsChecked = editingEvent.IsAllDay;
            
            // Устанавливаем тип события
            var eventTypeText = editingEvent.EventType switch
            {
                CalendarEventType.Meeting => "Встреча",
                CalendarEventType.Call => "Звонок",
                CalendarEventType.Task => "Задача",
                CalendarEventType.Deadline => "Дедлайн",
                CalendarEventType.Presentation => "Презентация",
                CalendarEventType.FollowUp => "След. действие",
                CalendarEventType.Other => "Другое",
                _ => "Другое"
            };
            EventTypeComboBox.SelectedItem = eventTypeText;
            
            // Устанавливаем приоритет
            var priorityText = editingEvent.Priority switch
            {
                Priority.Low => "Низкий",
                Priority.Medium => "Средний",
                Priority.High => "Высокий",
                Priority.Critical => "Критический",
                _ => "Средний"
            };
            PriorityComboBox.SelectedItem = priorityText;
            
            selectedColor = editingEvent.Color ?? "#3B82F4";
            
            ReminderComboBox.SelectedIndex = 0; // По умолчанию "За 1 час"
            
            // Выбираем связанную сделку
            if (editingEvent.DealId.HasValue)
            {
                try
                {
                    using (var context = MultiUserSecurityManager.CreateCompanyContext())
                    {
                        var dealInfo = context.Deals
                            .Where(d => d.Id == editingEvent.DealId.Value)
                            .Select(d => new { d.Title, ClientName = d.Client.Name })
                            .FirstOrDefault();
                        
                        if (dealInfo != null)
                        {
                            var clientName = dealInfo.ClientName ?? "Без клиента";
                            string dealText = $"{dealInfo.Title} ({clientName})";
                            for (int i = 0; i < DealComboBox.Items.Count; i++)
                            {
                                if (DealComboBox.Items[i].ToString() == dealText)
                                {
                                    DealComboBox.SelectedIndex = i;
                                    break;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка загрузки данных сделки: {ex.Message}", "Ошибка", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ToggleTimeFields(bool enabled)
        {
            StartTimeTextBox.IsEnabled = enabled;
            EndTimeTextBox.IsEnabled = enabled;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInput())
                return;

            try
            {
                using (var context = MultiUserSecurityManager.CreateCompanyContext())
                {
                    var calendarEvent = editingEvent ?? new CalendarEvent();
                    
                    calendarEvent.Title = TitleTextBox.Text.Trim();
                    calendarEvent.Description = DescriptionTextBox.Text?.Trim();
                    calendarEvent.Location = LocationTextBox.Text?.Trim();
                    
                    // Формируем даты
                    var startDate = StartDatePicker.SelectedDate ?? DateTime.Today;
                    var endDate = EndDatePicker.SelectedDate ?? DateTime.Today;
                    
                    if (IsAllDayCheckBox.IsChecked == true)
                    {
                        calendarEvent.StartDate = startDate.Date;
                        calendarEvent.EndDate = endDate.Date.AddDays(1).AddSeconds(-1);
                        calendarEvent.IsAllDay = true;
                    }
                    else
                    {
                        if (TimeSpan.TryParse(StartTimeTextBox.Text, out TimeSpan startTime))
                        {
                            calendarEvent.StartDate = startDate.Date.Add(startTime);
                        }
                        else
                        {
                            calendarEvent.StartDate = startDate.Date.AddHours(9);
                        }
                        
                        if (TimeSpan.TryParse(EndTimeTextBox.Text, out TimeSpan endTime))
                        {
                            calendarEvent.EndDate = endDate.Date.Add(endTime);
                        }
                        else
                        {
                            calendarEvent.EndDate = endDate.Date.AddHours(10);
                        }
                        calendarEvent.IsAllDay = false;
                    }
                    
                    // Конвертируем тип события из русского в enum
                    var selectedTypeText = EventTypeComboBox.SelectedItem.ToString();
                    calendarEvent.EventType = selectedTypeText switch
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
                    
                    // Конвертируем приоритет из русского в enum
                    var selectedPriorityText = PriorityComboBox.SelectedItem.ToString();
                    calendarEvent.Priority = selectedPriorityText switch
                    {
                        "Низкий" => Priority.Low,
                        "Средний" => Priority.Medium,
                        "Высокий" => Priority.High,
                        "Критический" => Priority.Critical,
                        _ => Priority.Medium
                    };
                    calendarEvent.Color = "#3B82F6";
                    
                    // Обрабатываем напоминание
                    var reminderText = ReminderComboBox.SelectedItem?.ToString() ?? "За 1 час";
                    calendarEvent.ReminderMinutes = reminderText switch
                    {
                        "Без напоминания" => 0,
                        "За 15 минут" => 15,
                        "За 30 минут" => 30,
                        "За 1 час" => 60,
                        "За 2 часа" => 120,
                        "За 1 день" => 1440,
                        _ => 60
                    };
                    
                    // Связь со сделкой
                    if (DealComboBox.SelectedIndex > 0)
                    {
                        var dealText = DealComboBox.SelectedItem.ToString();
                        // Извлекаем название сделки (до открывающей скобки)
                        var dealTitle = dealText.Contains("(") ? dealText.Substring(0, dealText.IndexOf("(")).Trim() : dealText;
                        
                        // Ищем DealId напрямую, не загружая сущность
                        var dealId = context.Deals
                            .Where(d => d.Title == dealTitle)
                            .Select(d => d.Id)
                            .FirstOrDefault();
                        
                        if (dealId != 0)
                        {
                            calendarEvent.DealId = dealId;
                        }
                    }
                    else
                    {
                        calendarEvent.DealId = null;
                    }
                    
                    calendarEvent.Status = EventStatus.Scheduled;
                    calendarEvent.UpdatedAt = DateTime.Now;
                    
                    if (editingEvent == null)
                    {
                        calendarEvent.CreatedAt = DateTime.Now;
                        context.CalendarEvents.Add(calendarEvent);
                    }
                    else
                    {
                        context.CalendarEvents.Update(calendarEvent);
                    }
                    
                    context.SaveChanges();
                    DialogResult = true;
                    Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка сохранения события: {ex.Message}", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool ValidateInput()
        {
            if (string.IsNullOrWhiteSpace(TitleTextBox.Text))
            {
                MessageBox.Show("Название события обязательно!", "Ошибка валидации", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TitleTextBox.Focus();
                return false;
            }
            
            if (!StartDatePicker.SelectedDate.HasValue)
            {
                MessageBox.Show("Дата начала обязательна!", "Ошибка валидации", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                StartDatePicker.Focus();
                return false;
            }
            
            if (!EndDatePicker.SelectedDate.HasValue)
            {
                MessageBox.Show("Дата окончания обязательна!", "Ошибка валидации", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                EndDatePicker.Focus();
                return false;
            }
            
            return true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
