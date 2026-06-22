using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;

namespace MyFirstCRM
{
    public partial class AddExpenseWindow : Window
    {
        private bool _isIncome = false;

        // Конструктор по умолчанию (для расхода)
        public AddExpenseWindow()
        {
            InitializeComponent();
        }

        // Специальный конструктор, чтобы переключить окно в режим "Доход"
        public void SetAsIncome()
        {
            _isIncome = true;
            WindowHeader.Text = "Новый доход";
            // Меняем цвет кнопки на зеленый (SuccessColor)
            SaveButton.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#10B981"));
            CategoryInput.SelectedIndex = 4; // Выбираем "Продажи" по умолчанию
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TitleInput.Text) || !decimal.TryParse(AmountInput.Text, out decimal amount))
            {
                MessageBox.Show("Пожалуйста, введите корректное название и сумму.");
                return;
            }

            if (_isIncome)
            {
                MessageBox.Show("Доходы формируются из успешных сделок.\n\nДобавьте или закройте сделку как 'Успешная' в разделе 'Сделки'.",
                    "Доходы", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                using (var db = MultiUserSecurityManager.CreateCompanyContext())
                {
                    var category =
                        (CategoryInput.SelectedItem as ComboBoxItem)?.Content?.ToString()
                        ?? CategoryInput.Text
                        ?? "Прочее";

                    var expense = new Expense
                    {
                        Title = TitleInput.Text,
                        Category = category,
                        Amount = amount,
                        Date = DateTime.Now
                    };

                    db.Expenses.Add(expense);
                    db.SaveChanges();
                }
                this.DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при сохранении: " + ex.Message);
            }
        }

        

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        private void Border_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
                this.DragMove();
        }
    }
}