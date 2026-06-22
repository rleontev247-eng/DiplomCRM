using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace MyFirstCRM
{
    /// <summary>
    /// Менеджер для работы с Supabase
    /// </summary>
    public class SupabaseManager
    {
        private static SupabaseManager? _instance;
        private static readonly object _lock = new object();
        private readonly HttpClient _httpClient;
        private string _supabaseUrl;
        private string _supabaseKey;

        public static SupabaseManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                            _instance = new SupabaseManager();
                    }
                }
                return _instance;
            }
        }

        private SupabaseManager()
        {
            _httpClient = new HttpClient();
        }

        public void Initialize(string url, string key)
        {
            // Проверяем и исправляем формат URL
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
            {
                url = "https://" + url;
            }
            
            _supabaseUrl = url.TrimEnd('/');
            _supabaseKey = key;
            
            System.Diagnostics.Debug.WriteLine($"Initializing Supabase with URL: {_supabaseUrl}");
            System.Diagnostics.Debug.WriteLine($"API key length: {_supabaseKey?.Length ?? 0}");
            
            // Устанавливаем заголовки для Supabase
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("apikey", _supabaseKey);
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_supabaseKey}");
            _httpClient.DefaultRequestHeaders.Add("Prefer", "return=representation");
        }

        /// <summary>
        /// Получение детального сообщения об ошибке авторизации
        /// </summary>
        private string GetUnauthorizedErrorMessage(string errorContent)
        {
            if (errorContent.Contains("Access to schema is forbidden") && 
                errorContent.Contains("only allowed using a secret API key"))
            {
                return "❌ Ошибка доступа к схеме данных!\n\n" +
                       "Для доступа к схеме через REST API требуется SECRET ключ, а не publishable.\n\n" +
                       "Решение:\n" +
                       "1. Зайдите в Settings → API в вашем проекте Supabase\n" +
                       "2. Скопируйте 'service_role' (secret) ключ\n" +
                       "3. Используйте этот ключ вместо publishable ключа\n\n" +
                       "Внимание: Secret ключ должен использоваться только в доверенных приложениях!";
            }
            
            if (errorContent.Contains("Invalid API key"))
            {
                return "❌ Неверный API ключ.\n\nПроверьте правильность ключа в настройках проекта Supabase.";
            }
            
            if (errorContent.Contains("JWT"))
            {
                return "❌ Ошибка JWT токена.\n\nПроверьте, что API ключ действительный и не истек.";
            }
            
            return "❌ Ошибка авторизации.\n\nПроверьте API ключ и настройки проекта Supabase.";
        }

        /// <summary>
        /// Проверка валидности API ключа Supabase
        /// </summary>
        public bool ValidateApiKey(string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey))
                return false;
                
            // Supabase publishable ключи начинаются с 'sb_publishable_'
            // service_role (secret) ключи начинаются с 'eyJ'
            if (!apiKey.StartsWith("sb_publishable_") && 
                !apiKey.StartsWith("sb_secret_") && 
                !apiKey.StartsWith("eyJ"))
            {
                return false;
            }
            
            // Дополнительная проверка длины ключа
            if (apiKey.Length < 20)
            {
                return false;
            }
            
            return true;
        }

        /// <summary>
        /// Проверка подключения к Supabase
        /// </summary>
        public async Task<bool> TestConnection()
        {
            try
            {
                if (string.IsNullOrEmpty(_supabaseUrl) || string.IsNullOrEmpty(_supabaseKey))
                {
                    System.Diagnostics.Debug.WriteLine("Supabase URL or key is empty");
                    MessageBox.Show("URL или API ключ Supabase не указаны", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                // Проверяем валидность API ключа
                if (!ValidateApiKey(_supabaseKey))
                {
                    System.Diagnostics.Debug.WriteLine($"Invalid API key format: {_supabaseKey.Substring(0, Math.Min(10, _supabaseKey.Length))}...");
                    MessageBox.Show("Неверный формат API ключа Supabase.\n\nКлюч должен начинаться с 'sb_publishable_' или 'sb_secret_'", 
                                  "Ошибка ключа", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                System.Diagnostics.Debug.WriteLine($"Testing Supabase connection to: {_supabaseUrl}");
                System.Diagnostics.Debug.WriteLine($"Using API key: {_supabaseKey.Substring(0, Math.Min(10, _supabaseKey.Length))}...");

                // Проверяем базовое подключение
                var response = await _httpClient.GetAsync($"{_supabaseUrl}/rest/v1/");
                
                System.Diagnostics.Debug.WriteLine($"Response status: {response.StatusCode}");
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"Error response: {errorContent}");
                    
                    // Показываем детальную ошибку пользователю
                    string errorMessage = response.StatusCode switch
                    {
                        System.Net.HttpStatusCode.Unauthorized => GetUnauthorizedErrorMessage(errorContent),
                        System.Net.HttpStatusCode.NotFound => "URL Supabase не найден. Проверьте правильность адреса.",
                        System.Net.HttpStatusCode.Forbidden => "Доступ запрещен. Проверьте настройки RLS в Supabase.",
                        _ => $"Ошибка подключения: {response.StatusCode}\n\n{errorContent}"
                    };
                    
                    MessageBox.Show($"Ошибка подключения к Supabase:\n\n{errorMessage}", 
                                  "Ошибка подключения", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
                
                return true;
            }
            catch (HttpRequestException ex)
            {
                System.Diagnostics.Debug.WriteLine($"HTTP request failed: {ex.Message}");
                MessageBox.Show($"Ошибка HTTP запроса к Supabase:\n\n{ex.Message}\n\nПроверьте URL и подключение к интернету.", 
                              "Ошибка подключения", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            catch (TaskCanceledException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Request timeout: {ex.Message}");
                MessageBox.Show($"Таймаут подключения к Supabase:\n\n{ex.Message}\n\nПроверьте подключение к интернету.", 
                              "Ошибка подключения", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Supabase connection test failed: {ex.Message}");
                MessageBox.Show($"Ошибка подключения к Supabase:\n\n{ex.Message}", 
                              "Ошибка подключения", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// Получение всех компаний
        /// </summary>
        public async Task<List<Company>> GetCompanies()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_supabaseUrl}/rest/v1/companies?select=*");
                response.EnsureSuccessStatusCode();
                
                var json = await response.Content.ReadAsStringAsync();
                var companies = JsonSerializer.Deserialize<List<Company>>(json);
                return companies ?? new List<Company>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting companies: {ex.Message}");
                return new List<Company>();
            }
        }

        /// <summary>
        /// Создание новой компании
        /// </summary>
        public async Task<Company?> CreateCompany(Company company)
        {
            try
            {
                var json = JsonSerializer.Serialize(company);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync($"{_supabaseUrl}/rest/v1/companies", content);
                response.EnsureSuccessStatusCode();
                
                var responseJson = await response.Content.ReadAsStringAsync();
                var createdCompany = JsonSerializer.Deserialize<List<Company>>(responseJson);
                return createdCompany?.Count > 0 ? createdCompany[0] : null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating company: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Получение клиентов компании
        /// </summary>
        public async Task<List<Client>> GetClients(int companyId)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_supabaseUrl}/rest/v1/clients?company_id=eq.{companyId}&select=*");
                response.EnsureSuccessStatusCode();
                
                var json = await response.Content.ReadAsStringAsync();
                var clients = JsonSerializer.Deserialize<List<Client>>(json);
                return clients ?? new List<Client>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting clients: {ex.Message}");
                return new List<Client>();
            }
        }

        /// <summary>
        /// Создание нового клиента
        /// </summary>
        public async Task<Client?> CreateClient(Client client)
        {
            try
            {
                var json = JsonSerializer.Serialize(client);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PostAsync($"{_supabaseUrl}/rest/v1/clients", content);
                response.EnsureSuccessStatusCode();
                
                var responseJson = await response.Content.ReadAsStringAsync();
                var createdClient = JsonSerializer.Deserialize<List<Client>>(responseJson);
                return createdClient?.Count > 0 ? createdClient[0] : null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating client: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Обновление клиента
        /// </summary>
        public async Task<Client?> UpdateClient(Client client)
        {
            try
            {
                var json = JsonSerializer.Serialize(client);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                var response = await _httpClient.PatchAsync($"{_supabaseUrl}/rest/v1/clients?id=eq.{client.Id}", content);
                response.EnsureSuccessStatusCode();
                
                var responseJson = await response.Content.ReadAsStringAsync();
                var updatedClient = JsonSerializer.Deserialize<List<Client>>(responseJson);
                return updatedClient?.Count > 0 ? updatedClient[0] : null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating client: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Удаление клиента
        /// </summary>
        public async Task<bool> DeleteClient(int clientId)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"{_supabaseUrl}/rest/v1/clients?id=eq.{clientId}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting client: {ex.Message}");
                return false;
            }
        }
    }
}
