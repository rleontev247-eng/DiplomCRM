using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MyFirstCRM
{
    public partial class AIAssistantWindow : Window
    {
        public AIAssistantWindow()
        {
            InitializeComponent();
            Loaded += AIAssistantWindow_Loaded;
        }

        private void AIAssistantWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadRecommendations();
        }

        private void LoadRecommendations()
        {
            RecommendationsPanel.Children.Clear();

            try
            {
                var recommendations = AIAssistant.GetDailyRecommendations();

                if (recommendations.Count == 0)
                {
                    var emptyCard = CreateEmptyStateCard();
                    RecommendationsPanel.Children.Add(emptyCard);
                    return;
                }

                foreach (var rec in recommendations)
                {
                    var card = CreateRecommendationCard(rec);
                    RecommendationsPanel.Children.Add(card);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки рекомендаций: {ex.Message}", "Ошибка");
            }
        }

        private Border CreateRecommendationCard(AIRecommendation rec)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(255, 30, 41, 59)),
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(0, 0, 0, 15),
                Padding = new Thickness(20)
            };

            var content = new StackPanel();

            // Заголовок с иконкой
            var headerPanel = new StackPanel { Orientation = Orientation.Horizontal };

            var iconBorder = new Border
            {
                Width = 40,
                Height = 40,
                CornerRadius = new CornerRadius(8),
                Background = GetCategoryColor(rec.Category),
                Margin = new Thickness(0, 0, 12, 0)
            };
            iconBorder.Child = new TextBlock
            {
                Text = rec.Icon,
                FontSize = 20,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            headerPanel.Children.Add(iconBorder);

            var titleStack = new StackPanel();
            titleStack.Children.Add(new TextBlock
            {
                Text = rec.Title,
                Foreground = Brushes.White,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                TextWrapping = TextWrapping.Wrap
            });
            titleStack.Children.Add(new TextBlock
            {
                Text = rec.Category,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 148, 163, 184)),
                FontSize = 11,
                Margin = new Thickness(0, 4, 0, 0)
            });
            headerPanel.Children.Add(titleStack);

            content.Children.Add(headerPanel);

            // Описание
            content.Children.Add(new TextBlock
            {
                Text = rec.Description,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 203, 213, 225)),
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 12, 0, 0)
            });

            // Уверенность
            var confidencePanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 12, 0, 0)
            };
            confidencePanel.Children.Add(new TextBlock
            {
                Text = $"Уверенность: {rec.Confidence}%",
                Foreground = new SolidColorBrush(Color.FromArgb(255, 148, 163, 184)),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            });
            confidencePanel.Children.Add(new ProgressBar
            {
                Value = rec.Confidence,
                Maximum = 100,
                Width = 150,
                Height = 6,
                Margin = new Thickness(10, 0, 0, 0),
                Foreground = GetConfidenceBrush(rec.Confidence)
            });
            content.Children.Add(confidencePanel);

            // Потенциальная выручка
            if (rec.PotentialRevenue > 0)
            {
                content.Children.Add(new TextBlock
                {
                    Text = $"💰 Потенциал: {rec.PotentialRevenue:N0} ₽",
                    Foreground = new SolidColorBrush(Color.FromArgb(255, 16, 185, 129)),
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 8, 0, 0)
                });
            }

            // Действие
            var actionBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(255, 59, 130, 246)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 12, 0, 0)
            };
            actionBorder.Child = new TextBlock
            {
                Text = $"✅ {rec.ActionRequired}",
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap
            };
            content.Children.Add(actionBorder);

            card.Child = content;
            return card;
        }

        private Border CreateEmptyStateCard()
        {
            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(255, 30, 41, 59)),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(40),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var content = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };

            content.Children.Add(new TextBlock
            {
                Text = "✅",
                FontSize = 48,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 15)
            });

            content.Children.Add(new TextBlock
            {
                Text = "Отличная работа!",
                Foreground = Brushes.White,
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center
            });

            content.Children.Add(new TextBlock
            {
                Text = "Все сделки под контролем. Продолжайте в том же духе!",
                Foreground = new SolidColorBrush(Color.FromArgb(255, 148, 163, 184)),
                FontSize = 13,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 8, 0, 0)
            });

            card.Child = content;
            return card;
        }

        private Brush GetCategoryColor(string category)
        {
            return category switch
            {
                "Реактивация" => new SolidColorBrush(Color.FromArgb(255, 245, 158, 11)),
                "Оптимизация" => new SolidColorBrush(Color.FromArgb(255, 139, 92, 246)),
                "Возможность" => new SolidColorBrush(Color.FromArgb(255, 16, 185, 129)),
                "Риск" => new SolidColorBrush(Color.FromArgb(255, 239, 68, 68)),
                _ => new SolidColorBrush(Color.FromArgb(255, 59, 130, 246))
            };
        }

        private Brush GetConfidenceBrush(int confidence)
        {
            if (confidence >= 80)
                return new SolidColorBrush(Color.FromArgb(255, 16, 185, 129));
            if (confidence >= 60)
                return new SolidColorBrush(Color.FromArgb(255, 59, 130, 246));
            return new SolidColorBrush(Color.FromArgb(255, 245, 158, 11));
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadRecommendations();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}