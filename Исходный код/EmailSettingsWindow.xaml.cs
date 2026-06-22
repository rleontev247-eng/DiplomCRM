using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace MyFirstCRM
{
    public partial class EmailSettingsWindow : Window
    {
        public string SmtpHost => SmtpHostTextBox.Text.Trim();
        public int SmtpPort => int.TryParse(SmtpPortTextBox.Text.Trim(), out int port) ? port : 587;
        public string SmtpUser => SmtpUserTextBox.Text.Trim();
        public string SmtpPassword => SmtpPasswordBox.Password;
        public bool UseSsl => UseSslCheckBox.IsChecked == true;
        
        public EmailSettingsWindow()
        {
            InitializeComponent();
            LoadSavedSettings();
        }
        
        private void LoadSavedSettings()
        {
            // Загрузка сохраненных настроек из AppSettings
            var settings = AppSettings.Load();
            SmtpHostTextBox.Text = settings.SmtpHost;
            SmtpPortTextBox.Text = settings.SmtpPort.ToString();
            SmtpUserTextBox.Text = settings.SmtpUser;
            SmtpPasswordBox.Password = settings.SmtpPassword;
            UseSslCheckBox.IsChecked = settings.UseSsl;
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
        
        private async void TestButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SmtpHost) || 
                string.IsNullOrWhiteSpace(SmtpUser) || 
                string.IsNullOrWhiteSpace(SmtpPassword))
            {
                MessageBox.Show("Пожалуйста, заполните все поля для тестирования соединения", 
                              "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            TestButton.IsEnabled = false;
            TestButton.Content = "🔄 Тестирование...";
            
            try
            {
                var emailService = new EmailService(SmtpHost, SmtpPort, SmtpUser, SmtpPassword, UseSsl);
                
                // Используем новый метод тестирования с разными вариантами SSL/TLS
                var result = await emailService.TestConnectionAsync();
                
                if (result.Success)
                {
                    MessageBox.Show($"✅ {result.Message}", 
                                  "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"❌ {result.Message}", 
                                  "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Ошибка соединения: {ex.Message}", 
                              "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                TestButton.IsEnabled = true;
                TestButton.Content = "🧪 Тест соединения";
            }
        }
        
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SmtpHost) || 
                string.IsNullOrWhiteSpace(SmtpUser) || 
                string.IsNullOrWhiteSpace(SmtpPassword))
            {
                MessageBox.Show("Пожалуйста, заполните все обязательные поля", 
                              "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            if (SmtpPort <= 0 || SmtpPort > 65535)
            {
                MessageBox.Show("Пожалуйста, введите корректный номер порта (1-65535)", 
                              "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                SmtpPortTextBox.Focus();
                return;
            }
            
            // Сохранение настроек в AppSettings
            var settings = AppSettings.Load();
            settings.SmtpHost = SmtpHost;
            settings.SmtpPort = SmtpPort;
            settings.SmtpUser = SmtpUser;
            settings.SmtpPassword = SmtpPassword;
            settings.UseSsl = UseSsl;
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
