using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Shapes;

namespace MyFirstCRM
{
    public partial class MessagesWindow : Window
    {
        private EmailService _emailService;
        private SmsService _smsService;
        private NotificationService _notificationService;
        private string? _initialEmail;
        private string? _initialPhone;
        
        // Менеджер шаблонов сообщений
        private List<MessageTemplate> _messageTemplates = new();
        
        
        public MessagesWindow()
        {
            try
            {
                InitializeComponent();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка инициализации окна: {ex.Message}", "Критическая ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            
            // Добавляем обработчик события Loaded
            this.Loaded += MessagesWindow_Loaded;
        }
        
        // Новый конструктор для предзаполнения полей email и телефона
        public MessagesWindow(string? email = null, string? phone = null) : this()
        {
            _initialEmail = email;
            _initialPhone = phone;
        }
        
        private void MessagesWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                LoadServices();
                LoadTemplates();
                UpdateStatus();
                InitializeWindow();
                
                // Проверяем что элементы загружены
                if (ToEmailTextBox == null || BodyTextBox == null)
                {
                    MessageBox.Show("Ошибка загрузки элементов интерфейса", "Критическая ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                // Заполняем поля после загрузки
                if (!string.IsNullOrEmpty(_initialEmail))
                {
                    ToEmailTextBox.Text = _initialEmail;
                }
                
                if (!string.IsNullOrEmpty(_initialPhone))
                {
                    ToPhoneTextBox.Text = _initialPhone;
                }
                
                ToEmailTextBox.Focus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки сервисов: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void InitializeWindow()
        {
            // Добавляем возможность перетаскивания окна за заголовок
          //  this.MouseLeftButtonDown += (sender, e) => this.DragMove();
            
            // Инициализация счетчиков только после загрузки элементов
            if (BodyTextBox != null)
            {
                UpdateCharCount();
                UpdatePreview();
            }
        }
        
        private void LoadTemplates()
        {
            try
            {
                _messageTemplates = MessageTemplateManager.GetTemplates();
                
                // Обновляем ComboBox шаблонов
                if (TemplateComboBox != null)
                {
                    TemplateComboBox.Items.Clear();
                    TemplateComboBox.Items.Add("Выберите шаблон...");
                    
                    foreach (var template in _messageTemplates)
                    {
                        var comboBoxItem = new ComboBoxItem 
                        { 
                            Content = template.Name,
                            Tag = template.Id
                        };
                        TemplateComboBox.Items.Add(comboBoxItem);
                    }
                    
                    TemplateComboBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки шаблонов: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void LoadServices()
        {
            var settings = AppSettings.Load();
            
            _emailService = new EmailService(
                settings.SmtpHost,
                settings.SmtpPort,
                settings.SmtpUser,
                settings.SmtpPassword,
                settings.UseSsl
            );
            
            _smsService = new SmsService(
                settings.SmsApiKey,
                settings.SmsSenderName,
                Enum.Parse<SmsProvider>(settings.SmsProvider)
            );
            
            _notificationService = new NotificationService(_emailService, _smsService);
        }
        
        private void UpdateStatus()
        {
            bool emailConfigured = _emailService.IsConfigured();
            bool smsConfigured = _smsService.IsConfigured();
            
            if (emailConfigured && smsConfigured)
            {
                StatusBorder.Background = System.Windows.Media.Brushes.LightGreen;
                StatusBorder.BorderBrush = System.Windows.Media.Brushes.Green;
                StatusText.Text = "✅ Email и SMS настроены";
                StatusText.Foreground = System.Windows.Media.Brushes.DarkGreen;
                DetailedStatusText.Text = $"Email: {_emailService.GetConfigurationInfo()}\nSMS: {_smsService.GetConfigurationInfo()}";
                SendButton.IsEnabled = true;
            }
            else if (emailConfigured)
            {
                StatusBorder.Background = System.Windows.Media.Brushes.LightBlue;
                StatusBorder.BorderBrush = System.Windows.Media.Brushes.Blue;
                StatusText.Text = "✅ Email настроен, SMS не настроен";
                StatusText.Foreground = System.Windows.Media.Brushes.DarkBlue;
                DetailedStatusText.Text = $"Email: {_emailService.GetConfigurationInfo()}\nSMS: Не настроен";
                SendButton.IsEnabled = true;
            }
            else if (smsConfigured)
            {
                StatusBorder.Background = System.Windows.Media.Brushes.LightBlue;
                StatusBorder.BorderBrush = System.Windows.Media.Brushes.Blue;
                StatusText.Text = "✅ SMS настроен, Email не настроен";
                StatusText.Foreground = System.Windows.Media.Brushes.DarkBlue;
                DetailedStatusText.Text = $"Email: Не настроен\nSMS: {_smsService.GetConfigurationInfo()}";
                SendButton.IsEnabled = true;
            }
            else
            {
                StatusBorder.Background = System.Windows.Media.Brushes.LightYellow;
                StatusBorder.BorderBrush = System.Windows.Media.Brushes.Orange;
                StatusText.Text = "⚠️ Email или SMS не настроены";
                StatusText.Foreground = System.Windows.Media.Brushes.DarkOrange;
                DetailedStatusText.Text = "Настройте в настройках сообщений";
                SendButton.IsEnabled = false;
            }
        }
        
        private void MessageTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateFieldVisibility();
            UpdateCharCount();
            UpdatePreview();
        }
        
        private void UpdateFieldVisibility()
        {
            if (MessageTypeComboBox?.SelectedItem is ComboBoxItem selectedItem)
            {
                var messageType = selectedItem.Tag?.ToString();
                
                switch (messageType)
                {
                    case "Email":
                        if (ToEmailTextBox != null) ToEmailTextBox.IsEnabled = true;
                        if (ToPhoneTextBox != null) ToPhoneTextBox.IsEnabled = false;
                        if (SubjectTextBox != null) SubjectTextBox.IsEnabled = true;
                        break;
                    case "SMS":
                        if (ToEmailTextBox != null) ToEmailTextBox.IsEnabled = false;
                        if (ToPhoneTextBox != null) ToPhoneTextBox.IsEnabled = true;
                        if (SubjectTextBox != null) SubjectTextBox.IsEnabled = false; // SMS не имеет темы
                        break;
                    case "Both":
                        if (ToEmailTextBox != null) ToEmailTextBox.IsEnabled = true;
                        if (ToPhoneTextBox != null) ToPhoneTextBox.IsEnabled = true;
                        if (SubjectTextBox != null) SubjectTextBox.IsEnabled = true;
                        break;
                }
            }
        }
        
        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            if (MessageTypeComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                var messageType = selectedItem.Tag?.ToString();
                
                switch (messageType)
                {
                    case "Email":
                        await SendEmail();
                        break;
                    case "SMS":
                        await SendSms();
                        break;
                    case "Both":
                        await SendBoth();
                        break;
                }
            }
        }
        
        private async Task SendEmail()
        {
            if (!ValidateEmail()) return;
            
            LoadingOverlay.Visibility = Visibility.Visible;
            SendButton.IsEnabled = false;
            
            try
            {
                var (success, errorMessage) = await _emailService.SendEmailAsync(
                    ToEmailTextBox.Text.Trim(),
                    SubjectTextBox.Text.Trim(),
                    BodyTextBox.Text.Trim()
                );
                
                LoadingOverlay.Visibility = Visibility.Collapsed;
                
                if (success)
                {
                    MessageBox.Show("Email успешно отправлен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    ClearFields();
                }
                else
                {
                    MessageBox.Show($"Ошибка отправки Email: {errorMessage}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                MessageBox.Show($"Произошла ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SendButton.IsEnabled = true;
            }
        }
        
        private async Task SendSms()
        {
            if (!ValidateSms()) return;
            
            LoadingOverlay.Visibility = Visibility.Visible;
            SendButton.IsEnabled = false;
            
            try
            {
                var (success, errorMessage) = await _smsService.SendSmsAsync(
                    ToPhoneTextBox.Text.Trim(),
                    BodyTextBox.Text.Trim()
                );
                
                LoadingOverlay.Visibility = Visibility.Collapsed;
                
                if (success)
                {
                    MessageBox.Show("SMS успешно отправлено!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    ClearFields();
                }
                else
                {
                    MessageBox.Show($"Ошибка отправки SMS: {errorMessage}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                MessageBox.Show($"Произошла ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SendButton.IsEnabled = true;
            }
        }
        
        private async Task SendBoth()
        {
            if (!ValidateBoth()) return;
            
            LoadingOverlay.Visibility = Visibility.Visible;
            SendButton.IsEnabled = false;
            
            try
            {
                var result = await _notificationService.SendMultiChannelNotificationAsync(
                    ToEmailTextBox.Text.Trim(),
                    ToPhoneTextBox.Text.Trim(),
                    SubjectTextBox.Text.Trim(),
                    BodyTextBox.Text.Trim(),
                    NotificationType.Info
                );
                
                LoadingOverlay.Visibility = Visibility.Collapsed;
                
                if (result.OverallSuccess)
                {
                    string message = "Сообщение отправлено!\n\n";
                    if (result.EmailSuccess) message += "✅ Email: доставлен\n";
                    else if (!string.IsNullOrEmpty(result.EmailError)) message += $"❌ Email: {result.EmailError}\n";
                    
                    if (result.SmsSuccess) message += "✅ SMS: доставлено\n";
                    else if (!string.IsNullOrEmpty(result.SmsError)) message += $"❌ SMS: {result.SmsError}\n";
                    
                    MessageBox.Show(message, "Результат отправки", MessageBoxButton.OK, MessageBoxImage.Information);
                    ClearFields();
                }
                else
                {
                    string message = "Ошибка отправки!\n\n";
                    if (!string.IsNullOrEmpty(result.EmailError)) message += $"Email: {result.EmailError}\n";
                    if (!string.IsNullOrEmpty(result.SmsError)) message += $"SMS: {result.SmsError}\n";
                    
                    MessageBox.Show(message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                MessageBox.Show($"Произошла ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SendButton.IsEnabled = true;
            }
        }
        
        private bool ValidateEmail()
        {
            if (string.IsNullOrWhiteSpace(ToEmailTextBox.Text))
            {
                MessageBox.Show("Пожалуйста, введите email получателя", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                ToEmailTextBox.Focus();
                return false;
            }
            
            if (string.IsNullOrWhiteSpace(SubjectTextBox.Text))
            {
                MessageBox.Show("Пожалуйста, введите тему сообщения", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                SubjectTextBox.Focus();
                return false;
            }
            
            if (string.IsNullOrWhiteSpace(BodyTextBox.Text))
            {
                MessageBox.Show("Пожалуйста, введите текст сообщения", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                BodyTextBox.Focus();
                return false;
            }
            
            if (!IsValidEmail(ToEmailTextBox.Text))
            {
                MessageBox.Show("Пожалуйста, введите корректный email адрес", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                ToEmailTextBox.Focus();
                return false;
            }
            
            return true;
        }
        
        private bool ValidateSms()
        {
            if (string.IsNullOrWhiteSpace(ToPhoneTextBox.Text))
            {
                MessageBox.Show("Пожалуйста, введите номер телефона", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                ToPhoneTextBox.Focus();
                return false;
            }
            
            if (string.IsNullOrWhiteSpace(BodyTextBox.Text))
            {
                MessageBox.Show("Пожалуйста, введите текст сообщения", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                BodyTextBox.Focus();
                return false;
            }
            
            return true;
        }
        
        private bool ValidateBoth()
        {
            if (string.IsNullOrWhiteSpace(ToEmailTextBox.Text) && string.IsNullOrWhiteSpace(ToPhoneTextBox.Text))
            {
                MessageBox.Show("Пожалуйста, введите email или номер телефона", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                ToEmailTextBox.Focus();
                return false;
            }
            
            if (!string.IsNullOrEmpty(ToEmailTextBox.Text) && !IsValidEmail(ToEmailTextBox.Text))
            {
                MessageBox.Show("Пожалуйста, введите корректный email адрес", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                ToEmailTextBox.Focus();
                return false;
            }
            
            if (string.IsNullOrWhiteSpace(BodyTextBox.Text))
            {
                MessageBox.Show("Пожалуйста, введите текст сообщения", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                BodyTextBox.Focus();
                return false;
            }
            
            return true;
        }
        
        private void ClearFields()
        {
            ToEmailTextBox.Text = string.Empty;
            ToPhoneTextBox.Text = "+7";
            SubjectTextBox.Text = string.Empty;
            BodyTextBox.Text = string.Empty;
            ToEmailTextBox.Focus();
        }
        
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        
        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }
        
        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
                MaximizeButton.Content = "□";
            }
            else
            {
                this.WindowState = WindowState.Maximized;
                MaximizeButton.Content = "❐";
            }
        }
        
        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                MaximizeButton.Content = "❐";
                // Скрываем ресайзеры при максимизации
                HideResizeElements();
            }
            else
            {
                MaximizeButton.Content = "□";
                // Показываем ресайзеры при нормальном состоянии
                ShowResizeElements();
            }
        }
        
        private void HideResizeElements()
        {
            if (ResizeGrip != null) ResizeGrip.Visibility = Visibility.Collapsed;
            if (TopResize != null) TopResize.Visibility = Visibility.Collapsed;
            if (LeftResize != null) LeftResize.Visibility = Visibility.Collapsed;
            if (RightResize != null) RightResize.Visibility = Visibility.Collapsed;
            if (BottomResize != null) BottomResize.Visibility = Visibility.Collapsed;
            if (TopLeftResize != null) TopLeftResize.Visibility = Visibility.Collapsed;
            if (TopRightResize != null) TopRightResize.Visibility = Visibility.Collapsed;
            if (BottomLeftResize != null) BottomLeftResize.Visibility = Visibility.Collapsed;
            if (BottomRightResize != null) BottomRightResize.Visibility = Visibility.Collapsed;
        }
        
        private void ShowResizeElements()
        {
            if (ResizeGrip != null) ResizeGrip.Visibility = Visibility.Visible;
            if (TopResize != null) TopResize.Visibility = Visibility.Visible;
            if (LeftResize != null) LeftResize.Visibility = Visibility.Visible;
            if (RightResize != null) RightResize.Visibility = Visibility.Visible;
            if (BottomResize != null) BottomResize.Visibility = Visibility.Visible;
            if (TopLeftResize != null) TopLeftResize.Visibility = Visibility.Visible;
            if (TopRightResize != null) TopRightResize.Visibility = Visibility.Visible;
            if (BottomLeftResize != null) BottomLeftResize.Visibility = Visibility.Visible;
            if (BottomRightResize != null) BottomRightResize.Visibility = Visibility.Visible;
        }
        
        private void BorderResize_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Rectangle rectangle)
            {
                IntPtr windowHandle = new WindowInteropHelper(this).Handle;
                HwndSource source = HwndSource.FromHwnd(windowHandle);
                
                if (source != null)
                {
                    ResizeDirection direction = GetResizeDirection(rectangle.Name);
                    SendMessage(windowHandle, WM_SYSCOMMAND, (IntPtr)((int)direction | SC_SIZE), IntPtr.Zero);
                }
            }
        }
        
        private ResizeDirection GetResizeDirection(string elementName)
        {
            return elementName switch
            {
                "TopResize" => ResizeDirection.Top,
                "LeftResize" => ResizeDirection.Left,
                "RightResize" => ResizeDirection.Right,
                "BottomResize" => ResizeDirection.Bottom,
                "TopLeftResize" => ResizeDirection.TopLeft,
                "TopRightResize" => ResizeDirection.TopRight,
                "BottomLeftResize" => ResizeDirection.BottomLeft,
                "BottomRightResize" => ResizeDirection.BottomRight,
                _ => ResizeDirection.Right
            };
        }
        
        // Win32 API константы и методы
        private const int WM_SYSCOMMAND = 0x0112;
        private const int SC_SIZE = 0xF000;
        
        private enum ResizeDirection
        {
            Left = 1,
            Right = 2,
            Top = 3,
            TopLeft = 4,
            TopRight = 5,
            Bottom = 6,
            BottomLeft = 7,
            BottomRight = 8
        }
        
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);
        
        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new NotificationSettingsWindow();
            settingsWindow.Owner = this;
            
            if (settingsWindow.ShowDialog() == true)
            {
                LoadServices();
                UpdateStatus();
            }
        }
        
        private void TemplateComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TemplateComboBox == null || SubjectTextBox == null || BodyTextBox == null)
                return;
                
            if (TemplateComboBox.SelectedItem is ComboBoxItem selectedItem && 
                selectedItem.Tag is string templateId && 
                templateId != "Выберите шаблон...")
            {
                var template = _messageTemplates.FirstOrDefault(t => t.Id == templateId);
                if (template != null)
                {
                    SubjectTextBox.Text = template.Subject;
                    BodyTextBox.Text = template.Body;
                    UpdateCharCount();
                    UpdatePreview();
                }
            }
        }
        
        private void TemplateComboBox_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (DeleteTemplateMenuItem != null && TemplateComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                var templateId = selectedItem.Tag?.ToString();
                if (string.IsNullOrEmpty(templateId) || templateId == "Выберите шаблон...")
                {
                    DeleteTemplateMenuItem.IsEnabled = false;
                }
                else
                {
                    var template = _messageTemplates.FirstOrDefault(t => t.Id == templateId);
                    DeleteTemplateMenuItem.IsEnabled = template?.IsCustom == true;
                }
            }
        }
        
        private void DeleteTemplateMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (TemplateComboBox.SelectedItem is ComboBoxItem selectedItem && 
                selectedItem.Tag is string templateId && 
                templateId != "Выберите шаблон...")
            {
                var template = _messageTemplates.FirstOrDefault(t => t.Id == templateId);
                if (template != null && template.IsCustom)
                {
                    var result = MessageBox.Show(
                        $"Вы уверены, что хотите удалить шаблон \"{template.Name}\"?",
                        "Подтверждение удаления",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                    
                    if (result == MessageBoxResult.Yes)
                    {
                        MessageTemplateManager.DeleteTemplate(templateId);
                        LoadTemplates(); // Перезагружаем список шаблонов
                        MessageBox.Show("Шаблон успешно удален!", "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
        }
        
        private void BodyTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateCharCount();
            UpdatePreview();
        }
        
        private void ToEmailTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdatePreview();
        }
        
        private void ToPhoneTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdatePreview();
        }
        
        private void UpdateCharCount()
        {
            if (BodyTextBox == null || CharCountText == null || SmsCountText == null)
                return;
                
            var text = BodyTextBox.Text ?? "";
            var charCount = text.Length;
            
            CharCountText.Text = $"{charCount} символов";
            
            // Показываем количество SMS для SMS типа
            var messageType = GetSelectedMessageType();
            if (messageType == "SMS" || messageType == "Both")
            {
                var smsCount = CalculateSmsCount(text);
                SmsCountText.Text = $"{smsCount} SMS";
                SmsCountText.Visibility = Visibility.Visible;
            }
            else
            {
                SmsCountText.Visibility = Visibility.Collapsed;
            }
        }
        
        private int CalculateSmsCount(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            
            // Упрощенный расчет: 1 SMS = 160 символов для латиницы, 70 для кириллицы
            var hasCyrillic = text.Any(c => c >= 'А' && c <= 'Я' || c >= 'а' && c <= 'я');
            var smsLength = hasCyrillic ? 70 : 160;
            
            return (int)Math.Ceiling((double)text.Length / smsLength);
        }
        
        private void UpdatePreview()
        {
            if (ToEmailTextBox == null || ToPhoneTextBox == null || SubjectTextBox == null || 
                BodyTextBox == null || PreviewText == null || PreviewSubText == null || SendButton == null)
                return;
                
            var email = ToEmailTextBox.Text?.Trim();
            var phone = ToPhoneTextBox.Text?.Trim();
            var subject = SubjectTextBox.Text?.Trim();
            var body = BodyTextBox.Text?.Trim();
            
            var messageType = GetSelectedMessageType();
            var hasValidData = false;
            var previewText = "Сообщение готово к отправке";
            var previewSubText = "";
            
            switch (messageType)
            {
                case "Email":
                    hasValidData = !string.IsNullOrEmpty(email) && IsValidEmail(email) && !string.IsNullOrEmpty(body);
                    previewSubText = hasValidData ? $"Email: {email}" : "Заполните email и текст сообщения";
                    break;
                case "SMS":
                    hasValidData = !string.IsNullOrEmpty(phone) && !string.IsNullOrEmpty(body);
                    previewSubText = hasValidData ? $"SMS: {phone}" : "Заполните телефон и текст сообщения";
                    break;
                case "Both":
                    var hasEmail = !string.IsNullOrEmpty(email) && IsValidEmail(email);
                    var hasPhone = !string.IsNullOrEmpty(phone);
                    hasValidData = (hasEmail || hasPhone) && !string.IsNullOrEmpty(body);
                    
                    if (hasValidData)
                    {
                        var channels = new List<string>();
                        if (hasEmail) channels.Add($"Email: {email}");
                        if (hasPhone) channels.Add($"SMS: {phone}");
                        previewSubText = string.Join(" | ", channels);
                    }
                    else
                    {
                        previewSubText = "Заполните хотя бы один получатель и текст сообщения";
                    }
                    break;
            }
            
            PreviewText.Text = previewText;
            PreviewSubText.Text = previewSubText;
            
            // Обновляем состояние кнопки отправки
            SendButton.IsEnabled = hasValidData;
        }
        
        private string GetSelectedMessageType()
        {
            if (MessageTypeComboBox?.SelectedItem is ComboBoxItem selectedItem)
            {
                return selectedItem.Tag?.ToString() ?? "Email";
            }
            return "Email";
        }
        
        private void SaveTemplateButton_Click(object sender, RoutedEventArgs e)
        {
            if (SubjectTextBox == null || BodyTextBox == null)
            {
                MessageBox.Show("Окно еще не полностью загружено. Попробуйте позже.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            if (string.IsNullOrWhiteSpace(SubjectTextBox.Text) && string.IsNullOrWhiteSpace(BodyTextBox.Text))
            {
                MessageBox.Show("Сначала введите тему и/или текст сообщения для сохранения шаблона.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            try
            {
                // Простое диалоговое окно для ввода имени
                var result = Microsoft.VisualBasic.Interaction.InputBox("Введите имя шаблона:", "Новый пользовательский шаблон", "");
                
                if (!string.IsNullOrEmpty(result))
                {
                    var templateName = result.Trim();
                    if (string.IsNullOrEmpty(templateName))
                    {
                        MessageBox.Show("Имя шаблона не может быть пустым.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    
                    var newTemplate = new MessageTemplate
                    {
                        Name = templateName,
                        Subject = SubjectTextBox.Text.Trim(),
                        Body = BodyTextBox.Text.Trim(),
                        IsCustom = true
                    };
                    
                    MessageTemplateManager.SaveTemplate(newTemplate);
                    LoadTemplates(); // Перезагружаем список шаблонов
                    
                    MessageBox.Show("Шаблон успешно сохранен!", "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении шаблона: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }
    }
}
