using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace MyFirstCRM
{
    public partial class NotificationSettingsWindow : Window
    {
        // Email свойства
        public string SmtpHost => SmtpHostTextBox.Text.Trim();
        public int SmtpPort => int.TryParse(SmtpPortTextBox.Text.Trim(), out int port) ? port : 587;
        public string SmtpUser => SmtpUserTextBox.Text.Trim();
        public string SmtpPassword => SmtpPasswordBox.Password;
        public bool UseSsl => UseSslCheckBox.IsChecked == true;
        
        // SMS свойства
        public string SmsApiKey => SmsApiKeyBox.Password.Trim();
        public string SmsSenderName => SmsSenderNameBox.Text.Trim();
        public SmsProvider SmsProvider => Enum.TryParse<SmsProvider>(SmsProviderComboBox.SelectedValue?.ToString(), out var provider) ? provider : SmsProvider.SMSRU;
        public bool PreferEmail => PreferEmailCheckbox.IsChecked == true;
        public bool SendBoth => SendBothCheckbox.IsChecked == true;

        private NotificationService _notificationService;

        public NotificationSettingsWindow()
        {
            InitializeComponent();
            LoadSavedSettings();
            _notificationService = new NotificationService();
        }

        private void LoadSavedSettings()
        {
            var settings = AppSettings.Load();
            
            // Email настройки
            SmtpHostTextBox.Text = settings.SmtpHost;
            SmtpPortTextBox.Text = settings.SmtpPort.ToString();
            SmtpUserTextBox.Text = settings.SmtpUser;
            SmtpPasswordBox.Password = settings.SmtpPassword;
            UseSslCheckBox.IsChecked = settings.UseSsl;
            
            // SMS настройки
            SmsApiKeyBox.Password = settings.SmsApiKey ?? "";
            SmsSenderNameBox.Text = settings.SmsSenderName ?? "CRM";
            
            // Выбор провайдера SMS
            if (!string.IsNullOrEmpty(settings.SmsProvider))
            {
                foreach (ComboBoxItem item in SmsProviderComboBox.Items)
                {
                    if (item.Tag?.ToString() == settings.SmsProvider)
                    {
                        SmsProviderComboBox.SelectedItem = item;
                        break;
                    }
                }
            }
            
            // Дополнительные настройки
            PreferEmailCheckbox.IsChecked = settings.PreferEmailForNotifications;
            SendBothCheckbox.IsChecked = settings.SendBothChannels;
        }

        private void EmailTabButton_Click(object sender, RoutedEventArgs e)
        {
            SetActiveTab("email");
        }

        private void SmsTabButton_Click(object sender, RoutedEventArgs e)
        {
            SetActiveTab("sms");
        }

        private void TestTabButton_Click(object sender, RoutedEventArgs e)
        {
            SetActiveTab("test");
        }

        private void SetActiveTab(string tabName)
        {
            // Сбрасываем стили всех кнопок
            EmailTabButton.Style = (Style)FindResource("TabButtonStyle");
            SmsTabButton.Style = (Style)FindResource("TabButtonStyle");
            TestTabButton.Style = (Style)FindResource("TabButtonStyle");

            // Скрываем все панели
            EmailPanel.Visibility = Visibility.Collapsed;
            SmsPanel.Visibility = Visibility.Collapsed;
            TestPanel.Visibility = Visibility.Collapsed;

            // Активируем нужную вкладку
            switch (tabName.ToLower())
            {
                case "email":
                    EmailTabButton.Style = (Style)FindResource("ActiveTabButtonStyle");
                    EmailPanel.Visibility = Visibility.Visible;
                    break;
                case "sms":
                    SmsTabButton.Style = (Style)FindResource("ActiveTabButtonStyle");
                    SmsPanel.Visibility = Visibility.Visible;
                    break;
                case "test":
                    TestTabButton.Style = (Style)FindResource("ActiveTabButtonStyle");
                    TestPanel.Visibility = Visibility.Visible;
                    break;
            }
        }

        private void SmtpProviderComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SmtpProviderComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                var tag = selectedItem.Tag?.ToString();
                
                if (tag == "custom")
                {
                    SmtpHostTextBox.IsEnabled = true;
                    SmtpPortTextBox.IsEnabled = true;
                }
                else if (!string.IsNullOrEmpty(tag) && tag.Contains(":"))
                {
                    var parts = tag.Split(':');
                    SmtpHostTextBox.Text = parts[0];
                    SmtpPortTextBox.Text = parts[1];
                    SmtpHostTextBox.IsEnabled = false;
                    SmtpPortTextBox.IsEnabled = false;
                }
            }
        }

        private async void TestConnectionButton_Click(object sender, RoutedEventArgs e)
        {
            TestConnectionButton.IsEnabled = false;
            TestConnectionButton.Content = "🔄 Тестирование...";

            try
            {
                // Создаем сервисы с текущими настройками
                var emailService = new EmailService(SmtpHost, SmtpPort, SmtpUser, SmtpPassword, UseSsl);
                var smsService = new SmsService(SmsApiKey, SmsSenderName, SmsProvider);
                
                var results = new System.Text.StringBuilder();
                results.AppendLine("📊 Результаты тестирования соединения:\n");

                // Тест Email
                if (!string.IsNullOrEmpty(SmtpUser) && !string.IsNullOrEmpty(SmtpPassword))
                {
                    var emailTest = await emailService.TestConnectionAsync();
                    results.AppendLine($"📧 Email: {(emailTest.Success ? "✅" : "❌")} {emailTest.Message}");
                }
                else
                {
                    results.AppendLine("📧 Email: ⚠️ Не настроен");
                }

                // Тест SMS
                if (!string.IsNullOrEmpty(SmsApiKey))
                {
                    var smsTest = await smsService.TestConnectionAsync();
                    results.AppendLine($"📱 SMS: {(smsTest.Success ? "✅" : "❌")} {smsTest.Message}");
                }
                else
                {
                    results.AppendLine("📱 SMS: ⚠️ Не настроен");
                }

                MessageBox.Show(results.ToString(), "Результаты тестирования", 
                              MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Ошибка тестирования: {ex.Message}", 
                              "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                TestConnectionButton.IsEnabled = true;
                TestConnectionButton.Content = "🧪 Тест соединения";
            }
        }

        private async void TestEmailButton_Click(object sender, RoutedEventArgs e)
        {
            await TestNotification("email");
        }

        private async void TestSmsButton_Click(object sender, RoutedEventArgs e)
        {
            await TestNotification("sms");
        }

        private async void TestBothButton_Click(object sender, RoutedEventArgs e)
        {
            await TestNotification("both");
        }

        private async Task TestNotification(string type)
        {
            var testEmail = TestEmailTextBox.Text.Trim();
            var testPhone = TestPhoneTextBox.Text.Trim();
            var testMessage = TestMessageTextBox.Text.Trim();

            if (string.IsNullOrEmpty(testMessage))
            {
                MessageBox.Show("Введите тестовое сообщение", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Показываем результаты
            TestResultsBorder.Visibility = Visibility.Visible;
            TestResultsText.Text = "🔄 Отправка тестовых уведомлений...";

            try
            {
                // Создаем сервисы с текущими настройками
                var emailService = new EmailService(SmtpHost, SmtpPort, SmtpUser, SmtpPassword, UseSsl);
                var smsService = new SmsService(SmsApiKey, SmsSenderName, SmsProvider);
                var notificationService = new NotificationService(emailService, smsService);

                var results = new System.Text.StringBuilder();

                switch (type.ToLower())
                {
                    case "email":
                        if (string.IsNullOrEmpty(testEmail))
                        {
                            results.AppendLine("📧 Email: ❌ Введите email адрес");
                        }
                        else
                        {
                            var emailResult = await notificationService.SendEmailNotificationAsync(testEmail, "Тестовое уведомление", testMessage);
                            results.AppendLine($"📧 Email: {(emailResult.Success ? "✅ Отправлено" : "❌ " + emailResult.ErrorMessage)}");
                        }
                        break;

                    case "sms":
                        if (string.IsNullOrEmpty(testPhone))
                        {
                            results.AppendLine("📱 SMS: ❌ Введите номер телефона");
                        }
                        else
                        {
                            var smsResult = await notificationService.SendSmsNotificationAsync(testPhone, testMessage);
                            results.AppendLine($"📱 SMS: {(smsResult.Success ? "✅ Отправлено" : "❌ " + smsResult.ErrorMessage)}");
                        }
                        break;

                    case "both":
                        if (string.IsNullOrEmpty(testEmail) || string.IsNullOrEmpty(testPhone))
                        {
                            results.AppendLine("🔔 Оба канала: ❌ Введите email и номер телефона");
                        }
                        else
                        {
                            var bothResult = await notificationService.SendMultiChannelNotificationAsync(
                                testEmail, testPhone, "Тестовое уведомление", testMessage, NotificationType.Info, PreferEmail);
                            
                            results.AppendLine($"🔔 Мультиканал: {(bothResult.OverallSuccess ? "✅ Успешно" : "❌ Ошибка")}");
                            if (bothResult.EmailSuccess)
                                results.AppendLine("  📧 Email: ✅ Отправлено");
                            else if (!string.IsNullOrEmpty(bothResult.EmailError))
                                results.AppendLine($"  📧 Email: ❌ {bothResult.EmailError}");
                            
                            if (bothResult.SmsSuccess)
                                results.AppendLine("  📱 SMS: ✅ Отправлено");
                            else if (!string.IsNullOrEmpty(bothResult.SmsError))
                                results.AppendLine($"  📱 SMS: ❌ {bothResult.SmsError}");
                        }
                        break;
                }

                TestResultsText.Text = results.ToString();
            }
            catch (Exception ex)
            {
                TestResultsText.Text = $"❌ Ошибка: {ex.Message}";
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Валидация Email настроек
            if (!string.IsNullOrEmpty(SmtpUser) || !string.IsNullOrEmpty(SmtpPassword))
            {
                if (string.IsNullOrWhiteSpace(SmtpHost) || 
                    string.IsNullOrWhiteSpace(SmtpUser) || 
                    string.IsNullOrWhiteSpace(SmtpPassword))
                {
                    MessageBox.Show("Заполните все поля для Email настроек или оставьте их пустыми", 
                                  "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (SmtpPort <= 0 || SmtpPort > 65535)
                {
                    MessageBox.Show("Введите корректный номер порта (1-65535)", 
                                  "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    SmtpPortTextBox.Focus();
                    return;
                }
            }

            // Валидация SMS настроек
            if (!string.IsNullOrEmpty(SmsApiKey))
            {
                if (string.IsNullOrWhiteSpace(SmsSenderName))
                {
                    MessageBox.Show("Введите имя отправителя для SMS", 
                                  "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    SmsSenderNameBox.Focus();
                    return;
                }
            }

            // Сохранение настроек
            var settings = AppSettings.Load();
            
            // Email настройки
            settings.SmtpHost = SmtpHost;
            settings.SmtpPort = SmtpPort;
            settings.SmtpUser = SmtpUser;
            settings.SmtpPassword = SmtpPassword;
            settings.UseSsl = UseSsl;
            
            // SMS настройки
            settings.SmsApiKey = SmsApiKey;
            settings.SmsSenderName = SmsSenderName;
            settings.SmsProvider = SmsProvider.ToString();
            settings.PreferEmailForNotifications = PreferEmail;
            settings.SendBothChannels = SendBoth;
            
            settings.Save();

            MessageBox.Show("Настройки успешно сохранены!", 
                          "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

            this.DialogResult = true;
            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
