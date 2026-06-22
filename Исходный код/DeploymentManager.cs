using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;
using MyFirstCRM.Supabase;

namespace MyFirstCRM
{
    /// <summary>
    /// Менеджер развертывания и синхронизации CRM
    /// </summary>
    public class DeploymentManager
    {
        private static DeploymentManager? _instance;
        private static readonly object _lock = new object();
        private Timer? _syncTimer;
        private readonly HttpClient _httpClient;
        private DeploymentConfig _config;

        public static DeploymentManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                            _instance = new DeploymentManager();
                    }
                }
                return _instance;
            }
        }

        private DeploymentManager()
        {
            _httpClient = new HttpClient(new HttpClientHandler()
            {
                // Разрешаем переиспользование заголовков
            });
            _config = LoadConfig();
            
            // Инициализируем Supabase если в облачном режиме
            if (_config.Mode == DeploymentMode.Cloud)
            {
                SupabaseManager.Instance.Initialize(_config.CloudServerUrl, _config.CloudApiKey);
            }
            
            StartAutoSync();
        }

        /// <summary>
        /// Текущая конфигурация развертывания
        /// </summary>
        public DeploymentConfig Config
        {
            get => _config;
            set
            {
                _config = value;
                SaveConfig(_config);
                RestartAutoSync();
            }
        }

        /// <summary>
        /// Загрузка конфигурации
        /// </summary>
        private DeploymentConfig LoadConfig()
        {
            try
            {
                string configPath = GetConfigPath();
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    return JsonSerializer.Deserialize<DeploymentConfig>(json) ?? new DeploymentConfig();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading deployment config: {ex.Message}");
            }

            return new DeploymentConfig();
        }

        /// <summary>
        /// Сохранение конфигурации
        /// </summary>
        private void SaveConfig(DeploymentConfig config)
        {
            try
            {
                string configPath = GetConfigPath();
                string directory = Path.GetDirectoryName(configPath);
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(configPath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving deployment config: {ex.Message}");
            }
        }

        /// <summary>
        /// Публичный метод сохранения конфигурации
        /// </summary>
        public void SaveConfiguration(DeploymentConfig config)
        {
            _config = config;
            SaveConfig(_config);
            RestartAutoSync();
        }

        /// <summary>
        /// Получение пути к файлу конфигурации
        /// </summary>
        private string GetConfigPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                               "MyFirstCRM", "deployment.json");
        }

        /// <summary>
        /// Запуск автоматической синхронизации
        /// </summary>
        private void StartAutoSync()
        {
            if (_config.AutoSyncEnabled && _config.Mode != DeploymentMode.Local)
            {
                int intervalMs = _config.SyncIntervalMinutes * 60 * 1000;
                _syncTimer = new Timer(async _ => await PerformAutoSync(), null, intervalMs, intervalMs);
            }
        }

        /// <summary>
        /// Перезапуск автоматической синхронизации
        /// </summary>
        private void RestartAutoSync()
        {
            _syncTimer?.Dispose();
            StartAutoSync();
        }

        /// <summary>
        /// Автоматическая синхронизация
        /// </summary>
        private async Task PerformAutoSync()
        {
            try
            {
                if (_config.Mode == DeploymentMode.Local) 
                {
                    System.Diagnostics.Debug.WriteLine("Auto sync skipped: Local mode");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"Starting auto sync for mode: {_config.Mode}");
                bool result = await SyncAllCompanies();
                System.Diagnostics.Debug.WriteLine($"Auto sync completed: {result}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Auto sync error: {ex.Message}");
                LogSyncError(0, SyncType.Scheduled, ex.Message);
            }
        }

        /// <summary>
        /// Загрузка данных из облачного сервиса
        /// </summary>
        public async Task<bool> LoadFromCloud()
        {
            if (string.IsNullOrEmpty(_config.CloudServerUrl) || string.IsNullOrEmpty(_config.CloudApiKey))
            {
                throw new Exception("Cloud server URL and API key are required for cloud sync");
            }

            try
            {
                System.Diagnostics.Debug.WriteLine("Loading data from cloud...");
                
                // Устанавливаем правильные заголовки для Supabase
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.Add("apikey", _config.CloudApiKey);
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_config.CloudApiKey}");
                httpClient.Timeout = TimeSpan.FromMinutes(5);

                bool allSuccess = true;

                // 1. Загружаем компании
                var companiesResponse = await httpClient.GetAsync($"{_config.CloudServerUrl.TrimEnd('/')}/rest/v1/companies");
                if (companiesResponse.IsSuccessStatusCode)
                {
                    var companiesJson = await companiesResponse.Content.ReadAsStringAsync();
                    var companies = JsonSerializer.Deserialize<List<Company>>(companiesJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    
                    if (companies != null && companies.Any())
                    {
                        using var globalContext = new GlobalDbContext();
                        
                        foreach (var company in companies)
                        {
                            var existingCompany = await globalContext.Companies.FindAsync(company.Id);
                            if (existingCompany == null)
                            {
                                globalContext.Companies.Add(company);
                                System.Diagnostics.Debug.WriteLine($"Added new company: {company.Name}");
                            }
                            else
                            {
                                // Обновляем существующую компанию
                                existingCompany.Name = company.Name;
                                existingCompany.Description = company.Description;
                                existingCompany.IsActive = company.IsActive;
                                System.Diagnostics.Debug.WriteLine($"Updated existing company: {company.Name}");
                            }
                        }
                        
                        await globalContext.SaveChangesAsync();
                        System.Diagnostics.Debug.WriteLine($"Loaded {companies.Count} companies from cloud");
                    }
                }
                else
                {
                    var error = await companiesResponse.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"Failed to load companies from cloud: {error}");
                    allSuccess = false;
                }

                // 2. Загружаем данные для каждой компании
                using var companyContext = new GlobalDbContext();
                var activeCompanies = await companyContext.Companies.Where(c => c.IsActive).ToListAsync();
                
                foreach (var company in activeCompanies)
                {
                    bool success = await LoadCompanyDataFromCloud(company.Id);
                    if (!success) allSuccess = false;
                }

                System.Diagnostics.Debug.WriteLine($"Cloud load completed. Overall success: {allSuccess}");
                return allSuccess;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading from cloud: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Загрузка данных компании из облака
        /// </summary>
        private async Task<bool> LoadCompanyDataFromCloud(int companyId)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Loading company {companyId} data from cloud...");
                
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.Add("apikey", _config.CloudApiKey);
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_config.CloudApiKey}");
                httpClient.Timeout = TimeSpan.FromMinutes(5);

                bool allSuccess = true;

                // Создаем контекст для компании
                using var companyContext = MultiUserSecurityManager.CreateCompanyContext();
                companyContext.CurrentCompanyId = companyId;

                // 1. Загружаем клиентов
                var clientsResponse = await httpClient.GetAsync($"{_config.CloudServerUrl.TrimEnd('/')}/rest/v1/clients?companyid=eq.{companyId}");
                if (clientsResponse.IsSuccessStatusCode)
                {
                    var clientsJson = await clientsResponse.Content.ReadAsStringAsync();
                    var clients = JsonSerializer.Deserialize<List<Client>>(clientsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    
                    if (clients != null && clients.Any())
                    {
                        foreach (var client in clients)
                        {
                            var existingClient = await companyContext.Clients.FindAsync(client.Id);
                            if (existingClient == null)
                            {
                                companyContext.Clients.Add(client);
                            }
                            else
                            {
                                // Обновляем существующего клиента
                                existingClient.Name = client.Name;
                                existingClient.Phone = client.Phone;
                                existingClient.Email = client.Email;
                                existingClient.Notes = client.Notes;
                                existingClient.ABC_Category = client.ABC_Category;
                            }
                        }
                        await companyContext.SaveChangesAsync();
                        System.Diagnostics.Debug.WriteLine($"Loaded {clients.Count} clients for company {companyId}");
                    }
                }

                // 2. Загружаем сделки
                var dealsResponse = await httpClient.GetAsync($"{_config.CloudServerUrl.TrimEnd('/')}/rest/v1/deals?companyid=eq.{companyId}");
                if (dealsResponse.IsSuccessStatusCode)
                {
                    var dealsJson = await dealsResponse.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true,
                        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
                    };
                    var deals = JsonSerializer.Deserialize<List<Deal>>(dealsJson, options);
                    
                    if (deals != null && deals.Any())
                    {
                        foreach (var deal in deals)
                        {
                            var existingDeal = await companyContext.Deals.FindAsync(deal.Id);
                            if (existingDeal == null)
                            {
                                companyContext.Deals.Add(deal);
                            }
                            else
                            {
                                // Обновляем существующую сделку
                                existingDeal.Title = deal.Title;
                                existingDeal.Description = deal.Description;
                                existingDeal.Amount = deal.Amount;
                                existingDeal.Status = deal.Status;
                                existingDeal.UpdatedAt = deal.UpdatedAt;
                            }
                        }
                        await companyContext.SaveChangesAsync();
                        System.Diagnostics.Debug.WriteLine($"Loaded {deals.Count} deals for company {companyId}");
                    }
                }

                // 3. Загружаем расходы
                var expensesResponse = await httpClient.GetAsync($"{_config.CloudServerUrl.TrimEnd('/')}/rest/v1/expenses?companyid=eq.{companyId}");
                if (expensesResponse.IsSuccessStatusCode)
                {
                    var expensesJson = await expensesResponse.Content.ReadAsStringAsync();
                    var expenses = JsonSerializer.Deserialize<List<Expense>>(expensesJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    
                    if (expenses != null && expenses.Any())
                    {
                        foreach (var expense in expenses)
                        {
                            var existingExpense = await companyContext.Expenses.FindAsync(expense.Id);
                            if (existingExpense == null)
                            {
                                companyContext.Expenses.Add(expense);
                            }
                            else
                            {
                                // Обновляем существующий расход
                                existingExpense.Amount = expense.Amount;
                                existingExpense.Category = expense.Category;
                                existingExpense.Notes = expense.Notes;
                                existingExpense.Date = expense.Date;
                            }
                        }
                        await companyContext.SaveChangesAsync();
                        System.Diagnostics.Debug.WriteLine($"Loaded {expenses.Count} expenses for company {companyId}");
                    }
                }

                System.Diagnostics.Debug.WriteLine($"Company {companyId} data load completed");
                return allSuccess;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading company {companyId} data from cloud: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Синхронизация всех компаний
        /// </summary>
        public async Task<bool> SyncAllCompanies()
        {
            try
            {
                using var globalContext = new GlobalDbContext();
                var companies = await globalContext.Companies.Where(c => c.IsActive).ToListAsync();
                
                System.Diagnostics.Debug.WriteLine($"Found {companies.Count} active companies to sync");

                bool allSuccess = true;
                foreach (var company in companies)
                {
                    System.Diagnostics.Debug.WriteLine($"Syncing company {company.Id}: {company.Name}");
                    bool success = await SyncCompany(company.Id);
                    System.Diagnostics.Debug.WriteLine($"Company {company.Id} sync result: {success}");
                    if (!success) allSuccess = false;
                }

                System.Diagnostics.Debug.WriteLine($"All companies sync completed. Overall success: {allSuccess}");
                return allSuccess;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error syncing all companies: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Синхронизация конкретной компании
        /// </summary>
        public async Task<bool> SyncCompany(int companyId)
        {
            try
            {
                var syncLog = new SyncLog
                {
                    CompanyId = companyId,
                    SyncType = SyncType.Incremental,
                    Status = SyncStatus.InProgress,
                    StartedAt = DateTime.Now
                };

                LogSyncStart(syncLog);

                switch (_config.Mode)
                {
                    case DeploymentMode.Cloud:
                        return await SyncToCloud(companyId, syncLog);
                    case DeploymentMode.Hybrid:
                        return await SyncHybrid(companyId, syncLog);
                    case DeploymentMode.ServerClient:
                        return await SyncToServer(companyId, syncLog);
                    default:
                        return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error syncing company {companyId}: {ex.Message}");
                LogSyncError(companyId, SyncType.Incremental, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Синхронизация с облачным сервером
        /// </summary>
        private async Task<bool> SyncToCloud(int companyId, SyncLog syncLog)
        {
            if (string.IsNullOrEmpty(_config.CloudServerUrl) || string.IsNullOrEmpty(_config.CloudApiKey))
            {
                throw new Exception("Cloud server URL and API key are required for cloud sync");
            }

            try
            {
                var companyData = await GetCompanyData(companyId);
                
                // Устанавливаем правильные заголовки для Supabase
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.Add("apikey", _config.CloudApiKey);
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_config.CloudApiKey}");
                // Используем UPSERT вместо INSERT
                httpClient.DefaultRequestHeaders.Add("Prefer", "return=representation,resolution=merge-duplicates");
                httpClient.Timeout = TimeSpan.FromMinutes(5);

                bool allSuccess = true;
                var data = companyData as dynamic;

                // Сначала синхронизируем компании (если нужно)
                using var globalContext = new GlobalDbContext();
                var companies = await globalContext.Companies.Where(c => c.IsActive).ToListAsync();
                
                if (companies.Any())
                {
                    var supabaseCompanies = companies.Select(c => new
                    {
                        id = c.Id,
                        name = c.Name,
                        description = c.Description,
                        created_at = c.CreatedAt
                    }).ToList();
                    
                    var companiesJson = JsonSerializer.Serialize(supabaseCompanies);
                    var companiesContent = new StringContent(companiesJson, Encoding.UTF8, "application/json");
                    var companiesResponse = await httpClient.PostAsync($"{_config.CloudServerUrl.TrimEnd('/')}/rest/v1/companies", companiesContent);
                    
                    if (!companiesResponse.IsSuccessStatusCode)
                    {
                        var error = await companiesResponse.Content.ReadAsStringAsync();
                        System.Diagnostics.Debug.WriteLine($"Companies sync error: {error}");
                        // Не делаем allSuccess = false, так как компании могут уже существовать
                    }
                    
                    // Небольшая задержка для гарантии сохранения
                    await Task.Delay(100);
                }

                // Синхронизация клиентов
                if (data?.Clients != null)
                {
                    var supabaseClients = ((List<Client>)data.Clients).Select(c => new SupabaseClient
                    {
                        Id = c.Id,
                        CompanyId = c.CompanyId,
                        Name = c.Name,
                        Phone = c.Phone,
                        Email = c.Email,
                        Notes = c.Notes,
                        CreatedAt = c.CreatedAt,
                        ABC_Category = c.ABC_Category
                    }).ToList();
                    
                    var clientsJson = JsonSerializer.Serialize(supabaseClients);
                    var clientsContent = new StringContent(clientsJson, Encoding.UTF8, "application/json");
                    var clientsResponse = await httpClient.PostAsync($"{_config.CloudServerUrl.TrimEnd('/')}/rest/v1/clients", clientsContent);
                    
                    if (!clientsResponse.IsSuccessStatusCode)
                    {
                        var error = await clientsResponse.Content.ReadAsStringAsync();
                        System.Diagnostics.Debug.WriteLine($"Clients sync error: {error}");
                        allSuccess = false;
                    }
                    
                    // Задержка для гарантии сохранения клиентов перед сделками
                    await Task.Delay(100);
                }

                // Синхронизация сделок
                if (data?.Deals != null)
                {
                    var supabaseDeals = ((List<Deal>)data.Deals).Select(d => new SupabaseDeal
                    {
                        Id = d.Id,
                        ClientId = d.ClientId,
                        CompanyId = d.CompanyId,
                        Title = d.Title,
                        Description = d.Description,
                        Amount = d.Amount,
                        Status = d.Status.ToString(),
                        CreatedAt = d.CreatedAt,
                        UpdatedAt = d.UpdatedAt
                    }).ToList();
                    
                    var dealsJson = JsonSerializer.Serialize(supabaseDeals);
                    var dealsContent = new StringContent(dealsJson, Encoding.UTF8, "application/json");
                    var dealsResponse = await httpClient.PostAsync($"{_config.CloudServerUrl.TrimEnd('/')}/rest/v1/deals", dealsContent);
                    
                    if (!dealsResponse.IsSuccessStatusCode)
                    {
                        var error = await dealsResponse.Content.ReadAsStringAsync();
                        System.Diagnostics.Debug.WriteLine($"Deals sync error: {error}");
                        allSuccess = false;
                    }
                }

                // Синхронизация расходов
                if (data?.Expenses != null)
                {
                    var supabaseExpenses = ((List<Expense>)data.Expenses).Select(e => new SupabaseExpense
                    {
                        Id = e.Id,
                        CompanyId = e.CompanyId,
                        ClientId = e.ClientId,
                        Amount = e.Amount,
                        Category = e.Category,
                        Description = e.Notes,
                        Date = e.Date,
                        CreatedAt = e.CreatedAt
                    }).ToList();
                    
                    var expensesJson = JsonSerializer.Serialize(supabaseExpenses);
                    var expensesContent = new StringContent(expensesJson, Encoding.UTF8, "application/json");
                    var expensesResponse = await httpClient.PostAsync($"{_config.CloudServerUrl.TrimEnd('/')}/rest/v1/expenses", expensesContent);
                    
                    if (!expensesResponse.IsSuccessStatusCode)
                    {
                        var error = await expensesResponse.Content.ReadAsStringAsync();
                        System.Diagnostics.Debug.WriteLine($"Expenses sync error: {error}");
                        allSuccess = false;
                    }
                }

                // Синхронизация задач
                if (data?.Tasks != null)
                {
                    var tasksJson = JsonSerializer.Serialize(data.Tasks);
                    var tasksContent = new StringContent(tasksJson, Encoding.UTF8, "application/json");
                    var tasksResponse = await httpClient.PostAsync($"{_config.CloudServerUrl.TrimEnd('/')}/rest/v1/tasks", tasksContent);
                    
                    if (!tasksResponse.IsSuccessStatusCode)
                    {
                        var error = await tasksResponse.Content.ReadAsStringAsync();
                        System.Diagnostics.Debug.WriteLine($"Tasks sync error: {error}");
                        allSuccess = false;
                    }
                }

                // Синхронизация уведомлений
                if (data?.Notifications != null)
                {
                    var notifications = (List<Notification>)data.Notifications;
                    var clients = (List<Client>)data.Clients ?? new List<Client>();
                    var validClientIds = clients.Select(c => c.Id).ToHashSet();
                    
                    // Фильтруем уведомления: только те, у которых ClientId существует или ClientId = 0
                    var supabaseNotifications = notifications
                        .Where(n => n.ClientId == 0 || validClientIds.Contains(n.ClientId ?? 0))
                        .Select(n => new SupabaseNotification
                        {
                            Id = n.Id,
                            CompanyId = n.CompanyId,
                            UserId = n.CreatedByUserId,
                            Title = n.Title,
                            Message = n.Message,
                            Type = n.Type.ToString(),
                            IsRead = n.IsRead,
                            CreatedAt = n.CreatedAt,
                            ClientId = n.ClientId
                        }).ToList();
                    
                    if (supabaseNotifications.Any())
                    {
                        var notificationsJson = JsonSerializer.Serialize(supabaseNotifications);
                        var notificationsContent = new StringContent(notificationsJson, Encoding.UTF8, "application/json");
                        var notificationsResponse = await httpClient.PostAsync($"{_config.CloudServerUrl.TrimEnd('/')}/rest/v1/notifications", notificationsContent);
                        
                        if (!notificationsResponse.IsSuccessStatusCode)
                        {
                            var error = await notificationsResponse.Content.ReadAsStringAsync();
                            System.Diagnostics.Debug.WriteLine($"Notifications sync error: {error}");
                            allSuccess = false;
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("No valid notifications to sync (all have invalid ClientId)");
                    }
                }

                // Синхронизация календарных событий
                if (data?.CalendarEvents != null)
                {
                    var supabaseCalendarEvents = ((List<CalendarEvent>)data.CalendarEvents).Select(e => new SupabaseCalendarEvent
                    {
                        Id = e.Id,
                        CompanyId = e.CompanyId,
                        ClientId = e.ClientId,
                        Title = e.Title,
                        Description = e.Description,
                        StartTime = e.StartDate,
                        EndTime = e.EndDate,
                        Location = e.Location,
                        EventType = e.EventType.ToString(),
                        CreatedAt = e.CreatedAt,
                        UpdatedAt = e.UpdatedAt,
                        AssignedToUserId = e.AssignedToUserId ?? 0
                    }).ToList();
                    
                    var calendarEventsJson = JsonSerializer.Serialize(supabaseCalendarEvents);
                    var calendarEventsContent = new StringContent(calendarEventsJson, Encoding.UTF8, "application/json");
                    var calendarEventsResponse = await httpClient.PostAsync($"{_config.CloudServerUrl.TrimEnd('/')}/rest/v1/calendar_events", calendarEventsContent);
                    
                    if (!calendarEventsResponse.IsSuccessStatusCode)
                    {
                        var error = await calendarEventsResponse.Content.ReadAsStringAsync();
                        System.Diagnostics.Debug.WriteLine($"Calendar events sync error: {error}");
                        allSuccess = false;
                    }
                }

                if (allSuccess)
                {
                    syncLog.Status = SyncStatus.Success;
                    syncLog.CompletedAt = DateTime.Now;
                    syncLog.RecordsSent = CountRecords(companyData);
                    LogSyncComplete(syncLog);
                    return true;
                }
                else
                {
                    throw new Exception("Some data types failed to sync");
                }
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                syncLog.Status = SyncStatus.Failed;
                syncLog.ErrorMessage = "Timeout: Cloud server did not respond within the expected time";
                syncLog.CompletedAt = DateTime.Now;
                LogSyncComplete(syncLog);
                return false;
            }
            catch (Exception ex)
            {
                syncLog.Status = SyncStatus.Failed;
                syncLog.ErrorMessage = ex.Message;
                syncLog.CompletedAt = DateTime.Now;
                LogSyncComplete(syncLog);
                return false;
            }
        }

        /// <summary>
        /// Гибридная синхронизация
        /// </summary>
        private async Task<bool> SyncHybrid(int companyId, SyncLog syncLog)
        {
            try
            {
                // Сначала отправляем изменения на сервер
                bool uploadSuccess = await UploadToServer(companyId, syncLog);
                
                if (!_config.OneWaySyncOnly)
                {
                    // Затем скачиваем изменения с сервера
                    bool downloadSuccess = await DownloadFromServer(companyId, syncLog);
                    return downloadSuccess;
                }

                return uploadSuccess;
            }
            catch (Exception ex)
            {
                syncLog.Status = SyncStatus.Failed;
                syncLog.ErrorMessage = ex.Message;
                syncLog.CompletedAt = DateTime.Now;
                LogSyncComplete(syncLog);
                return false;
            }
        }

        /// <summary>
        /// Синхронизация с локальным сервером
        /// </summary>
        private async Task<bool> SyncToServer(int companyId, SyncLog syncLog)
        {
            if (string.IsNullOrEmpty(_config.ServerIpAddress))
            {
                throw new Exception("Server IP address is required for server client sync");
            }

            try
            {
                var companyData = await GetCompanyData(companyId);
                var jsonData = JsonSerializer.Serialize(companyData);

                var content = new StringContent(jsonData, Encoding.UTF8, "application/json");
                string serverUrl = $"http://{_config.ServerIpAddress}:{_config.ServerPort}/api/sync/{companyId}";

                var response = await _httpClient.PostAsync(serverUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    syncLog.Status = SyncStatus.Success;
                    syncLog.CompletedAt = DateTime.Now;
                    LogSyncComplete(syncLog);
                    return true;
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Server sync failed: {response.StatusCode} - {error}");
                }
            }
            catch (Exception ex)
            {
                syncLog.Status = SyncStatus.Failed;
                syncLog.ErrorMessage = ex.Message;
                syncLog.CompletedAt = DateTime.Now;
                LogSyncComplete(syncLog);
                return false;
            }
        }

        /// <summary>
        /// Получение данных компании для синхронизации
        /// </summary>
        private async Task<object> GetCompanyData(int companyId)
        {
            using var context = MultiUserSecurityManager.CreateCompanyContext();
            context.CurrentCompanyId = companyId;

            // Проверяем и добавляем недостающие колонки
            context.EnsureMissingColumns();
            System.Diagnostics.Debug.WriteLine($"EnsureMissingColumns completed for company {companyId} in GetCompanyData");

            try
            {
                var clients = await context.Clients.ToListAsync();
                var deals = await context.Deals.ToListAsync();
                var expenses = await context.Expenses.ToListAsync();
                var tasks = await context.Tasks.ToListAsync();
                var notifications = await context.Notifications.ToListAsync();
                var interactions = await context.Interactions.ToListAsync();
                var calendarEvents = await context.CalendarEvents.ToListAsync();

                return new
                {
                    CompanyId = companyId,
                    SyncTime = DateTime.Now,
                    Clients = clients,
                    Deals = deals,
                    Expenses = expenses,
                    Tasks = tasks,
                    Notifications = notifications,
                    Interactions = interactions,
                    CalendarEvents = calendarEvents
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting company data: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Подсчет количества записей
        /// </summary>
        private int CountRecords(object companyData)
        {
            try
            {
                var json = JsonSerializer.Serialize(companyData);
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                
                int count = 0;
                foreach (var property in root.EnumerateObject())
                {
                    if (property.Name != "CompanyId" && property.Name != "SyncTime")
                    {
                        if (property.Value.ValueKind == JsonValueKind.Array)
                        {
                            count += property.Value.GetArrayLength();
                        }
                        else
                        {
                            count++;
                        }
                    }
                }
                return count;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Загрузка данных на сервер
        /// </summary>
        private async Task<bool> UploadToServer(int companyId, SyncLog syncLog)
        {
            // Реализация загрузки на сервер
            // Это будет зависеть от конкретной реализации сервера
            return true;
        }

        /// <summary>
        /// Скачивание данных с сервера
        /// </summary>
        private async Task<bool> DownloadFromServer(int companyId, SyncLog syncLog)
        {
            // Реализация скачивания с сервера
            // Это будет зависеть от конкретной реализации сервера
            return true;
        }

        /// <summary>
        /// Сжатие данных
        /// </summary>
        private string CompressData(string data)
        {
            try
            {
                using var output = new MemoryStream();
                using var gzip = new System.IO.Compression.GZipStream(output, System.IO.Compression.CompressionMode.Compress);
                using var writer = new StreamWriter(gzip, Encoding.UTF8);
                writer.Write(data);
                writer.Flush();
                gzip.Close();
                return Convert.ToBase64String(output.ToArray());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Compression error: {ex.Message}");
                return data; // Возвращаем как есть если сжатие не удалось
            }
        }

        /// <summary>
        /// Шифрование данных
        /// </summary>
        private string EncryptData(string data, string key)
        {
            try
            {
                using var aes = Aes.Create();
                using var sha = SHA256.Create();
                
                var keyBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(key));
                aes.Key = keyBytes;
                aes.GenerateIV();
                
                using var encryptor = aes.CreateEncryptor();
                using var msEncrypt = new MemoryStream();
                using var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write);
                using var swEncrypt = new StreamWriter(csEncrypt);
                
                swEncrypt.Write(data);
                swEncrypt.Flush();
                csEncrypt.FlushFinalBlock();
                
                var encrypted = msEncrypt.ToArray();
                var result = new byte[aes.IV.Length + encrypted.Length];
                Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
                Buffer.BlockCopy(encrypted, 0, result, aes.IV.Length, encrypted.Length);
                
                return Convert.ToBase64String(result);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Encryption error: {ex.Message}");
                return data; // Возвращаем как есть если шифрование не удалось
            }
        }

        /// <summary>
        /// Логирование начала синхронизации
        /// </summary>
        private void LogSyncStart(SyncLog syncLog)
        {
            try
            {
                using var context = new GlobalDbContext();
                context.SyncLogs.Add(syncLog);
                context.SaveChanges();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error logging sync start: {ex.Message}");
            }
        }

        /// <summary>
        /// Логирование завершения синхронизации
        /// </summary>
        private void LogSyncComplete(SyncLog syncLog)
        {
            try
            {
                using var context = new GlobalDbContext();
                context.SyncLogs.Update(syncLog);
                context.SaveChanges();

                _config.LastSyncAt = DateTime.Now;
                _config.LastSyncStatus = syncLog.Status;
                SaveConfig(_config);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error logging sync complete: {ex.Message}");
            }
        }

        /// <summary>
        /// Логирование ошибки синхронизации
        /// </summary>
        private void LogSyncError(int companyId, SyncType syncType, string errorMessage)
        {
            try
            {
                var syncLog = new SyncLog
                {
                    CompanyId = companyId,
                    SyncType = syncType,
                    Status = SyncStatus.Failed,
                    ErrorMessage = errorMessage,
                    StartedAt = DateTime.Now,
                    CompletedAt = DateTime.Now
                };

                using var context = new GlobalDbContext();
                context.SyncLogs.Add(syncLog);
                context.SaveChanges();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error logging sync error: {ex.Message}");
            }
        }

        /// <summary>
        /// Проверка доступности сервера
        /// </summary>
        public async Task<bool> TestServerConnection()
        {
            try
            {
                switch (_config.Mode)
                {
                    case DeploymentMode.Cloud:
                        if (string.IsNullOrEmpty(_config.CloudServerUrl) || string.IsNullOrEmpty(_config.CloudApiKey)) 
                            return false;
                        
                        // Используем SupabaseManager для проверки подключения
                        SupabaseManager.Instance.Initialize(_config.CloudServerUrl, _config.CloudApiKey);
                        return await SupabaseManager.Instance.TestConnection();

                    case DeploymentMode.ServerClient:
                        if (string.IsNullOrEmpty(_config.ServerIpAddress)) return false;
                        string serverUrl = $"http://{_config.ServerIpAddress}:{_config.ServerPort}/api/health";
                        var serverResponse = await _httpClient.GetAsync(serverUrl);
                        return serverResponse.IsSuccessStatusCode;

                    default:
                        return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Server connection test failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Получение статуса синхронизации
        /// </summary>
        public async Task<List<SyncLog>> GetSyncHistory(int companyId, int limit = 50)
        {
            try
            {
                using var context = new GlobalDbContext();
                return await context.SyncLogs
                    .Where(log => log.CompanyId == companyId)
                    .OrderByDescending(log => log.StartedAt)
                    .Take(limit)
                    .ToListAsync();
            }
            catch
            {
                return new List<SyncLog>();
            }
        }

        public void Dispose()
        {
            _syncTimer?.Dispose();
            _httpClient?.Dispose();
        }
    }
}
