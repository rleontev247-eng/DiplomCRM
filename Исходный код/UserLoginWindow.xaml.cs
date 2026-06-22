using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace MyFirstCRM
{
    public partial class UserLoginWindow : Window
    {
        private int _failedAttempts = 0;
        private DispatcherTimer? _lockoutTimer;
        private DateTime _lockoutEndTime;

        public UserLoginWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Инициализация базы данных
            MultiUserSecurityManager.InitializeGlobalDatabase();

            // Если нет ни одной компании, предлагаем создать
            if (!MultiUserSecurityManager.HasAnyCompanies())
            {
                var result = MessageBox.Show(
                    "Нет зарегистрированных компаний.\n\nСоздать новую компанию?",
                    "Первый запуск",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    var regWindow = new CompanyRegistrationWindow();
                    regWindow.Show();
                    this.Close();
                    return;
                }
            }

            // Анимация появления
            this.BeginAnimation(OpacityProperty,
                new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.5)));
        }

        private void OnFieldChanged(object sender, RoutedEventArgs e)
        {
            ValidateForm();
        }

        private void OnPasswordKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && LoginButton.IsEnabled)
            {
                OnLoginClick(sender, e);
            }
        }

        private void ValidateForm()
        {
            bool isValid = !string.IsNullOrWhiteSpace(CompanyCodeTextBox.Text) &&
                          !string.IsNullOrWhiteSpace(UsernameTextBox.Text) &&
                          !string.IsNullOrEmpty(PasswordBox.Password);

            LoginButton.IsEnabled = isValid;

            // Скрываем ошибку при изменении полей
            ErrorBorder.Visibility = Visibility.Collapsed;
        }

        private void OnLoginClick(object sender, RoutedEventArgs e)
        {
            try
            {
                LoginButton.IsEnabled = false;
                ErrorBorder.Visibility = Visibility.Collapsed;

                var result = MultiUserSecurityManager.Login(
                    CompanyCodeTextBox.Text.Trim(),
                    UsernameTextBox.Text.Trim(),
                    PasswordBox.Password
                );

                if (result.Success)
                {
                    // Успешный вход - открываем главное окно
                    this.Hide();
                    App.SafeLaunchMain();
                    this.Close();
                }
                else
                {
                    _failedAttempts++;
                    ErrorMessage.Text = result.Message;
                    ErrorBorder.Visibility = Visibility.Visible;

                    // Показываем кнопку подсказки после 2 неудачных попыток
                    if (_failedAttempts >= 2)
                    {
                        ShowHintButton.Visibility = Visibility.Visible;
                    }

                    // Блокировка после 5 неудачных попыток
                    if (_failedAttempts >= 5)
                    {
                        StartLockout();
                    }
                    else
                    {
                        LoginButton.IsEnabled = true;
                        PasswordBox.Clear();
                        PasswordBox.Focus();
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorMessage.Text = $"Ошибка: {ex.Message}";
                ErrorBorder.Visibility = Visibility.Visible;
                LoginButton.IsEnabled = true;
            }
        }

        private void StartLockout()
        {
            _lockoutEndTime = DateTime.Now.AddMinutes(15);

            _lockoutTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _lockoutTimer.Tick += (s, e) =>
            {
                var remaining = _lockoutEndTime - DateTime.Now;
                if (remaining.TotalSeconds <= 0)
                {
                    _lockoutTimer?.Stop();
                    _failedAttempts = 0;
                    ErrorMessage.Text = "Блокировка снята. Можете попробовать снова.";
                    ErrorBorder.Background = System.Windows.Media.Brushes.LightGreen;
                    LoginButton.IsEnabled = true;
                    ShowHintButton.Visibility = Visibility.Collapsed;
                }
                else
                {
                    ErrorMessage.Text = $"Слишком много попыток. Попробуйте через {remaining:mm\\:ss}";
                }
            };
            _lockoutTimer.Start();

            ErrorMessage.Text = "Слишком много попыток. Попробуйте через 15 минут";
            ErrorBorder.Visibility = Visibility.Visible;
            LoginButton.IsEnabled = false;
        }

        private void OnShowHintClick(object sender, RoutedEventArgs e)
        {
            try
            {
                using var context = new GlobalDbContext();
                var company = context.Companies
                    .FirstOrDefault(c => c.CompanyCode.ToUpper() == CompanyCodeTextBox.Text.Trim().ToUpper());

                if (company == null)
                {
                    HintText.Text = "Компания не найдена";
                    HintBorder.Visibility = Visibility.Visible;
                    return;
                }

                var user = context.Users
                    .FirstOrDefault(u => u.CompanyId == company.Id &&
                                        u.Username.ToLower() == UsernameTextBox.Text.Trim().ToLower());

                if (user?.PasswordHint != null)
                {
                    HintText.Text = $"Подсказка: {user.PasswordHint}";
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
                    HintText.Text = "Подсказка не установлена. Обратитесь к администратору компании.";
                    HintBorder.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                HintText.Text = $"Ошибка: {ex.Message}";
                HintBorder.Visibility = Visibility.Visible;
            }
        }

        private void OnRegisterCompanyClick(object sender, RoutedEventArgs e)
        {
            var regWindow = new CompanyRegistrationWindow();
            regWindow.Show();
            this.Close();
        }

        private void OnDeleteAllDataClick(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "⚠️ ВНИМАНИЕ! Это действие удалит все данные CRM:\n\n" +
                "• Всех клиентов и сделки\n" +
                "• Все расходы и доходы\n" +
                "• Все события календаря\n" +
                "• Все задачи и уведомления\n" +
                "• Все настройки и логи\n\n" +
                "Пользователи и компании НЕ будут удалены.\n" +
                "Это действие НЕВОЗМОЖНО отменить!\n\n" +
                "Вы уверены, что хотите удалить все данные?",
                "Удаление данных CRM",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // Второе подтверждение
                    var confirmResult = MessageBox.Show(
                        "Последнее предупреждение!\n\n" +
                        "После удаления все данные будут потеряны навсегда.\n" +
                        "Продолжить удаление?",
                        "Подтверждение удаления",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Exclamation);

                    if (confirmResult == MessageBoxResult.Yes)
                    {
                        try
                        {
                            // Показываем информацию о процессе удаления
                            MessageBox.Show(
                                "Начинаю удаление данных...\n" +
                                "Это может занять несколько секунд.",
                                "Удаление данных",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);

                            DataManager.DeleteAllCRMData();
                            
                            MessageBox.Show(
                                "✅ Все данные успешно удалены!\n\n" +
                                "Удалено:\n" +
                                "• Клиенты и сделки\n" +
                                "• Расходы и доходы\n" +
                                "• События календаря\n" +
                                "• Задачи и уведомления\n" +
                                "• Настройки и логи\n\n" +
                                "Пользователи и компании сохранены.\n" +
                                "Программа будет перезапущена.",
                                "Удаление завершено",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);

                            // Правильный перезапуск приложения
                            var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
                            var startInfo = new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = currentProcess.MainModule.FileName,
                                WorkingDirectory = Environment.CurrentDirectory
                            };
                            
                            System.Diagnostics.Process.Start(startInfo);
                            Application.Current.Shutdown();
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(
                                $"❌ Ошибка при удалении данных:\n{ex.Message}\n\n" +
                                "Попробуйте перезапустить программу с правами администратора.",
                                "Ошибка",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"❌ Ошибка при удалении данных:\n{ex.Message}",
                        "Ошибка",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }
    }
}
