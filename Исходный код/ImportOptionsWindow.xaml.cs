using System.Windows;

namespace MyFirstCRM
{
    public partial class ImportOptionsWindow : Window
    {
        public string FilePath { get; set; }
        public bool UpdateExisting { get; private set; } = true;
        public bool CreateClients { get; private set; } = true;
        public bool SkipErrors { get; private set; } = true;

        public ImportOptionsWindow(string filePath)
        {
            InitializeComponent();
            FilePath = filePath;
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateExisting = UpdateExistingCheckBox.IsChecked ?? true;
            CreateClients = CreateClientsCheckBox.IsChecked ?? true;
            SkipErrors = SkipErrorsCheckBox.IsChecked ?? true;

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}