using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace MyFirstCRM
{
    public partial class PasswordConfirmationWindow : Window
    {
        public PasswordConfirmationWindow()
        {
            InitializeComponent();
            Loaded += PasswordConfirmationWindow_Loaded;
        }

        private void PasswordConfirmationWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Анимация появления
            this.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.8)));

            // Анимация успеха
            var successAnimation = new Storyboard();

            var scaleAnimation = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.6));
            scaleAnimation.EasingFunction = new ElasticEase { Oscillations = 1, Springiness = 3 };
            Storyboard.SetTargetProperty(scaleAnimation,
                new System.Windows.PropertyPath("RenderTransform.ScaleX"));
            successAnimation.Children.Add(scaleAnimation);

            var scaleAnimationY = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.6));
            scaleAnimationY.EasingFunction = new ElasticEase { Oscillations = 1, Springiness = 3 };
            Storyboard.SetTargetProperty(scaleAnimationY,
                new System.Windows.PropertyPath("RenderTransform.ScaleY"));
            successAnimation.Children.Add(scaleAnimationY);
        }

        private void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Закрываем текущее окно
                this.Close();

                // Запускаем главный интерфейс
                if (Application.Current is App app)
                {
                    app.LaunchMainInterface();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка запуска: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            this.DragMove();
        }
    }
}