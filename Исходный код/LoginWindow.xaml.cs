using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media.Animation;
using System.Windows.Controls;
using System.Windows.Media;

namespace MyFirstCRM
{
    public partial class LoginWindow : Window
    {
        private DispatcherTimer _lockoutTimer;
        private DateTime _lockoutEndTime;

        public LoginWindow()
        {
            InitializeComponent();
            Loaded += LoginWindow_Loaded;

            // Загрузка данных безопасности
            SecurityManager.LoadSecurityData();

            // Настройка таймера блокировки
            _lockoutTimer = new DispatcherTimer();
            _lockoutTimer.Interval = TimeSpan.FromSeconds(1);
            _lockoutTimer.Tick += LockoutTimer_Tick;
        }

        private void LoginWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Анимация появления
            this.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.8)));

            // Проверка блокировки
            CheckLockoutStatus();

            // Показ кнопки экстренного доступа после 3 неудачных попыток
            if (SecurityManager.CurrentSecurity.FailedAttempts >= 3)
            {
                EmergencyAccessButton.Visibility = Visibility.Visible;
            }
        }

        private void CheckLockoutStatus()
        {
            if (SecurityManager.CurrentSecurity.FailedAttempts >= 5 &&
                SecurityManager.CurrentSecurity.LastFailedAttempt.HasValue)
            {
                _lockoutEndTime = SecurityManager.CurrentSecurity.LastFailedAttempt.Value.AddMinutes(15);

                if (DateTime.Now < _lockoutEndTime)
                {
                    // Система заблокирована
                    LockoutBorder.Visibility = Visibility.Visible;
                    PasswordBox.IsEnabled = false;
                    LoginButton.IsEnabled = false;
                    _lockoutTimer.Start();
                    UpdateLockoutDisplay();
                }
                else
                {
                    // Блокировка истекла
                    SecurityManager.CurrentSecurity.FailedAttempts = 0;
                    SecurityManager.SaveSecurityData();
                    LockoutBorder.Visibility = Visibility.Collapsed;
                    PasswordBox.IsEnabled = true;
                }
            }
        }

        private void LockoutTimer_Tick(object sender, EventArgs e)
        {
            UpdateLockoutDisplay();

            if (DateTime.Now >= _lockoutEndTime)
            {
                _lockoutTimer.Stop();
                SecurityManager.CurrentSecurity.FailedAttempts = 0;
                SecurityManager.SaveSecurityData();

                LockoutBorder.Visibility = Visibility.Collapsed;
                PasswordBox.IsEnabled = true;
                LoginButton.IsEnabled = true;
            }
        }

        private void UpdateLockoutDisplay()
        {
            var remaining = _lockoutEndTime - DateTime.Now;
            if (remaining.TotalSeconds > 0)
            {
                LockoutTimeText.Text = $"До разблокировки: {remaining:mm\\:ss}";
                LockoutProgress.Value = 100 - (remaining.TotalSeconds / (15 * 60) * 100);
            }
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            LoginButton.IsEnabled = PasswordBox.Password.Length > 0;
        }

        private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && LoginButton.IsEnabled)
            {
                LoginButton_Click(sender, e);
            }
        }

        private void ShowHintButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(SecurityManager.CurrentSecurity.Hint))
            {
                HintText.Text = $"Подсказка: {SecurityManager.CurrentSecurity.Hint}";
                HintBorder.Visibility = Visibility.Visible;

                // Автоматическое скрытие через 10 секунд
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
                timer.Tick += (s, args) =>
                {
                    timer.Stop();
                    HintBorder.Visibility = Visibility.Collapsed;
                };
                timer.Start();
            }
            else
            {
                MessageBox.Show("Подсказка для пароля не установлена.", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LoginButton.IsEnabled = false;
                PasswordBox.IsEnabled = false;

                // БЕЗ асинхронности, БЕЗ анимаций
                var isValid = SecurityManager.VerifyPassword(PasswordBox.Password);

                if (isValid)
                {
                    // ПРОСТОЙ успешный вход
                    this.Hide(); // Скрываем окно
                    App.SafeLaunchMain();
                    this.Close(); // Закрываем окно
                }
                else
                {
                    // Простая ошибка
                    ErrorMessage.Text = "Неверный пароль";
                    ErrorBorder.Visibility = Visibility.Visible;
                    LoginButton.IsEnabled = true;
                    PasswordBox.IsEnabled = true;
                    PasswordBox.Clear();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка");
                LoginButton.IsEnabled = true;
                PasswordBox.IsEnabled = true;
            }
        }

        private void EmergencyAccessButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "🚨 ЭКСТРЕННЫЙ ДОСТУП\n\n" +
                "Вы собираетесь удалить ВСЕ данные из системы, включая:\n" +
                "• Всех клиентов\n" +
                "• Все сделки\n" +
                "• Все настройки\n" +
                "• Мастер-пароль\n\n" +
                "Это действие НЕОБРАТИМО!\n\n" +
                "Введите 'УДАЛИТЬ' для подтверждения:",
                "КРИТИЧЕСКОЕ ПОДТВЕРЖДЕНИЕ",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.OK)
            {
                var inputDialog = new InputDialog("Подтверждение удаления",
                    "Введите 'УДАЛИТЬ' для подтверждения:", "УДАЛИТЬ");

                if (inputDialog.ShowDialog() == true && inputDialog.Answer == "УДАЛИТЬ")
                {
                    SecurityManager.EmergencyReset();
                }
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            this.DragMove();
        }
    }

    // Вспомогательный класс для диалога ввода
    // Вспомогательный класс для диалога ввода
    public class InputDialog : Window
    {
        public string Answer { get; private set; }

        public InputDialog(string title, string prompt, string defaultValue = "")
        {
            Title = title;
            Width = 300;
            Height = 180;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var stackPanel = new StackPanel { Margin = new Thickness(20) };

            stackPanel.Children.Add(new TextBlock
            {
                Text = prompt,
                Margin = new Thickness(0, 0, 0, 10)
            });

            var textBox = new TextBox
            {
                Text = defaultValue,
                Margin = new Thickness(0, 0, 0, 20)
            };

            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

            var okButton = new Button
            {
                Content = "OK",
                IsDefault = true,
                Margin = new Thickness(0, 0, 10, 0)
            };
            okButton.Click += (sender, e) =>
            {
                Answer = textBox.Text;
                DialogResult = true;
            };

            var cancelButton = new Button { Content = "Cancel", IsCancel = true };
            cancelButton.Click += (sender, e) => { DialogResult = false; };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);

            stackPanel.Children.Add(textBox);
            stackPanel.Children.Add(buttonPanel);

            Content = stackPanel;
        }
    }

}