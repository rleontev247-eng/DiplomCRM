using System;
using System.Collections.Generic;
using System.IO;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;
using System.ComponentModel;
using System.Text;

namespace MyFirstCRM
{
    public static class ExcelExporter
    {
        public static void ExportClientsToExcel(List<Client> clients, string filePath)
        {

            using (var package = new ExcelPackage())
            {
                // Основной лист с клиентами
                var worksheet = package.Workbook.Worksheets.Add("Клиенты");

                // Заголовок
                worksheet.Cells[1, 1].Value = "Экспорт клиентов";
                worksheet.Cells[1, 1, 1, 7].Merge = true;
                worksheet.Cells[1, 1].Style.Font.Size = 16;
                worksheet.Cells[1, 1].Style.Font.Bold = true;
                worksheet.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                worksheet.Cells[1, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                worksheet.Cells[1, 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(59, 130, 246));
                worksheet.Cells[1, 1].Style.Font.Color.SetColor(Color.White);

                // Информация об экспорте
                worksheet.Cells[2, 1].Value = $"Дата экспорта: {DateTime.Now:dd.MM.yyyy HH:mm}";
                worksheet.Cells[2, 1, 2, 7].Merge = true;
                worksheet.Cells[2, 1].Style.Font.Italic = true;
                worksheet.Cells[2, 1].Style.Font.Size = 10;

                worksheet.Cells[3, 1].Value = $"Всего клиентов: {clients.Count}";
                worksheet.Cells[3, 1, 3, 7].Merge = true;
                worksheet.Cells[3, 1].Style.Font.Size = 11;
                worksheet.Cells[3, 1].Style.Font.Bold = true;

                // Заголовки столбцов
                var headerRow = 5;
                string[] headers = { "ID", "ФИО", "Телефон", "Email", "Дата добавления", "Примечания", "Статус" };

                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cells[headerRow, i + 1].Value = headers[i];
                    worksheet.Cells[headerRow, i + 1].Style.Font.Bold = true;
                    worksheet.Cells[headerRow, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    worksheet.Cells[headerRow, i + 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(226, 232, 240));
                    worksheet.Cells[headerRow, i + 1].Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                    worksheet.Cells[headerRow, i + 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                }

                // Данные клиентов
                int row = headerRow + 1;
                foreach (var client in clients)
                {
                    worksheet.Cells[row, 1].Value = client.Id;
                    worksheet.Cells[row, 2].Value = client.Name;
                    worksheet.Cells[row, 3].Value = client.Phone;
                    worksheet.Cells[row, 4].Value = client.Email;
                    worksheet.Cells[row, 5].Value = client.CreatedAt.ToString("dd.MM.yyyy HH:mm");
                    worksheet.Cells[row, 6].Value = client.Notes;
                    worksheet.Cells[row, 7].Value = "Активен";

                    // Форматирование
                    if (row % 2 == 0)
                    {
                        worksheet.Cells[row, 1, row, 7].Style.Fill.PatternType = ExcelFillStyle.Solid;
                        worksheet.Cells[row, 1, row, 7].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(248, 250, 252));
                    }

                    row++;
                }

                // Автоподбор ширины столбцов
                worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

                // Границы для таблицы
                var tableRange = worksheet.Cells[headerRow, 1, row - 1, 7];
                tableRange.Style.Border.Top.Style = ExcelBorderStyle.Thin;
                tableRange.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                tableRange.Style.Border.Left.Style = ExcelBorderStyle.Thin;
                tableRange.Style.Border.Right.Style = ExcelBorderStyle.Thin;
                tableRange.Style.Border.Top.Color.SetColor(Color.LightGray);
                tableRange.Style.Border.Bottom.Color.SetColor(Color.LightGray);
                tableRange.Style.Border.Left.Color.SetColor(Color.LightGray);
                tableRange.Style.Border.Right.Color.SetColor(Color.LightGray);

                // Лист со статистикой
                var statsWorksheet = package.Workbook.Worksheets.Add("Статистика");

                statsWorksheet.Cells[1, 1].Value = "Статистика по клиентам";
                statsWorksheet.Cells[1, 1, 1, 3].Merge = true;
                statsWorksheet.Cells[1, 1].Style.Font.Size = 14;
                statsWorksheet.Cells[1, 1].Style.Font.Bold = true;

                statsWorksheet.Cells[3, 1].Value = "Показатель";
                statsWorksheet.Cells[3, 2].Value = "Значение";
                statsWorksheet.Cells[3, 3].Value = "Процент";

                // Вычисляем статистику
                int withPhone = clients.Count(c => !string.IsNullOrEmpty(c.Phone));
                int withEmail = clients.Count(c => !string.IsNullOrEmpty(c.Email));
                int withNotes = clients.Count(c => !string.IsNullOrEmpty(c.Notes));

                var stats = new[]
                {
                    new { Name = "Всего клиентов", Value = clients.Count, Percent = 100 },
                    new { Name = "С телефоном", Value = withPhone, Percent = (int)((double)withPhone / clients.Count * 100) },
                    new { Name = "С email", Value = withEmail, Percent = (int)((double)withEmail / clients.Count * 100) },
                    new { Name = "С примечаниями", Value = withNotes, Percent = (int)((double)withNotes / clients.Count * 100) },
                };

                int statsRow = 4;
                foreach (var stat in stats)
                {
                    statsWorksheet.Cells[statsRow, 1].Value = stat.Name;
                    statsWorksheet.Cells[statsRow, 2].Value = stat.Value;
                    statsWorksheet.Cells[statsRow, 3].Value = $"{stat.Percent}%";

                    if (stat.Name != "Всего клиентов")
                    {
                        // Добавляем индикатор прогресса
                        var cell = statsWorksheet.Cells[statsRow, 3];
                        cell.Style.Font.Color.SetColor(stat.Percent > 50 ? Color.Green : stat.Percent > 20 ? Color.Orange : Color.Red);
                    }

                    statsRow++;
                }

                statsWorksheet.Cells[3, 1, statsRow - 1, 3].Style.Border.Top.Style = ExcelBorderStyle.Thin;
                statsWorksheet.Cells[3, 1, statsRow - 1, 3].Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                statsWorksheet.Cells[3, 1, statsRow - 1, 3].Style.Border.Left.Style = ExcelBorderStyle.Thin;
                statsWorksheet.Cells[3, 1, statsRow - 1, 3].Style.Border.Right.Style = ExcelBorderStyle.Thin;

                statsWorksheet.Cells.AutoFitColumns();

                // Сохраняем файл
                package.SaveAs(new FileInfo(filePath));
            }
        }
        public static void CreateEmployeeInfoFile(string filePath, string employeeName)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Информация о сотруднике");
            sb.AppendLine("========================");
            sb.AppendLine($"ФИО: {employeeName}");
            sb.AppendLine($"Дата выгрузки: {DateTime.Now:dd.MM.yyyy HH:mm:ss}");
            sb.AppendLine($"Название CRM: DiplomCRM");
            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }
        public static void ExportDealsToExcel(List<Deal> deals, string filePath)
        {
            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Сделки");
                worksheet.Cells[1, 1].Value = "ID";
                worksheet.Cells[1, 2].Value = "Название";
                worksheet.Cells[1, 3].Value = "Клиент";
                worksheet.Cells[1, 4].Value = "Сумма";
                worksheet.Cells[1, 5].Value = "Статус";
                worksheet.Cells[1, 6].Value = "Приоритет";
                worksheet.Cells[1, 7].Value = "Вероятность";
                worksheet.Cells[1, 8].Value = "Срок";
                worksheet.Cells[1, 9].Value = "Дата создания";
                worksheet.Cells[1, 10].Value = "Категория";

                int row = 2;
                foreach (var deal in deals)
                {
                    worksheet.Cells[row, 1].Value = deal.Id;
                    worksheet.Cells[row, 2].Value = deal.Title;
                    worksheet.Cells[row, 3].Value = deal.Client?.Name ?? "Не указан";
                    worksheet.Cells[row, 4].Value = deal.Amount;
                    worksheet.Cells[row, 5].Value = deal.Status.ToString();
                    worksheet.Cells[row, 6].Value = deal.Priority.ToString();
                    worksheet.Cells[row, 7].Value = deal.Probability;
                    worksheet.Cells[row, 8].Value = deal.Deadline.ToString("dd.MM.yyyy");
                    worksheet.Cells[row, 9].Value = deal.CreatedAt.ToString("dd.MM.yyyy HH:mm");
                    worksheet.Cells[row, 10].Value = deal.Category ?? "";
                    row++;
                }

                worksheet.Cells[1, 1, row - 1, 10].AutoFitColumns();
                package.SaveAs(new FileInfo(filePath));
            }
        }

        public static void ExportExpensesToExcel(List<Expense> expenses, string filePath)
        {
            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Расходы");
                
                // Заголовок
                worksheet.Cells[1, 1].Value = "Экспорт расходов";
                worksheet.Cells[1, 1, 1, 6].Merge = true;
                worksheet.Cells[1, 1].Style.Font.Size = 16;
                worksheet.Cells[1, 1].Style.Font.Bold = true;
                worksheet.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                worksheet.Cells[1, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                worksheet.Cells[1, 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(239, 68, 68));
                worksheet.Cells[1, 1].Style.Font.Color.SetColor(Color.White);

                worksheet.Cells[2, 1].Value = $"Дата экспорта: {DateTime.Now:dd.MM.yyyy HH:mm}";
                worksheet.Cells[2, 1, 2, 6].Merge = true;
                worksheet.Cells[2, 1].Style.Font.Italic = true;
                worksheet.Cells[2, 1].Style.Font.Size = 10;

                worksheet.Cells[3, 1].Value = $"Всего расходов: {expenses.Count}";
                worksheet.Cells[3, 1, 3, 6].Merge = true;
                worksheet.Cells[3, 1].Style.Font.Size = 11;
                worksheet.Cells[3, 1].Style.Font.Bold = true;

                // Заголовки столбцов
                var headerRow = 5;
                string[] headers = { "ID", "Название", "Категория", "Сумма", "Дата", "Примечания" };

                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cells[headerRow, i + 1].Value = headers[i];
                    worksheet.Cells[headerRow, i + 1].Style.Font.Bold = true;
                    worksheet.Cells[headerRow, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    worksheet.Cells[headerRow, i + 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(254, 226, 226));
                    worksheet.Cells[headerRow, i + 1].Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                    worksheet.Cells[headerRow, i + 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                }

                // Данные расходов
                int row = headerRow + 1;
                foreach (var expense in expenses)
                {
                    worksheet.Cells[row, 1].Value = expense.Id;
                    worksheet.Cells[row, 2].Value = expense.Title;
                    worksheet.Cells[row, 3].Value = expense.Category;
                    worksheet.Cells[row, 4].Value = expense.Amount;
                    worksheet.Cells[row, 5].Value = expense.Date.ToString("dd.MM.yyyy");
                    worksheet.Cells[row, 6].Value = expense.Notes;

                    // Форматирование
                    if (row % 2 == 0)
                    {
                        worksheet.Cells[row, 1, row, 6].Style.Fill.PatternType = ExcelFillStyle.Solid;
                        worksheet.Cells[row, 1, row, 6].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(255, 250, 250));
                    }

                    row++;
                }

                worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
                package.SaveAs(new FileInfo(filePath));
            }
        }

        public static void ExportCalendarEventsToExcel(List<CalendarEvent> events, string filePath)
        {
            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Календарь");
                
                // Заголовок
                worksheet.Cells[1, 1].Value = "Экспорт событий календаря";
                worksheet.Cells[1, 1, 1, 8].Merge = true;
                worksheet.Cells[1, 1].Style.Font.Size = 16;
                worksheet.Cells[1, 1].Style.Font.Bold = true;
                worksheet.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                worksheet.Cells[1, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                worksheet.Cells[1, 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(34, 197, 94));
                worksheet.Cells[1, 1].Style.Font.Color.SetColor(Color.White);

                worksheet.Cells[2, 1].Value = $"Дата экспорта: {DateTime.Now:dd.MM.yyyy HH:mm}";
                worksheet.Cells[2, 1, 2, 8].Merge = true;
                worksheet.Cells[2, 1].Style.Font.Italic = true;
                worksheet.Cells[2, 1].Style.Font.Size = 10;

                worksheet.Cells[3, 1].Value = $"Всего событий: {events.Count}";
                worksheet.Cells[3, 1, 3, 8].Merge = true;
                worksheet.Cells[3, 1].Style.Font.Size = 11;
                worksheet.Cells[3, 1].Style.Font.Bold = true;

                // Заголовки столбцов
                var headerRow = 5;
                string[] headers = { "ID", "Название", "Тип", "Дата начала", "Дата окончания", "Место", "Клиент", "Статус" };

                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cells[headerRow, i + 1].Value = headers[i];
                    worksheet.Cells[headerRow, i + 1].Style.Font.Bold = true;
                    worksheet.Cells[headerRow, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    worksheet.Cells[headerRow, i + 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(220, 252, 231));
                    worksheet.Cells[headerRow, i + 1].Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
                    worksheet.Cells[headerRow, i + 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                }

                // Данные событий
                int row = headerRow + 1;
                foreach (var evt in events)
                {
                    worksheet.Cells[row, 1].Value = evt.Id;
                    worksheet.Cells[row, 2].Value = evt.Title;
                    worksheet.Cells[row, 3].Value = evt.EventType.ToString();
                    worksheet.Cells[row, 4].Value = evt.StartDate.ToString("dd.MM.yyyy HH:mm");
                    worksheet.Cells[row, 5].Value = evt.EndDate.ToString("dd.MM.yyyy HH:mm");
                    worksheet.Cells[row, 6].Value = evt.Location ?? "";
                    worksheet.Cells[row, 7].Value = evt.Client?.Name ?? "";
                    worksheet.Cells[row, 8].Value = evt.Status.ToString();

                    // Форматирование
                    if (row % 2 == 0)
                    {
                        worksheet.Cells[row, 1, row, 8].Style.Fill.PatternType = ExcelFillStyle.Solid;
                        worksheet.Cells[row, 1, row, 8].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(250, 255, 250));
                    }

                    row++;
                }

                worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
                package.SaveAs(new FileInfo(filePath));
            }
        }

        public static void ExportComprehensiveReport(List<Client> clients, List<Deal> deals, List<Expense> expenses, List<CalendarEvent> events, string filePath)
        {
            using var package = new ExcelPackage();

            // 1. Лист с клиентами
            var clientsWorksheet = package.Workbook.Worksheets.Add("Клиенты");
            CreateClientsSheet(clientsWorksheet, clients);

            // 2. Лист со сделками
            var dealsWorksheet = package.Workbook.Worksheets.Add("Сделки");
            CreateDealsSheet(dealsWorksheet, deals);

            // 3. Лист с расходами
            var expensesWorksheet = package.Workbook.Worksheets.Add("Расходы");
            CreateExpensesSheet(expensesWorksheet, expenses);

            // 4. Лист с событиями календаря
            var eventsWorksheet = package.Workbook.Worksheets.Add("Календарь");
            CreateEventsSheet(eventsWorksheet, events);

            // 5. Лист со сводной статистикой
            var summaryWorksheet = package.Workbook.Worksheets.Add("Сводка");
            CreateSummarySheet(summaryWorksheet, clients, deals, expenses, events);

            package.SaveAs(new FileInfo(filePath));
        }

        private static void CreateClientsSheet(ExcelWorksheet worksheet, List<Client> clients)
        {
            // Заголовок
            worksheet.Cells[1, 1].Value = "Клиенты CRM";
            worksheet.Cells[1, 1, 1, 7].Merge = true;
            worksheet.Cells[1, 1].Style.Font.Size = 16;
            worksheet.Cells[1, 1].Style.Font.Bold = true;
            worksheet.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            worksheet.Cells[1, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
            worksheet.Cells[1, 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(59, 130, 246));
            worksheet.Cells[1, 1].Style.Font.Color.SetColor(Color.White);

            worksheet.Cells[2, 1].Value = $"Дата экспорта: {DateTime.Now:dd.MM.yyyy HH:mm}";
            worksheet.Cells[2, 1, 2, 7].Merge = true;
            worksheet.Cells[2, 1].Style.Font.Italic = true;

            worksheet.Cells[3, 1].Value = $"Всего клиентов: {clients.Count}";
            worksheet.Cells[3, 1, 3, 7].Merge = true;
            worksheet.Cells[3, 1].Style.Font.Bold = true;

            // Заголовки столбцов
            string[] headers = { "ID", "ФИО", "Телефон", "Email", "Дата добавления", "Примечания", "ABC категория" };
            int headerRow = 5;
            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cells[headerRow, i + 1].Value = headers[i];
                worksheet.Cells[headerRow, i + 1].Style.Font.Bold = true;
                worksheet.Cells[headerRow, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                worksheet.Cells[headerRow, i + 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(226, 232, 240));
            }

            // Данные
            int row = headerRow + 1;
            foreach (var client in clients)
            {
                worksheet.Cells[row, 1].Value = client.Id;
                worksheet.Cells[row, 2].Value = client.Name;
                worksheet.Cells[row, 3].Value = client.Phone;
                worksheet.Cells[row, 4].Value = client.Email;
                worksheet.Cells[row, 5].Value = client.CreatedAt.ToString("dd.MM.yyyy HH:mm");
                worksheet.Cells[row, 6].Value = client.Notes;
                worksheet.Cells[row, 7].Value = client.ABC_Category ?? "";
                row++;
            }

            worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
        }

        private static void CreateDealsSheet(ExcelWorksheet worksheet, List<Deal> deals)
        {
            worksheet.Cells[1, 1].Value = "Сделки CRM";
            worksheet.Cells[1, 1, 1, 10].Merge = true;
            worksheet.Cells[1, 1].Style.Font.Size = 16;
            worksheet.Cells[1, 1].Style.Font.Bold = true;
            worksheet.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            worksheet.Cells[1, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
            worksheet.Cells[1, 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(34, 197, 94));
            worksheet.Cells[1, 1].Style.Font.Color.SetColor(Color.White);

            worksheet.Cells[2, 1].Value = $"Всего сделок: {deals.Count}";
            worksheet.Cells[2, 1, 2, 10].Merge = true;
            worksheet.Cells[2, 1].Style.Font.Bold = true;

            string[] headers = { "ID", "Название", "Клиент", "Сумма", "Статус", "Приоритет", "Вероятность", "Срок", "Дата создания", "Категория" };
            int headerRow = 4;
            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cells[headerRow, i + 1].Value = headers[i];
                worksheet.Cells[headerRow, i + 1].Style.Font.Bold = true;
                worksheet.Cells[headerRow, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                worksheet.Cells[headerRow, i + 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(220, 252, 231));
            }

            int row = headerRow + 1;
            foreach (var deal in deals)
            {
                worksheet.Cells[row, 1].Value = deal.Id;
                worksheet.Cells[row, 2].Value = deal.Title;
                worksheet.Cells[row, 3].Value = deal.Client?.Name ?? "Не указан";
                worksheet.Cells[row, 4].Value = deal.Amount;
                worksheet.Cells[row, 5].Value = deal.Status.ToString();
                worksheet.Cells[row, 6].Value = deal.Priority.ToString();
                worksheet.Cells[row, 7].Value = deal.Probability;
                worksheet.Cells[row, 8].Value = deal.Deadline.ToString("dd.MM.yyyy");
                worksheet.Cells[row, 9].Value = deal.CreatedAt.ToString("dd.MM.yyyy HH:mm");
                worksheet.Cells[row, 10].Value = deal.Category ?? "";
                row++;
            }

            worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
        }

        private static void CreateExpensesSheet(ExcelWorksheet worksheet, List<Expense> expenses)
        {
            worksheet.Cells[1, 1].Value = "Расходы CRM";
            worksheet.Cells[1, 1, 1, 6].Merge = true;
            worksheet.Cells[1, 1].Style.Font.Size = 16;
            worksheet.Cells[1, 1].Style.Font.Bold = true;
            worksheet.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            worksheet.Cells[1, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
            worksheet.Cells[1, 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(239, 68, 68));
            worksheet.Cells[1, 1].Style.Font.Color.SetColor(Color.White);

            worksheet.Cells[2, 1].Value = $"Всего расходов: {expenses.Count}";
            worksheet.Cells[2, 1, 2, 6].Merge = true;
            worksheet.Cells[2, 1].Style.Font.Bold = true;

            string[] headers = { "ID", "Дата", "Название", "Категория", "Сумма", "Примечания" };
            int headerRow = 4;
            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cells[headerRow, i + 1].Value = headers[i];
                worksheet.Cells[headerRow, i + 1].Style.Font.Bold = true;
                worksheet.Cells[headerRow, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                worksheet.Cells[headerRow, i + 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(254, 226, 226));
            }

            int row = headerRow + 1;
            foreach (var expense in expenses)
            {
                worksheet.Cells[row, 1].Value = expense.Id;
                worksheet.Cells[row, 2].Value = expense.Date.ToString("dd.MM.yyyy");
                worksheet.Cells[row, 3].Value = expense.Title;
                worksheet.Cells[row, 4].Value = expense.Category;
                worksheet.Cells[row, 5].Value = expense.Amount;
                worksheet.Cells[row, 6].Value = expense.Notes;
                row++;
            }

            worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
        }

        private static void CreateEventsSheet(ExcelWorksheet worksheet, List<CalendarEvent> events)
        {
            worksheet.Cells[1, 1].Value = "События календаря";
            worksheet.Cells[1, 1, 1, 8].Merge = true;
            worksheet.Cells[1, 1].Style.Font.Size = 16;
            worksheet.Cells[1, 1].Style.Font.Bold = true;
            worksheet.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            worksheet.Cells[1, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
            worksheet.Cells[1, 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(168, 85, 247));
            worksheet.Cells[1, 1].Style.Font.Color.SetColor(Color.White);

            worksheet.Cells[2, 1].Value = $"Всего событий: {events.Count}";
            worksheet.Cells[2, 1, 2, 8].Merge = true;
            worksheet.Cells[2, 1].Style.Font.Bold = true;

            string[] headers = { "ID", "Название", "Тип", "Дата начала", "Дата окончания", "Место", "Клиент", "Статус" };
            int headerRow = 4;
            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cells[headerRow, i + 1].Value = headers[i];
                worksheet.Cells[headerRow, i + 1].Style.Font.Bold = true;
                worksheet.Cells[headerRow, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                worksheet.Cells[headerRow, i + 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(237, 233, 254));
            }

            int row = headerRow + 1;
            foreach (var evt in events)
            {
                worksheet.Cells[row, 1].Value = evt.Id;
                worksheet.Cells[row, 2].Value = evt.Title;
                worksheet.Cells[row, 3].Value = evt.EventType.ToString();
                worksheet.Cells[row, 4].Value = evt.StartDate.ToString("dd.MM.yyyy HH:mm");
                worksheet.Cells[row, 5].Value = evt.EndDate.ToString("dd.MM.yyyy HH:mm");
                worksheet.Cells[row, 6].Value = evt.Location ?? "";
                worksheet.Cells[row, 7].Value = evt.Client?.Name ?? "";
                worksheet.Cells[row, 8].Value = evt.Status.ToString();
                row++;
            }

            worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
        }

        private static void CreateSummarySheet(ExcelWorksheet worksheet, List<Client> clients, List<Deal> deals, List<Expense> expenses, List<CalendarEvent> events)
        {
            worksheet.Cells[1, 1].Value = "Сводная статистика CRM";
            worksheet.Cells[1, 1, 1, 3].Merge = true;
            worksheet.Cells[1, 1].Style.Font.Size = 18;
            worksheet.Cells[1, 1].Style.Font.Bold = true;
            worksheet.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

            worksheet.Cells[2, 1].Value = $"Дата формирования: {DateTime.Now:dd.MM.yyyy HH:mm}";
            worksheet.Cells[2, 1, 2, 3].Merge = true;

            // Заголовки
            worksheet.Cells[4, 1].Value = "Показатель";
            worksheet.Cells[4, 2].Value = "Количество";
            worksheet.Cells[4, 3].Value = "Дополнительная информация";

            // Стиль заголовков
            worksheet.Cells[4, 1, 4, 3].Style.Font.Bold = true;
            worksheet.Cells[4, 1, 4, 3].Style.Fill.PatternType = ExcelFillStyle.Solid;
            worksheet.Cells[4, 1, 4, 3].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(226, 232, 240));

            int row = 5;

            // Клиенты
            worksheet.Cells[row, 1].Value = "Всего клиентов";
            worksheet.Cells[row, 2].Value = clients.Count;
            worksheet.Cells[row, 3].Value = $"Активных: {clients.Count(c => !string.IsNullOrEmpty(c.Phone) || !string.IsNullOrEmpty(c.Email))}";
            row++;

            // Сделки
            worksheet.Cells[row, 1].Value = "Всего сделок";
            worksheet.Cells[row, 2].Value = deals.Count;
            var successfulDeals = deals.Count(d => d.Status == DealStatus.Successful);
            worksheet.Cells[row, 3].Value = $"Успешных: {successfulDeals} ({(deals.Count > 0 ? successfulDeals * 100 / deals.Count : 0)}%)";
            row++;

            worksheet.Cells[row, 1].Value = "Общая сумма сделок";
            worksheet.Cells[row, 2].Value = $"{deals.Sum(d => d.Amount):N0} ₽";
            worksheet.Cells[row, 3].Value = $"Средняя: {(deals.Count > 0 ? deals.Average(d => d.Amount) : 0):N0} ₽";
            row++;

            // Расходы
            worksheet.Cells[row, 1].Value = "Всего расходов";
            worksheet.Cells[row, 2].Value = expenses.Count;
            worksheet.Cells[row, 3].Value = $"Общая сумма: {expenses.Sum(e => e.Amount):N0} ₽";
            row++;

            // События
            worksheet.Cells[row, 1].Value = "Событий календаря";
            worksheet.Cells[row, 2].Value = events.Count;
            var upcomingEvents = events.Count(e => e.StartDate >= DateTime.Now);
            worksheet.Cells[row, 3].Value = $"Предстоящих: {upcomingEvents}";
            row++;

            // Финансовые показатели
            row++;
            worksheet.Cells[row, 1].Value = "Финансовые показатели";
            worksheet.Cells[row, 1, row, 3].Merge = true;
            worksheet.Cells[row, 1].Style.Font.Bold = true;
            worksheet.Cells[row, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
            worksheet.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(220, 252, 231));
            row++;

            decimal totalRevenue = deals.Where(d => d.Status == DealStatus.Successful).Sum(d => d.Amount);
            decimal totalExpensesAmount = expenses.Sum(e => e.Amount);
            decimal netProfit = totalRevenue - totalExpensesAmount;

            worksheet.Cells[row, 1].Value = "Общая выручка";
            worksheet.Cells[row, 2].Value = $"{totalRevenue:N0} ₽";
            worksheet.Cells[row, 3].Value = "По успешным сделкам";
            row++;

            worksheet.Cells[row, 1].Value = "Чистая прибыль";
            worksheet.Cells[row, 2].Value = $"{netProfit:N0} ₽";
            worksheet.Cells[row, 3].Value = netProfit >= 0 ? "Прибыль" : "Убыток";
            worksheet.Cells[row, 2].Style.Font.Color.SetColor(netProfit >= 0 ? Color.Green : Color.Red);
            row++;

            worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
        }

    }
}