using System.Windows;

namespace MyFirstCRM
{
    public partial class ImportProgressWindow : Window
    {
        public ImportProgressWindow(string title = "Импорт")
        {
            InitializeComponent();
            StatusText.Text = title;
        }
    }
}