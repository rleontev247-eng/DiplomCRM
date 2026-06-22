using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.EntityFrameworkCore;

namespace MyFirstCRM
{
    public static class YandexDiskDataExporter
    {
        /// <summary>
        /// Выполняет полную выгрузку данных на Яндекс.Диск
        /// </summary>
        public static async Task<bool> ExportAllDataToYandexDisk(string token, string employeeName)
        {
            try
            {
                // Создаем временную папку для экспорта
                var tempFolder = Path.Combine(Path.GetTempPath(), "CRM_Export", DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                Directory.CreateDirectory(tempFolder);

                // Получаем данные из базы
                using (var context = MultiUserSecurityManager.CreateCompanyContext())
                {
                    var clients = context.Clients.ToList();
                    var deals = context.Deals.Include(d => d.Client).ToList();
                    var expenses = new List<Expense>();
                    var events = new List<CalendarEvent>();

                    try
                    {
                        expenses = context.Expenses.ToList();
                    }
                    catch { }

                    try
                    {
                        events = context.CalendarEvents.Include(e => e.Client).ToList();
                    }
                    catch { }

                    // Создаем файлы с данными
                    var files = new List<string>();

                    // 1. Информация об аккаунте и сотруднике
                    var accountInfoPath = Path.Combine(tempFolder, "Информация_об_аккаунте.txt");
                    CreateAccountInfoFile(accountInfoPath, employeeName, clients.Count, deals.Count, expenses.Count, events.Count);
                    files.Add(accountInfoPath);

                    // 2. Клиенты
                    if (clients.Any())
                    {
                        var clientsPath = Path.Combine(tempFolder, "Клиенты.xlsx");
                        ExcelExporter.ExportClientsToExcel(clients, clientsPath);
                        files.Add(clientsPath);
                    }

                    // 3. Сделки
                    if (deals.Any())
                    {
                        var dealsPath = Path.Combine(tempFolder, "Сделки.xlsx");
                        ExcelExporter.ExportDealsToExcel(deals, dealsPath);
                        files.Add(dealsPath);
                    }

                    // 4. Расходы
                    if (expenses.Any())
                    {
                        var expensesPath = Path.Combine(tempFolder, "Расходы.xlsx");
                        ExcelExporter.ExportExpensesToExcel(expenses, expensesPath);
                        files.Add(expensesPath);
                    }

                    // 5. Календарь
                    if (events.Any())
                    {
                        var eventsPath = Path.Combine(tempFolder, "Календарь.xlsx");
                        ExcelExporter.ExportCalendarEventsToExcel(events, eventsPath);
                        files.Add(eventsPath);
                    }

                    // 6. Общая статистика
                    var statsPath = Path.Combine(tempFolder, "Статистика.xlsx");
                    StatisticsExporter.ExportStatistics(statsPath);
                    files.Add(statsPath);

                    // Создаем папку на Яндекс.Диске
                    var folderName = $"CRM_Backup_{DateTime.Now:yyyyMMdd_HHmmss}";
                    await YandexDiskHelper.CreateFolderAsync(token, $"/{folderName}");

                    // Загружаем все файлы
                    var uploadTasks = new List<Task<bool>>();
                    foreach (var file in files)
                    {
                        var fileName = Path.GetFileName(file);
                        var remotePath = $"/{folderName}/{fileName}";
                        uploadTasks.Add(YandexDiskHelper.UploadFileAsync(token, file, remotePath));
                    }

                    // Ждем завершения всех загрузок
                    var results = await Task.WhenAll(uploadTasks);

                    // Проверяем результаты
                    if (results.All(r => r))
                    {
                        MessageBox.Show($"Данные успешно отправлены на Яндекс.Диск в папку: {folderName}", 
                                      "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                        return true;
                    }
                    else
                    {
                        var failedCount = results.Count(r => !r);
                        MessageBox.Show($"Не удалось загрузить {failedCount} файлов из {files.Count}", 
                                      "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при экспорте данных: {ex.Message}", "Ошибка", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// Создает файл с информацией об аккаунте и выгрузке
        /// </summary>
        private static void CreateAccountInfoFile(string filePath, string employeeName, 
            int clientsCount, int dealsCount, int expensesCount, int eventsCount)
        {
            var content = $"ИНФОРМАЦИЯ ОБ АККАУНТЕ И ВЫГРУЗКЕ\n" +
                         $"=====================================\n\n" +
                         $"Дата и время выгрузки: {DateTime.Now:dd.MM.yyyy HH:mm:ss}\n" +
                         $"Сотрудник: {employeeName}\n" +
                         $"CRM система: MyFirstCRM\n\n" +
                         $"СТАТИСТИКА ДАННЫХ:\n" +
                         $"-----------------\n" +
                         $"Всего клиентов: {clientsCount}\n" +
                         $"Всего сделок: {dealsCount}\n" +
                         $"Всего расходов: {expensesCount}\n" +
                         $"Всего событий календаря: {eventsCount}\n\n";

            // Добавляем информацию о компании
            try
            {
                using (var context = MultiUserSecurityManager.CreateCompanyContext())
                {
                    var company = context.Companies.FirstOrDefault();
                    if (company != null)
                    {
                        content += $"ИНФОРМАЦИЯ О КОМПАНИИ:\n" +
                                  $"---------------------\n" +
                                  $"Название: {company.Name}\n" +
                                  $"Код компании: {company.CompanyCode}\n" +
                                  $"Дата создания: {company.CreatedAt:dd.MM.yyyy}\n\n";
                    }
                }
            }
            catch
            {
                content += "ИНФОРМАЦИЯ О КОМПАНИИ: недоступна\n\n";
            }

            content += $"СПРАВКА:\n" +
                      $"--------\n" +
                      $"Эта выгрузка содержит все данные из CRM системы на момент экспорта.\n" +
                      $"Файлы организованы по категориям для удобства анализа.\n" +
                      $"Статистика содержит сводную информацию по всем разделам.\n\n" +
                      $"Сгенерировано автоматически MyFirstCRM";

            File.WriteAllText(filePath, content, System.Text.Encoding.UTF8);
        }
    }
}
