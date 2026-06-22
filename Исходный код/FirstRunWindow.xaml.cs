using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MyFirstCRM
{
    public partial class FirstRunWindow : Window
    {
        public FirstRunWindow()
        {
            InitializeComponent();
            Loaded += FirstRunWindow_Loaded;
        }

        private void FirstRunWindow_Loaded(object sender, RoutedEventArgs e)
        {
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.5)));
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            UpdatePasswordStrength();
            ValidateForm();
        }

        private void ConfirmPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            ValidateForm();
        }

        private void UpdatePasswordStrength()
        {
            var password = PasswordBox.Password;
            var strength = SecurityManager.CheckPasswordStrength(password);

            // Сброс всех индикаторов
            Strength1.Style = (Style)FindResource("StrengthIndicatorWeak");
            Strength2.Style = (Style)FindResource("StrengthIndicatorWeak");
            Strength3.Style = (Style)FindResource("StrengthIndicatorWeak");
            Strength4.Style = (Style)FindResource("StrengthIndicatorWeak");

            // Обновление текста и цвета
            switch (strength)
            {
                case SecurityManager.PasswordStrength.None:
                    StrengthText.Text = "Сложность: не задан";
                    StrengthText.Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139));
                    break;

                case SecurityManager.PasswordStrength.Weak:
                    StrengthText.Text = "Сложность: слабый";
                    StrengthText.Foreground = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                    Strength1.Style = (Style)FindResource("StrengthIndicatorWeak");
                    break;

                case SecurityManager.PasswordStrength.Medium:
                    StrengthText.Text = "Сложность: средний";
                    StrengthText.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                    Strength1.Style = (Style)FindResource("StrengthIndicatorMedium");
                    Strength2.Style = (Style)FindResource("StrengthIndicatorMedium");
                    break;

                case SecurityManager.PasswordStrength.Strong:
                    StrengthText.Text = "Сложность: сильный";
                    StrengthText.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                    Strength1.Style = (Style)FindResource("StrengthIndicatorStrong");
                    Strength2.Style = (Style)FindResource("StrengthIndicatorStrong");
                    Strength3.Style = (Style)FindResource("StrengthIndicatorStrong");
                    break;

                case SecurityManager.PasswordStrength.VeryStrong:
                    StrengthText.Text = "Сложность: очень сильный";
                    StrengthText.Foreground = new SolidColorBrush(Color.FromRgb(59, 130, 246));
                    Strength1.Style = (Style)FindResource("StrengthIndicatorVeryStrong");
                    Strength2.Style = (Style)FindResource("StrengthIndicatorVeryStrong");
                    Strength3.Style = (Style)FindResource("StrengthIndicatorVeryStrong");
                    Strength4.Style = (Style)FindResource("StrengthIndicatorVeryStrong");
                    break;
            }

            // Обновление рекомендаций
            UpdateRecommendations(password);
        }

        private void UpdateRecommendations(string password)
        {
            // Минимум 8 символов
            LengthRecommendation.Foreground = password.Length >= 8 ?
                new SolidColorBrush(Color.FromRgb(16, 185, 129)) :
                new SolidColorBrush(Color.FromRgb(203, 213, 225));

            // Заглавные и строчные
            CaseRecommendation.Foreground = password.Any(char.IsUpper) && password.Any(char.IsLower) ?
                new SolidColorBrush(Color.FromRgb(16, 185, 129)) :
                new SolidColorBrush(Color.FromRgb(203, 213, 225));

            // Цифры и специальные символы
            SpecialRecommendation.Foreground = password.Any(char.IsDigit) && password.Any(c => !char.IsLetterOrDigit(c)) ?
                new SolidColorBrush(Color.FromRgb(16, 185, 129)) :
                new SolidColorBrush(Color.FromRgb(203, 213, 225));
        }

        private void ValidateForm()
        {
            var password = PasswordBox.Password;
            var confirmPassword = ConfirmPasswordBox.Password;

            bool isValid = true;
            string errorMessage = "";

            // Проверка минимальной длины
            if (password.Length < 8)
            {
                isValid = false;
                errorMessage = "Пароль должен содержать минимум 8 символов";
            }
            // Проверка совпадения паролей
            else if (password != confirmPassword)
            {
                isValid = false;
                errorMessage = "Пароли не совпадают";
            }
            // Проверка сложности
            else if (SecurityManager.CheckPasswordStrength(password) == SecurityManager.PasswordStrength.Weak)
            {
                isValid = false;
                errorMessage = "Пароль слишком слабый. Добавьте заглавные буквы, цифры или специальные символы";
            }
            // Проверка согласия
            else if (AgreementCheckBox.IsChecked != true)
            {
                isValid = false;
                errorMessage = "Необходимо подтвердить понимание ответственности";
            }

            // Показать/скрыть ошибку
            if (!isValid && !string.IsNullOrEmpty(errorMessage))
            {
                ErrorMessage.Text = errorMessage;
                ErrorBorder.Visibility = Visibility.Visible;

                // Анимация тряски
                var shake = new Storyboard();
                var translateAnimation = new DoubleAnimationUsingKeyFrames();
                translateAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                translateAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(-10, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.1))));
                translateAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(10, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.2))));
                translateAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(-10, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.3))));
                translateAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(10, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.4))));
                translateAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.5))));

                Storyboard.SetTargetProperty(translateAnimation, new System.Windows.PropertyPath("RenderTransform.Children[0].X"));
                shake.Children.Add(translateAnimation);

                var transformGroup = new TransformGroup();
                transformGroup.Children.Add(new TranslateTransform());
                ErrorBorder.RenderTransform = transformGroup;
                shake.Begin(ErrorBorder);
            }
            else
            {
                ErrorBorder.Visibility = Visibility.Collapsed;
            }

            ContinueButton.IsEnabled = isValid;
        }

        private void TogglePassword_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("В реальной реализации здесь будет переключение видимости пароля",
                "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void AgreementCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            ValidateForm();
        }

        private void AgreementCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            ValidateForm();
        }

        private void EmergencyDelete_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "🚨 ВНИМАНИЕ! ЭКСТРЕННОЕ УДАЛЕНИЕ\n\n" +
                "Это действие:\n" +
                "• Удалит ВСЕ данные безвозвратно\n" +
                "• Удалит всех клиентов и сделки\n" +
                "• Удалит все настройки\n" +
                "• Программа закроется\n\n" +
                "Вы уверены? Это действие нельзя отменить!",
                "КРИТИЧЕСКОЕ ПОДТВЕРЖДЕНИЕ",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                SecurityManager.EmergencyReset();
            }
        }

        private void ContinueButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Простое сохранение
                SecurityManager.CurrentSecurity.IsFirstRun = false;
                SecurityManager.CurrentSecurity.MasterPasswordHash = SecurityManager.HashPassword(PasswordBox.Password);
                SecurityManager.CurrentSecurity.Hint = HintTextBox.Text;
                SecurityManager.CurrentSecurity.SetupDate = DateTime.Now;

                // Простой ключ
                var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
                rng.GetBytes(SecurityManager.CurrentSecurity.EncryptionKey);

                SecurityManager.SaveSecurityData();

                // Простой запуск
                this.Hide();
                App.SafeLaunchMain();
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка");
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            this.DragMove();
        }
    }
}