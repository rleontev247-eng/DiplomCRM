using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace MyFirstCRM
{
    public static class DealExcelImporter
    {
        public static (int added, int updated, int errors, int skipped) ImportDealsFromExcel(string filePath, bool updateExisting = true, bool createNewClients = false)
        {
            int added = 0, updated = 0, errors = 0, skipped = 0;

            // Отладочная информация - выводим параметры
            Console.WriteLine($"=== НАЧАЛО ИМПОРТА СДЕЛОК ===");
            Console.WriteLine($"Файл: {filePath}");
            Console.WriteLine($"Обновлять существующие: {updateExisting}");
            Console.WriteLine($"Создавать новых клиентов: {createNewClients}");
            Console.WriteLine($"================================");

            try
            {
                using (var context = MultiUserSecurityManager.CreateCompanyContext())
                {
                    var fileInfo = new FileInfo(filePath);
                    using (var package = new ExcelPackage(fileInfo))
                    {
                        var worksheet = package.Workbook.Worksheets[0];
                        int rowCount = worksheet.Dimension?.Rows ?? 0;
                        int colCount = worksheet.Dimension?.Columns ?? 0;

                        if (rowCount <= 1) return (0, 0, 0, 0);

                        // Ищем строку с заголовками
                        int headerRow = FindHeaderRow(worksheet, rowCount);
                        if (headerRow == -1)
                        {
                            MessageBox.Show("Не найдена строка с заголовками!", "Ошибка", 
                                MessageBoxButton.OK, MessageBoxImage.Error);
                            return (0, 0, 0, 1);
                        }

                        // Автоопределение колонок
                        int idCol = -1, titleCol = -1, clientNameCol = -1, amountCol = -1,
                            statusCol = -1, deadlineCol = -1, probabilityCol = -1,
                            categoryCol = -1, descriptionCol = -1;

                        // Читаем заголовки из найденной строки
                        for (int col = 1; col <= colCount; col++)
                        {
                            var header = worksheet.Cells[headerRow, col].Text?.Trim().ToLower() ?? "";

                            // Расширенный поиск заголовков с учетом формата экспорта
                            if (header == "id" || header.Contains("ид"))
                                idCol = col;
                            else if (header.Contains("название") || header.Contains("title") || header == "название")
                                titleCol = col;
                            else if (header.Contains("клиент") || header.Contains("client") || header == "клиент")
                                clientNameCol = col;
                            else if (header.Contains("сумма") || header.Contains("amount") || header == "сумма")
                                amountCol = col;
                            else if (header.Contains("статус") || header.Contains("status") || header == "статус")
                                statusCol = col;
                            else if (header.Contains("срок") || header.Contains("deadline") || header.Contains("дата"))
                                deadlineCol = col;
                            else if (header.Contains("вероятность") || header.Contains("probability") || header == "вероятность")
                                probabilityCol = col;
                            else if (header.Contains("категория") || header.Contains("category") || header == "категория")
                                categoryCol = col;
                            else if (header.Contains("описание") || header.Contains("description") || header == "примечание")
                                descriptionCol = col;
                        }

                        // Если основные колонки не найдены, пробуем найти по другим признакам
                        if (titleCol == -1)
                        {
                            for (int col = 1; col <= colCount; col++)
                            {
                                var header = worksheet.Cells[headerRow, col].Text?.Trim().ToLower() ?? "";
                                if (header.Contains("сделк") || header.Contains("deal"))
                                    titleCol = col;
                            }
                        }

                        if (titleCol == -1 || clientNameCol == -1)
                        {
                            MessageBox.Show("Не найдены обязательные колонки: Название сделки и Клиент!", "Ошибка", 
                                MessageBoxButton.OK, MessageBoxImage.Error);
                            return (0, 0, 0, 1);
                        }

                        // Импорт строк
                        for (int row = headerRow + 1; row <= rowCount; row++)
                        {
                            try
                            {
                                // Чтение данных из колонок
                                string idText = idCol > 0 ? worksheet.Cells[row, idCol].Text?.Trim() : "";
                                string title = titleCol > 0 ? worksheet.Cells[row, titleCol].Text?.Trim() : "";
                                string clientName = clientNameCol > 0 ? worksheet.Cells[row, clientNameCol].Text?.Trim() : "";
                                string amountText = amountCol > 0 ? worksheet.Cells[row, amountCol].Text?.Trim() : "0";
                                string statusText = statusCol > 0 ? worksheet.Cells[row, statusCol].Text?.Trim() : "Новые";
                                string deadlineText = deadlineCol > 0 ? worksheet.Cells[row, deadlineCol].Text?.Trim() : "";
                                string probabilityText = probabilityCol > 0 ? worksheet.Cells[row, probabilityCol].Text?.Trim() : "50";
                                string category = categoryCol > 0 ? worksheet.Cells[row, categoryCol].Text?.Trim() : "";
                                string description = descriptionCol > 0 ? worksheet.Cells[row, descriptionCol].Text?.Trim() : "";

                                // Очистка данных
                                idText = CleanImportedText(idText) ?? "";
                                title = CleanImportedText(title) ?? "";
                                clientName = CleanImportedText(clientName) ?? "";
                                amountText = CleanImportedText(amountText) ?? "";
                                statusText = CleanImportedText(statusText) ?? "";
                                deadlineText = CleanImportedText(deadlineText) ?? "";
                                probabilityText = CleanImportedText(probabilityText) ?? "";
                                category = CleanImportedText(category) ?? "";
                                description = CleanImportedText(description) ?? "";

                                // Проверка обязательных полей
                                if (string.IsNullOrWhiteSpace(title))
                                {
                                    errors++;
                                    continue;
                                }

                                if (string.IsNullOrWhiteSpace(clientName))
                                {
                                    errors++;
                                    continue;
                                }

                                // Поиск или создание клиента с улучшенной логикой
                                Client? client = null;
                                
                                // Сначала ищем точное совпадение по имени
                                client = context.Clients.FirstOrDefault(c => 
                                    c.Name.ToLower().Trim() == clientName.ToLower().Trim());

                                // Отладочная информация
                                Console.WriteLine($"Строка {row}: Ищем клиента '{clientName}' - " + 
                                    (client != null ? $"найден (ID: {client.Id})" : "не найден"));

                                // Если не найдено и разрешено создавать новых клиентов
                                if (client == null && createNewClients)
                                {
                                    client = new Client
                                    {
                                        Name = clientName.Trim(),
                                        CreatedAt = DateTime.Now
                                    };
                                    context.Clients.Add(client);
                                    context.SaveChanges();
                                    Console.WriteLine($"Строка {row}: Создан новый клиент '{clientName}' (ID: {client.Id})");
                                }
                                else if (client == null && !createNewClients)
                                {
                                    // Клиент не найден и создание запрещено - пропускаем сделку
                                    Console.WriteLine($"Строка {row}: Клиент '{clientName}' не найден и создание запрещено - пропуск");
                                    skipped++;
                                    continue;
                                }

                                // Преобразуем ID если есть
                                int? dealId = null;
                                if (!string.IsNullOrEmpty(idText) && int.TryParse(idText, out int parsedId))
                                {
                                    dealId = parsedId;
                                }

                                // Проверяем, существует ли уже такая сделка
                                Deal? existingDeal = null;
                                
                                // Сначала ищем по ID (если есть в Excel)
                                if (dealId.HasValue)
                                {
                                    existingDeal = context.Deals.FirstOrDefault(d => d.Id == dealId.Value);
                                    Console.WriteLine($"Строка {row}: Поиск сделки по ID {dealId.Value} - " + 
                                        (existingDeal != null ? $"найдена" : "не найдена"));
                                }
                                
                                // Если не нашли по ID, ищем по названию + клиент
                                if (existingDeal == null)
                                {
                                    existingDeal = context.Deals
                                        .FirstOrDefault(d => d.Title.ToLower().Trim() == title.ToLower().Trim() && d.ClientId == client.Id);
                                    
                                    Console.WriteLine($"Строка {row}: Поиск сделки по названию '{title}' и клиенту {client.Name} - " + 
                                        (existingDeal != null ? $"найдена (ID: {existingDeal.Id})" : "не найдена"));
                                }

                                // Преобразуем данные
                                decimal amount = 0;
                                if (!string.IsNullOrEmpty(amountText))
                                {
                                    // Убираем все символы, кроме цифр и запятой/точки
                                    string cleanAmount = new string(amountText.Where(c => char.IsDigit(c) || c == '.' || c == ',').ToArray());
                                    cleanAmount = cleanAmount.Replace(',', '.');

                                    if (decimal.TryParse(cleanAmount, out decimal amt))
                                        amount = amt;
                                }

                                DealStatus status = DealStatus.New;
                                if (!string.IsNullOrEmpty(statusText))
                                {
                                    statusText = statusText.ToLower();
                                    if (statusText.Contains("работа") || statusText.Contains("в работе"))
                                        status = DealStatus.InProgress;
                                    else if (statusText.Contains("успеш") || statusText.Contains("завершена"))
                                        status = DealStatus.Successful;
                                    else if (statusText.Contains("провал") || statusText.Contains("отменена"))
                                        status = DealStatus.Failed;
                                }

                                DateTime deadline = DateTime.Now.AddDays(30);
                                if (!string.IsNullOrEmpty(deadlineText))
                                {
                                    // Пробуем разные форматы даты
                                    if (DateTime.TryParse(deadlineText, out DateTime dt))
                                        deadline = dt;
                                    else if (DateTime.TryParseExact(deadlineText, "dd.MM.yyyy",
                                             System.Globalization.CultureInfo.InvariantCulture,
                                             System.Globalization.DateTimeStyles.None, out DateTime dtExact))
                                        deadline = dtExact;
                                }

                                int probability = 50;
                                if (!string.IsNullOrEmpty(probabilityText))
                                {
                                    // Убираем проценты и пробелы
                                    probabilityText = probabilityText.Replace("%", "").Trim();
                                    if (int.TryParse(probabilityText, out int prob))
                                        probability = Math.Clamp(prob, 0, 100);
                                }

                                if (existingDeal != null)
                                {
                                    if (updateExisting)
                                    {
                                        // Обновляем существующую сделку
                                        existingDeal.Title = title;
                                        existingDeal.Amount = amount;
                                        existingDeal.Status = status;
                                        existingDeal.Deadline = deadline;
                                        existingDeal.Probability = probability;
                                        existingDeal.Category = category;
                                        existingDeal.Description = description;
                                        updated++;
                                        Console.WriteLine($"Строка {row}: Сделка обновлена");
                                    }
                                    else
                                    {
                                        // Пропускаем существующую сделку
                                        skipped++;
                                        Console.WriteLine($"Строка {row}: Сделка пропущена (существует и обновление запрещено)");
                                        continue;
                                    }
                                }
                                else
                                {
                                    // Создаем новую сделку
                                    var newDeal = new Deal
                                    {
                                        Title = title,
                                        ClientId = client.Id,
                                        Amount = amount,
                                        Status = status,
                                        Deadline = deadline,
                                        Probability = probability,
                                        CreatedAt = DateTime.Now,
                                        Category = category,
                                        Description = description,
                                        Priority = Priority.Medium
                                    };

                                    context.Deals.Add(newDeal);
                                    added++;
                                    Console.WriteLine($"Строка {row}: Создана новая сделка");
                                }

                                // Сохраняем каждые 10 записей
                                if ((added + updated) % 10 == 0)
                                    context.SaveChanges();
                            }
                            catch (Exception ex)
                            {
                                errors++;
                                Console.WriteLine($"Ошибка в строке {row}: {ex.Message}");
                            }
                        }

                        // Сохраняем оставшиеся изменения
                        context.SaveChanges();
                        
                        // Финальная статистика
                        Console.WriteLine($"=== КОНЕЦ ИМПОРТА СДЕЛОК ===");
                        Console.WriteLine($"Добавлено: {added}");
                        Console.WriteLine($"Обновлено: {updated}");
                        Console.WriteLine($"Пропущено: {skipped}");
                        Console.WriteLine($"Ошибки: {errors}");
                        Console.WriteLine($"=============================");
                    }
                }

                return (added, updated, errors, skipped);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка импорта сделок: {ex.Message}\n\n" +
                              "Убедитесь, что файл Excel не открыт в другой программе.",
                              "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return (0, 0, 1, 0);
            }
        }

        public static List<string> ValidateExcelFile(string filePath)
        {
            var errors = new List<string>();

            try
            {
                var fileInfo = new FileInfo(filePath);
                using (var package = new ExcelPackage(fileInfo))
                {
                    var worksheet = package.Workbook.Worksheets[0];
                    int rowCount = worksheet.Dimension?.Rows ?? 0;

                    if (rowCount <= 1)
                        errors.Add("Файл пуст или содержит только заголовки");

                    // Ищем строку с заголовками
                    int headerRow = FindHeaderRow(worksheet, rowCount);
                    if (headerRow == -1)
                    {
                        errors.Add("Не найдена строка с заголовками");
                        return errors;
                    }

                    // Проверяем наличие обязательных колонок
                    bool hasTitle = false;
                    bool hasClient = false;

                    for (int col = 1; col <= worksheet.Dimension.Columns; col++)
                    {
                        var header = worksheet.Cells[headerRow, col].Text?.Trim().ToLower() ?? "";
                        if (header.Contains("название") || header.Contains("title") || header.Contains("сделк"))
                            hasTitle = true;
                        if (header.Contains("клиент") || header.Contains("client"))
                            hasClient = true;
                    }

                    if (!hasTitle)
                        errors.Add("Не найдена колонка с названием сделки");
                    if (!hasClient)
                        errors.Add("Не найдена колонка с именем клиента");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Ошибка чтения файла: {ex.Message}");
            }

            return errors;
        }

        private static int FindHeaderRow(ExcelWorksheet worksheet, int maxRow)
        {
            // Ищем строку с заголовками, проверяя первые 10 строк
            for (int row = 1; row <= Math.Min(10, maxRow); row++)
            {
                bool hasHeader = false;
                for (int col = 1; col <= worksheet.Dimension.Columns; col++)
                {
                    var cellValue = worksheet.Cells[row, col].Text?.Trim().ToLower() ?? "";
                    if (cellValue.Contains("название") || cellValue.Contains("title") || cellValue.Contains("сделк") ||
                        cellValue.Contains("клиент") || cellValue.Contains("client") || cellValue.Contains("сумма") ||
                        cellValue.Contains("amount") || cellValue.Contains("статус") || cellValue.Contains("status"))
                    {
                        hasHeader = true;
                        break;
                    }
                }
                if (hasHeader)
                    return row;
            }
            return -1; // Не найдено
        }

        private static string CleanImportedText(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return text ?? "";

            // Удаляем лишние пробелы и спецсимволы
            text = text.Trim();
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " "); // Заменяем множественные пробелы на один
            text = text.Replace("\n", " ").Replace("\r", " "); // Заменяем переносы строк на пробелы
            
            return text;
        }
    }
}