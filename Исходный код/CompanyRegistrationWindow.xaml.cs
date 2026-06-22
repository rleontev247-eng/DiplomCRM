using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MyFirstCRM
{
    public partial class CompanyRegistrationWindow : Window
    {
        public CompanyRegistrationWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Инициализация базы данных
            MultiUserSecurityManager.InitializeGlobalDatabase();
        }

        private void OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            UpdatePasswordStrength();
            ValidateForm();
        }

        private void OnFieldChanged(object sender, RoutedEventArgs e)
        {
            ValidateForm();
        }

        private void OnAgreementChanged(object sender, RoutedEventArgs e)
        {
            ValidateForm();
        }

        private void UpdatePasswordStrength()
        {
            var strength = MultiUserSecurityManager.CheckPasswordStrength(PasswordBox.Password);

            StrengthBar.Value = strength switch
            {
                MultiUserSecurityManager.PasswordStrength.Weak => 1,
                MultiUserSecurityManager.PasswordStrength.Medium => 2,
                MultiUserSecurityManager.PasswordStrength.Strong => 3,
                MultiUserSecurityManager.PasswordStrength.VeryStrong => 4,
                _ => 0
            };

            StrengthText.Text = strength switch
            {
                MultiUserSecurityManager.PasswordStrength.Weak => "Сложность: слабый",
                MultiUserSecurityManager.PasswordStrength.Medium => "Сложность: средний",
                MultiUserSecurityManager.PasswordStrength.Strong => "Сложность: сильный",
                MultiUserSecurityManager.PasswordStrength.VeryStrong => "Сложность: очень сильный",
                _ => "Сложность: не задан"
            };

            StrengthBar.Foreground = strength switch
            {
                MultiUserSecurityManager.PasswordStrength.Weak => new SolidColorBrush(Colors.Red),
                MultiUserSecurityManager.PasswordStrength.Medium => new SolidColorBrush(Colors.Orange),
                MultiUserSecurityManager.PasswordStrength.Strong => new SolidColorBrush(Colors.Green),
                MultiUserSecurityManager.PasswordStrength.VeryStrong => new SolidColorBrush(Colors.Blue),
                _ => new SolidColorBrush(Colors.Gray)
            };
        }

        private void ValidateForm()
        {
            bool isValid = true;
            string errorMessage = "";

            // Проверка названия компании
            if (string.IsNullOrWhiteSpace(CompanyNameTextBox.Text))
            {
                isValid = false;
                errorMessage = "Введите название компании";
            }
            // Проверка логина
            else if (string.IsNullOrWhiteSpace(UsernameTextBox.Text))
            {
                isValid = false;
                errorMessage = "Введите логин администратора";
            }
            // Проверка ФИО
            else if (string.IsNullOrWhiteSpace(FullNameTextBox.Text))
            {
                isValid = false;
                errorMessage = "Введите ФИО администратора";
            }
            // Проверка пароля
            else if (PasswordBox.Password.Length < 8)
            {
                isValid = false;
                errorMessage = "Пароль должен содержать минимум 8 символов";
            }
            // Проверка совпадения паролей
            else if (PasswordBox.Password != ConfirmPasswordBox.Password)
            {
                isValid = false;
                errorMessage = "Пароли не совпадают";
            }
            // Проверка согласия
            else if (AgreementCheckBox.IsChecked != true)
            {
                isValid = false;
                errorMessage = "Необходимо согласиться с условиями использования";
            }

            // Показать/скрыть ошибку
            if (!isValid && !string.IsNullOrEmpty(errorMessage))
            {
                ErrorMessage.Text = errorMessage;
                ErrorBorder.Visibility = Visibility.Visible;
            }
            else
            {
                ErrorBorder.Visibility = Visibility.Collapsed;
            }

            RegisterButton.IsEnabled = isValid;
        }

        private void OnRegisterClick(object sender, RoutedEventArgs e)
        {
            try
            {
                RegisterButton.IsEnabled = false;

                var result = MultiUserSecurityManager.CreateCompany(
                    companyName: CompanyNameTextBox.Text.Trim(),
                    adminUsername: UsernameTextBox.Text.Trim(),
                    adminFullName: FullNameTextBox.Text.Trim(),
                    adminPassword: PasswordBox.Password,
                    contactEmail: EmailTextBox.Text.Trim(),
                    contactPhone: PhoneTextBox.Text.Trim()
                );

                if (result.Success && result.Company != null)
                {
                    // Обновляем подсказку для пароля
                    using var context = new GlobalDbContext();
                    var user = context.Users.FirstOrDefault(u =>
                        u.CompanyId == result.Company.Id &&
                        u.Username == UsernameTextBox.Text.Trim().ToLower());

                    if (user != null && !string.IsNullOrWhiteSpace(PasswordHintTextBox.Text))
                    {
                        user.PasswordHint = PasswordHintTextBox.Text.Trim();
                        context.SaveChanges();
                    }

                    MessageBox.Show(
                        $"Компания '{result.Company.Name}' успешно создана!\n\n" +
                        $"Код компании: {result.Company.CompanyCode}\n\n" +
                        "Сохраните этот код - он понадобится для входа других сотрудников.",
                        "Успех",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    // Открываем окно входа
                    var loginWindow = new UserLoginWindow();
                    loginWindow.Show();
                    this.Close();
                }
                else
                {
                    ErrorMessage.Text = result.Message;
                    ErrorBorder.Visibility = Visibility.Visible;
                    RegisterButton.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage.Text = $"Ошибка: {ex.Message}";
                ErrorBorder.Visibility = Visibility.Visible;
                RegisterButton.IsEnabled = true;
            }
        }

        private void OnLoginClick(object sender, RoutedEventArgs e)
        {
            var loginWindow = new UserLoginWindow();
            loginWindow.Show();
            this.Close();
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }
    }
}
