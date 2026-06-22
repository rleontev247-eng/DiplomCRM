using System.Windows;

namespace MyFirstCRM
{
    public partial class ExportProgressWindow : Window
    {
        public ExportProgressWindow()
        {
            InitializeComponent();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}