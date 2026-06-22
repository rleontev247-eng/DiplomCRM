using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace MyFirstCRM
{
    public partial class DeploymentSettingsWindow : Window
    {
        private DeploymentManager _deploymentManager;
        private DeploymentConfig _config;

        public DeploymentSettingsWindow()
        {
            InitializeComponent();
            _deploymentManager = DeploymentManager.Instance;
            _config = _deploymentManager.Config;
            
            LoadDeploymentModes();
            LoadSettings();
            SetupEventHandlers();
            UpdateStatusDisplay();
        }

        /// <summary>
        /// Модель для отображения режима развертывания
        /// </summary>
        public class DeploymentModeItem
        {
            public string Icon { get; set; } = "";
            public string Title { get; set; } = "";
            public string Description { get; set; } = "";
            public DeploymentMode Mode { get; set; }
        }

        /// <summary>
        /// Загрузка режимов развертывания
        /// </summary>
        private void LoadDeploymentModes()
        {
            var modes = new List<DeploymentModeItem>
            {
                new DeploymentModeItem
                {
                    Icon = "📱",
                    Title = "Локальный режим",
                    Description = "Все данные хранятся только на этом компьютере. Идеально для небольших компаний и одиночной работы.",
                    Mode = DeploymentMode.Local
                },
                new DeploymentModeItem
                {
                    Icon = "☁️",
                    Title = "Облачный режим",
                    Description = "Подключение к центральному облачному серверу. Доступ из любой точки мира.",
                    Mode = DeploymentMode.Cloud
                },
                new DeploymentModeItem
                {
                    Icon = "🔄",
                    Title = "Гибридный режим",
                    Description = "Локальная база данных с периодической синхронизацией в облако. Работает без интернета.",
                    Mode = DeploymentMode.Hybrid
                },
                new DeploymentModeItem
                {
                    Icon = "🖥️",
                    Title = "Режим сервера",
                    Description = "Этот компьютер работает как сервер для других устройств в локальной сети.",
                    Mode = DeploymentMode.Server
                },
                new DeploymentModeItem
                {
                    Icon = "📡",
                    Title = "Клиент сервера",
                    Description = "Подключение к существующему серверу в локальной сети.",
                    Mode = DeploymentMode.ServerClient
                }
            };

            DeploymentModeItemsControl.ItemsSource = modes;
            
            // Выбираем текущий режим
            var currentMode = modes.FirstOrDefault(m => m.Mode == _config.Mode);
            if (currentMode != null)
            {
                // Найти RadioButton для текущего режима
                var radioButtons = FindVisualChildren<RadioButton>(DeploymentModeItemsControl);
                var currentRadioButton = radioButtons.FirstOrDefault(rb => rb.Tag?.ToString() == currentMode.Mode.ToString());
                if (currentRadioButton != null)
                {
                    currentRadioButton.IsChecked = true;
                }
            }
        }

        /// <summary>
        /// Поиск всех дочерних элементов определенного типа
        /// </summary>
        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T)
                    {
                        yield return (T)child;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }

        /// <summary>
        /// Загрузка текущих настроек
        /// </summary>
        private void LoadSettings()
        {
            // Загрузка настроек облачного режима
            CloudServerUrlTextBox.Text = _config.CloudServerUrl ?? "";
            CloudApiKeyPasswordBox.Password = _config.CloudApiKey ?? "";

            // Загрузка настроек сервера
            ServerIpAddressTextBox.Text = _config.ServerIpAddress ?? "";
            ServerPortTextBox.Text = _config.ServerPort.ToString();

            // Загрузка настроек синхронизации
            AutoSyncCheckBox.IsChecked = _config.AutoSyncEnabled;
            UseCompressionCheckBox.IsChecked = _config.UseCompression;
            UseEncryptionCheckBox.IsChecked = _config.UseEncryption;

            // Установка интервала синхронизации
            var intervalItem = SyncIntervalComboBox.Items.Cast<ComboBoxItem>()
                .FirstOrDefault(item => item.Tag?.ToString() == _config.SyncIntervalMinutes.ToString());
            if (intervalItem != null)
            {
                SyncIntervalComboBox.SelectedItem = intervalItem;
            }

            // Обновление отображения панелей
            UpdatePanelsVisibility(_config.Mode);
        }

        /// <summary>
        /// Настройка обработчиков событий
        /// </summary>
        private void SetupEventHandlers()
        {
            // Обработчики изменений текстовых полей для обновления информации
            ServerIpAddressTextBox.TextChanged += (s, e) => UpdateServerInfo();
            ServerPortTextBox.TextChanged += (s, e) => UpdateServerInfo();
        }

        /// <summary>
        /// Обновление информации о сервере
        /// </summary>
        private void UpdateServerInfo()
        {
            var ip = ServerIpAddressTextBox.Text.Trim();
            var port = ServerPortTextBox.Text.Trim();
            
            if (!string.IsNullOrEmpty(ip) && !string.IsNullOrEmpty(port))
            {
                ServerInfoTextBlock.Text = $"Клиенты смогут подключиться по адресу: http://{ip}:{port}";
            }
            else
            {
                ServerInfoTextBlock.Text = "Введите IP адрес и порт сервера";
            }
        }

        /// <summary>
        /// Обработка выбора режима работы
        /// </summary>
        private void DeploymentModeRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton radioButton && radioButton.Tag != null)
            {
                var mode = Enum.Parse<DeploymentMode>(radioButton.Tag.ToString()!);
                UpdatePanelsVisibility(mode);
            }
        }

        /// <summary>
        /// Обновление видимости панелей настроек
        /// </summary>
        private void UpdatePanelsVisibility(DeploymentMode mode)
        {
            // Скрываем все панели
            CloudSettingsPanel.Visibility = Visibility.Collapsed;
            ServerSettingsPanel.Visibility = Visibility.Collapsed;
            SyncSettingsPanel.Visibility = Visibility.Collapsed;

            // Показываем нужные панели
            switch (mode)
            {
                case DeploymentMode.Cloud:
                    CloudSettingsPanel.Visibility = Visibility.Visible;
                    SyncSettingsPanel.Visibility = Visibility.Visible;
                    break;
                case DeploymentMode.Hybrid:
                    CloudSettingsPanel.Visibility = Visibility.Visible;
                    SyncSettingsPanel.Visibility = Visibility.Visible;
                    break;
                case DeploymentMode.Server:
                    ServerSettingsPanel.Visibility = Visibility.Visible;
                    SyncSettingsPanel.Visibility = Visibility.Visible;
                    break;
                case DeploymentMode.ServerClient:
                    ServerSettingsPanel.Visibility = Visibility.Visible;
                    SyncSettingsPanel.Visibility = Visibility.Visible;
                    break;
                case DeploymentMode.Local:
                default:
                    // Для локального режима дополнительные панели не нужны
                    break;
            }

            UpdateStatusDisplay();
        }

        /// <summary>
        /// Обновление отображения статуса
        /// </summary>
        private void UpdateStatusDisplay()
        {
            var mode = _config.Mode;
            CurrentModeTextBlock.Text = mode switch
            {
                DeploymentMode.Local => "Локальный",
                DeploymentMode.Cloud => "Облачный",
                DeploymentMode.Hybrid => "Гибридный",
                DeploymentMode.Server => "Сервер",
                DeploymentMode.ServerClient => "Клиент сервера",
                _ => "Неизвестно"
            };

            // Обновление информации о последней синхронизации
            LastSyncTextBlock.Text = "Не выполнялась";

            // Обновление статуса
            StatusTextBlock.Text = "✅ Активно";
            StatusTextBlock.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green);
        }

        /// <summary>
        /// Проверка подключения к облачному серверу
        /// </summary>
        private async void TestCloudConnectionButton_Click(object sender, RoutedEventArgs e)
        {
            var url = CloudServerUrlTextBox.Text.Trim();
            var apiKey = CloudApiKeyPasswordBox.Password.Trim();

            if (string.IsNullOrEmpty(url))
            {
                MessageBox.Show("Введите URL облачного сервера", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Валидация API ключа Supabase
                if (!string.IsNullOrEmpty(apiKey) && 
                    !apiKey.StartsWith("sb_publishable_") && 
                    !apiKey.StartsWith("sb_secret_") && 
                    !apiKey.StartsWith("eyJ"))
                {
                    MessageBox.Show("❌ Неверный формат API ключа Supabase.\n\n" +
                                  "Ключ должен начинаться с:\n" +
                                  "• 'sb_publishable_' для publishable ключа\n" +
                                  "• 'sb_secret_' для secret ключа\n" +
                                  "• 'eyJ' для service_role (secret) ключа\n\n" +
                                  "Для доступа к схеме данных нужен service_role ключ.\n" +
                                  "Проверьте ключ в настройках вашего проекта Supabase.", 
                                  "Ошибка ключа", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Временно сохраняем настройки для проверки
                var originalConfig = _config;
                _config.CloudServerUrl = url;
                _config.CloudApiKey = apiKey;
                _config.Mode = DeploymentMode.Cloud;

                // Реальная проверка подключения
                bool isConnected = await _deploymentManager.TestServerConnection();

                // Восстанавливаем оригинальные настройки
                _config = originalConfig;

                if (isConnected)
                {
                    MessageBox.Show("✅ Подключение к облачному серверу успешно!\n\n" +
                                  "URL: " + url + "\n" +
                                  "Статус: Активно", 
                                  "Проверка подключения", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("❌ Не удалось подключиться к облачному серверу.\n\n" +
                                  "URL: " + url + "\n" +
                                  "Проверьте адрес сервера и API ключ.", 
                                  "Ошибка подключения", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("❌ Ошибка подключения к облачному серверу:\n\n" + ex.Message, 
                              "Ошибка подключения", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Проверка подключения к локальному серверу
        /// </summary>
        private async void TestServerConnectionButton_Click(object sender, RoutedEventArgs e)
        {
            var ipAddress = ServerIpAddressTextBox.Text.Trim();
            var portText = ServerPortTextBox.Text.Trim();

            if (string.IsNullOrEmpty(ipAddress))
            {
                MessageBox.Show("Введите IP адрес сервера", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(portText, out int port))
            {
                MessageBox.Show("Введите корректный номер порта", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Временно сохраняем настройки для проверки
                var originalConfig = _config;
                _config.ServerIpAddress = ipAddress;
                _config.ServerPort = port;
                _config.Mode = DeploymentMode.ServerClient;

                // Реальная проверка подключения
                bool isConnected = await _deploymentManager.TestServerConnection();

                // Восстанавливаем оригинальные настройки
                _config = originalConfig;

                if (isConnected)
                {
                    MessageBox.Show("✅ Подключение к серверу успешно!\n\n" +
                                  "Адрес: " + ipAddress + ":" + port + "\n" +
                                  "Статус: Активно", 
                                  "Проверка подключения", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("❌ Не удалось подключиться к серверу.\n\n" +
                                  "Адрес: " + ipAddress + ":" + port + "\n" +
                                  "Проверьте IP адрес и порт сервера.", 
                                  "Ошибка подключения", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("❌ Ошибка подключения к серверу:\n\n" + ex.Message, 
                              "Ошибка подключения", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Сохранение настроек
        /// </summary>
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Получаем выбранный режим
                var selectedRadioButton = FindVisualChildren<RadioButton>(DeploymentModeItemsControl)
                    .FirstOrDefault(rb => rb.IsChecked == true);
                
                if (selectedRadioButton?.Tag != null)
                {
                    _config.Mode = Enum.Parse<DeploymentMode>(selectedRadioButton.Tag.ToString()!);
                }

                // Сохраняем настройки облачного режима
                _config.CloudServerUrl = CloudServerUrlTextBox.Text.Trim();
                _config.CloudApiKey = CloudApiKeyPasswordBox.Password.Trim();

                // Сохраняем настройки сервера
                _config.ServerIpAddress = ServerIpAddressTextBox.Text.Trim();
                if (int.TryParse(ServerPortTextBox.Text.Trim(), out var port))
                {
                    _config.ServerPort = port;
                }

                // Сохраняем настройки синхронизации
                _config.AutoSyncEnabled = AutoSyncCheckBox.IsChecked == true;
                _config.UseCompression = UseCompressionCheckBox.IsChecked == true;
                _config.UseEncryption = UseEncryptionCheckBox.IsChecked == true;

                // Получаем интервал синхронизации
                if (SyncIntervalComboBox.SelectedItem is ComboBoxItem selectedInterval && selectedInterval.Tag != null)
                {
                    if (int.TryParse(selectedInterval.Tag.ToString(), out var interval))
                    {
                        _config.SyncIntervalMinutes = interval;
                    }
                }

                // Сохраняем конфигурацию
                _deploymentManager.SaveConfiguration(_config);
                
                MessageBox.Show("✅ Настройки успешно сохранены!\n\n" +
                              "Режим: " + _config.Mode + "\n" +
                              "Синхронизация: " + (_config.AutoSyncEnabled ? "Включена" : "Выключена"), 
                              "Сохранение настроек", MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("❌ Ошибка при сохранении настроек:\n\n" + ex.Message, 
                              "Ошибка сохранения", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Отмена настроек
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
