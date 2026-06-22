using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Data;  
using System.Windows.Media; 

namespace MyFirstCRM
{
    public partial class NotificationsWindow : Window
    {
        private List<Notification> _notifications = new List<Notification>();

        public NotificationsWindow()
        {
            InitializeComponent();
            Loaded += NotificationsWindow_Loaded;
            LoadNotifications();
        }

        private void NotificationsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Анимация появления
            this.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.3)));
        }

        private void LoadNotifications()
        {
            try
            {
                
                Console.WriteLine("=== Загрузка уведомлений ===");

                _notifications = NotificationManager.LoadNotifications();

               
                Console.WriteLine($"Загружено уведомлений: {_notifications.Count}");
                foreach (var notification in _notifications)
                {
                    Console.WriteLine($"Уведомление: {notification.Title}, ID: {notification.Id}");
                }

                UpdateNotificationsDisplay();
                UpdateUnreadCount();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка загрузки уведомлений: {ex.Message}");
            }
        }

        private void UpdateNotificationsDisplay()
        {
            NotificationsPanel.Children.Clear();

            if (!_notifications.Any())
            {
                EmptyState.Visibility = Visibility.Visible;
                return;
            }

            EmptyState.Visibility = Visibility.Collapsed;

            int index = 0;
            foreach (var notification in _notifications)
            {
                var notificationCard = CreateNotificationCard(notification, index);
                NotificationsPanel.Children.Add(notificationCard);
                index++;
            }
        }

        private Border CreateNotificationCard(Notification notification, int index)
        {
            // ОТЛАДКА: показываем, что карточка создается
            Console.WriteLine($"Создаем карточку уведомления: {notification.Title}, ID: {notification.Id}");

            var card = new Border
            {
                // ИЗМЕНЕНО: светлый фон вместо темного
                Background = new SolidColorBrush(Color.FromArgb(255, 248, 250, 252)), // Светло-серый
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(20, index == 0 ? 20 : 10, 20, 10),
                Padding = new Thickness(16),
                Tag = notification.Id,
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 226, 232, 240)), // Светлая граница
                BorderThickness = new Thickness(1)
            };

            // Сетка для содержимого
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Иконка
            var iconBorder = new Border
            {
                Width = 40,
                Height = 40,
                CornerRadius = new CornerRadius(10),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(notification.Color)),
                Margin = new Thickness(0, 0, 12, 0)
            };

            var iconText = new TextBlock
            {
                Text = notification.Icon,
                FontSize = 18,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brushes.White
            };

            iconBorder.Child = iconText;

            // Контент
            var contentStack = new StackPanel();

            var titleStack = new StackPanel { Orientation = Orientation.Horizontal };

            // ИЗМЕНЕНО: темный текст для светлого фона
            var titleText = new TextBlock
            {
                Text = notification.Title,
                Foreground = notification.IsRead ?
                    new SolidColorBrush(Color.FromArgb(255, 30, 41, 59)) : // Темный цвет для прочитанных
                    new SolidColorBrush(Color.FromArgb(255, 59, 130, 246)), // Синий цвет для непрочитанных
                FontSize = 14,
                FontWeight = notification.IsRead ? FontWeights.Normal : FontWeights.SemiBold
            };

            titleStack.Children.Add(titleText);

            if (!notification.IsRead)
            {
                var unreadBadge = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(255, 59, 130, 246)),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 2, 6, 2),
                    Margin = new Thickness(8, 0, 0, 0),
                    Child = new TextBlock
                    {
                        Text = "новое",
                        Foreground = Brushes.White,
                        FontSize = 9,
                        FontWeight = FontWeights.Bold
                    }
                };
                titleStack.Children.Add(unreadBadge);
            }

            contentStack.Children.Add(titleStack);

            // ИЗМЕНЕНО: темный текст сообщения
            contentStack.Children.Add(new TextBlock
            {
                Text = notification.Message,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 71, 85, 105)), // Темно-серый
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0)
            });

            var timeText = new TextBlock
            {
                Text = GetTimeAgo(notification.CreatedAt),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 100, 116, 139)), // Серый
                FontSize = 10,
                Margin = new Thickness(0, 8, 0, 0)
            };
            contentStack.Children.Add(timeText);

            // Кнопки действий
            var actionStack = new StackPanel { Orientation = Orientation.Horizontal };

            if (!notification.IsRead)
            {
                var markReadButton = new Button
                {
                    Content = "👁",
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    // ИЗМЕНЕНО: темный цвет для светлого фона
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 71, 85, 105)),
                    Padding = new Thickness(8),
                    FontSize = 12,
                    ToolTip = "Пометить как прочитанное",
                    Tag = notification.Id,
                    Cursor = Cursors.Hand
                };
                markReadButton.Click += MarkReadButton_Click;
                actionStack.Children.Add(markReadButton);
            }

            var deleteButton = new Button
            {
                Content = "🗑",
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = new SolidColorBrush(Color.FromArgb(255, 239, 68, 68)), // Красный
                Padding = new Thickness(8),
                FontSize = 12,
                ToolTip = "Удалить уведомление",
                Tag = notification.Id,
                Cursor = Cursors.Hand
            };
            deleteButton.Click += DeleteButton_Click;
            actionStack.Children.Add(deleteButton);

            // Размещение элементов в сетке
            Grid.SetColumn(iconBorder, 0);
            Grid.SetColumn(contentStack, 1);
            Grid.SetColumn(actionStack, 2);

            grid.Children.Add(iconBorder);
            grid.Children.Add(contentStack);
            grid.Children.Add(actionStack);

            card.Child = grid;

            // Клик по карточке - ИСПРАВЛЕННЫЙ код (убрали проверку на DealId)
            card.MouseLeftButtonUp += (s, e) =>
            {
                string details = $"📋 Детали уведомления:\n\n" +
                                $"Заголовок: {notification.Title}\n" +
                                $"Сообщение: {notification.Message}\n\n" +
                                $"Тип: {notification.Type}\n" +
                                $"Дата: {notification.CreatedAt:dd.MM.yyyy HH:mm}\n";

                if (notification.DueDate.HasValue)
                    details += $"Срок: {notification.DueDate.Value:dd.MM.yyyy}\n";

                details += $"Статус: {(notification.IsRead ? "✅ Прочитано" : "👁 Не прочитано")}";

                MessageBox.Show(details, "Уведомление",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            };

            // УБИРАЕМ ВСЕ СЛОЖНЫЕ АНИМАЦИИ
            // Просто задаем непрозрачность 1 (полностью видимый)
            card.Opacity = 1;

            return card;
        }

        private string GetTimeAgo(DateTime dateTime)
        {
            var timeSpan = DateTime.Now - dateTime;

            if (timeSpan.TotalMinutes < 1)
                return "только что";
            if (timeSpan.TotalHours < 1)
                return $"{(int)timeSpan.TotalMinutes} мин назад";
            if (timeSpan.TotalDays < 1)
                return $"{(int)timeSpan.TotalHours} ч назад";
            if (timeSpan.TotalDays < 7)
                return $"{(int)timeSpan.TotalDays} дн назад";

            return dateTime.ToString("dd.MM.yyyy");
        }

        private void UpdateUnreadCount()
        {
            int unreadCount = _notifications.Count(n => !n.IsRead);
            UnreadCountText.Text = $"{unreadCount} непрочитанных";
        }

        private void MarkReadButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int notificationId)
            {
                NotificationManager.MarkAsRead(notificationId);
                LoadNotifications();
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is int notificationId)
            {
                NotificationManager.DeleteNotification(notificationId);
                LoadNotifications();
            }
        }

        private void MarkAllReadButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Пометить все уведомления как прочитанные?",
                "Подтверждение",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                NotificationManager.MarkAllAsRead();
                LoadNotifications();
            }
        }

        private void CheckDealsButton_Click(object sender, RoutedEventArgs e)
        {
            NotificationManager.CheckDealReminders();
            LoadNotifications();

            // Анимация кнопки
            var rotation = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(0.5));
         //   CheckDealsButton.RenderTransform = new RotateTransform();
         //   CheckDealsButton.RenderTransform.BeginAnimation(RotateTransform.AngleProperty, rotation);
        }

        private void CreateNotificationButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CreateNotificationDialog();
            if (dialog.ShowDialog() == true)
            {
                LoadNotifications();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            this.DragMove();
        }
    }

}