using System;
using System.Windows;
using System.Windows.Controls;

namespace MyFirstCRM
{
    public partial class CreateNotificationDialog : Window
    {
        public CreateNotificationDialog()
        {
            InitializeComponent();
            DueDatePicker.SelectedDate = DateTime.Now.AddDays(1);
        }

        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TitleTextBox.Text))
            {
                MessageBox.Show("Введите заголовок напоминания!", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TitleTextBox.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(MessageTextBox.Text))
            {
                MessageBox.Show("Введите сообщение напоминания!", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                MessageTextBox.Focus();
                return;
            }

            var typeItem = (ComboBoxItem)TypeComboBox.SelectedItem;
            var type = (NotificationType)Enum.Parse(typeof(NotificationType), typeItem.Tag.ToString());

            var notification = new Notification
            {
                Title = TitleTextBox.Text.Trim(),
                Message = MessageTextBox.Text.Trim(),
                Type = type,
                DueDate = DueDatePicker.SelectedDate,
                Icon = GetIconForType(type),
                Color = GetColorForType(type)
            };

            NotificationManager.CreateNotification(notification);
            DialogResult = true;
            Close();
        }

        private string GetIconForType(NotificationType type)
        {
            return type switch
            {
                NotificationType.DealReminder => "💼",
                NotificationType.Deadline => "⏰",
                NotificationType.ClientAction => "👥",
                NotificationType.Info => "💡",
                _ => "🔔"
            };
        }

        private string GetColorForType(NotificationType type)
        {
            return type switch
            {
                NotificationType.DealReminder => "#3B82F6",
                NotificationType.Deadline => "#EF4444",
                NotificationType.ClientAction => "#8B5CF6",
                NotificationType.Info => "#10B981",
                _ => "#64748B"
            };
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}