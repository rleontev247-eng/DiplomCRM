using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;

namespace MyFirstCRM
{
    public partial class DayEventsWindow : Window
    {
        private List<CalendarEvent> events;
        private DateTime currentDate;
        private List<CalendarEventViewModel> eventViewModels;

        public bool EventsChanged { get; private set; } = false;

        public DayEventsWindow(List<CalendarEvent> dayEvents, DateTime date)
        {
            InitializeComponent();
            events = dayEvents;
            currentDate = date;
            
            InitializeWindow();
        }

        private void InitializeWindow()
        {
            DateText.Text = currentDate.ToString("dd MMMM yyyy", new System.Globalization.CultureInfo("ru-RU"));
            
            eventViewModels = events.Select(e => new CalendarEventViewModel(e)).ToList();
            EventsList.ItemsSource = eventViewModels;
            
            EventCountText.Text = $"Всего событий: {events.Count}";
        }

        private void AddEvent_Click(object sender, RoutedEventArgs e)
        {
            var addEventWindow = new AddEventWindow(currentDate);
            addEventWindow.Owner = this;
            
            if (addEventWindow.ShowDialog() == true)
            {
                EventsChanged = true;
                DialogResult = true;
                Close();
            }
        }

        private void EditEvent_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.DataContext is CalendarEventViewModel viewModel)
            {
                var editEventWindow = new AddEventWindow(currentDate, viewModel.Event);
                editEventWindow.Owner = this;
                
                if (editEventWindow.ShowDialog() == true)
                {
                    EventsChanged = true;
                    DialogResult = true;
                    Close();
                }
            }
        }

        private void DeleteEvent_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.DataContext is CalendarEventViewModel viewModel)
            {
                var result = MessageBox.Show(
                    $"Вы уверены, что хотите удалить событие \"{viewModel.Event.Title}\"?",
                    "Подтверждение удаления",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question
                );

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        using (var context = MultiUserSecurityManager.CreateCompanyContext())
                        {
                            context.CalendarEvents.Remove(viewModel.Event);
                            context.SaveChanges();
                        }
                        
                        EventsChanged = true;
                        DialogResult = true;
                        Close();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка удаления события: {ex.Message}", "Ошибка", 
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
        
        private void SendMessage_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.DataContext is CalendarEventViewModel viewModel)
            {
                var calendarEvent = viewModel.Event;
                
                // Проверяем, есть ли связанный клиент
                if (calendarEvent.Client != null)
                {
                    var messagesWindow = new MessagesWindow(
                        email: calendarEvent.Client.Email, 
                        phone: calendarEvent.Client.Phone
                    );
                    messagesWindow.Owner = this;
                    messagesWindow.ShowDialog();
                }
                else
                {
                    MessageBox.Show("Это событие не связано с клиентом. Нельзя отправить сообщение.", 
                        "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    public class CalendarEventViewModel
    {
        public CalendarEvent Event { get; }
        
        public string StartTimeFormatted
        {
            get
            {
                if (Event.IsAllDay)
                    return "Весь день";
                return Event.StartDate.ToString("HH:mm");
            }
        }
        
        public string EventTypeDisplay
        {
            get
            {
                return Event.EventType switch
                {
                    CalendarEventType.Meeting => "Встреча",
                    CalendarEventType.Call => "Звонок",
                    CalendarEventType.Task => "Задача",
                    CalendarEventType.Deadline => "Дедлайн",
                    CalendarEventType.Presentation => "Презентация",
                    CalendarEventType.FollowUp => "След. действие",
                    _ => "Другое"
                };
            }
        }
        
        public string PriorityDisplay
        {
            get
            {
                return Event.Priority switch
                {
                    Priority.Low => "Низкий",
                    Priority.Medium => "Средний",
                    Priority.High => "Высокий",
                    Priority.Critical => "Критический",
                    _ => "Средний"
                };
            }
        }
        
        public string DealInfo
        {
            get
            {
                if (Event.Deal != null)
                {
                    return $"📄 {Event.Deal.Title}";
                }
                return string.Empty;
            }
        }
        
        public string Location
        {
            get
            {
                if (!string.IsNullOrEmpty(Event.Location))
                {
                    return $"📍 {Event.Location}";
                }
                return string.Empty;
            }
        }
        
        public string ClientInfo
        {
            get
            {
                if (Event.Client != null)
                {
                    return $"👤 {Event.Client.Name}";
                }
                return string.Empty;
            }
        }
        
        public string ContactInfo
        {
            get
            {
                if (Event.Client != null)
                {
                    var contacts = new List<string>();
                    if (!string.IsNullOrEmpty(Event.Client.Email))
                        contacts.Add($"📧 {Event.Client.Email}");
                    if (!string.IsNullOrEmpty(Event.Client.Phone))
                        contacts.Add($"📱 {Event.Client.Phone}");
                    
                    return contacts.Count > 0 ? string.Join(" | ", contacts) : "Нет контактных данных";
                }
                return "Нет клиента";
            }
        }
        
        public CalendarEventViewModel(CalendarEvent calendarEvent)
        {
            Event = calendarEvent;
        }
    }
}
