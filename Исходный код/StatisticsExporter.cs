using System;
using System.Drawing;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace MyFirstCRM
{
    public static class StatisticsExporter
    {
        public static void ExportStatistics(string filePath)
        {
            using (var context = MultiUserSecurityManager.CreateCompanyContext())
            {
                // Загружаем все необходимые данные в память
                var clients = context.Clients.ToList();
                var deals = context.Deals.Include(d => d.Client).ToList();
                var expenses = new System.Collections.Generic.List<Expense>();
                try
                {
                    expenses = context.Expenses.ToList();
                }
                catch
                {
                    expenses = new System.Collections.Generic.List<Expense>();
                }

                using (var package = new ExcelPackage())
                {
                    var worksheet = package.Workbook.Worksheets.Add("Статистика");

                    // Заголовок
                    worksheet.Cells[1, 1].Value = "СТАТИСТИКА CRM СИСТЕМЫ";
                    worksheet.Cells[1, 1, 1, 5].Merge = true;
                    worksheet.Cells[1, 1].Style.Font.Size = 16;
                    worksheet.Cells[1, 1].Style.Font.Bold = true;
                    worksheet.Cells[1, 1].Style.Font.Color.SetColor(Color.White);
                    worksheet.Cells[1, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    worksheet.Cells[1, 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(59, 130, 246));
                    worksheet.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                    worksheet.Cells[2, 1].Value = $"Дата экспорта: {DateTime.Now:dd.MM.yyyy HH:mm}";
                    worksheet.Cells[2, 1, 2, 5].Merge = true;
                    worksheet.Cells[2, 1].Style.Font.Italic = true;
                    worksheet.Cells[2, 1].Style.Font.Size = 10;

                    int row = 4;

                    // 1. ОСНОВНАЯ СТАТИСТИКА
                    worksheet.Cells[row, 1].Value = "1. ОСНОВНАЯ СТАТИСТИКА";
                    worksheet.Cells[row, 1, row, 5].Merge = true;
                    worksheet.Cells[row, 1].Style.Font.Size = 14;
                    worksheet.Cells[row, 1].Style.Font.Bold = true;
                    worksheet.Cells[row, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    worksheet.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(226, 232, 240));
                    row++;

                    AddStatistic(worksheet, ref row, "Всего клиентов", clients.Count.ToString());
                    AddStatistic(worksheet, ref row, "Всего сделок", deals.Count.ToString());

                    var successfulDeals = deals.Count(d => d.Status == DealStatus.Successful);
                    AddStatistic(worksheet, ref row, "Успешных сделок", successfulDeals.ToString());

                    var totalRevenue = deals.Where(d => d.Status == DealStatus.Successful).Sum(d => d.Amount);
                    AddStatistic(worksheet, ref row, "Общая выручка", $"{totalRevenue:N0} ₽");

                    var totalExpenses = expenses.Sum(e => e.Amount);
                    AddStatistic(worksheet, ref row, "Общие расходы", $"{totalExpenses:N0} ₽");

                    var netProfit = totalRevenue - totalExpenses;
                    AddStatistic(worksheet, ref row, "Чистая прибыль", $"{netProfit:N0} ₽");

                    var profitMargin = totalRevenue > 0 ? (netProfit / totalRevenue * 100) : 0;
                    AddStatistic(worksheet, ref row, "Рентабельность", $"{profitMargin:F1}%");

                    double successRate = deals.Count > 0 ? (double)successfulDeals / deals.Count * 100 : 0;
                    AddStatistic(worksheet, ref row, "Конверсия", $"{successRate:F1}%");

                    row += 2;

                    // 2. СТАТИСТИКА ПО СТАТУСАМ СДЕЛОК
                    worksheet.Cells[row, 1].Value = "2. СТАТИСТИКА ПО СТАТУСАМ СДЕЛОК";
                    worksheet.Cells[row, 1, row, 5].Merge = true;
                    worksheet.Cells[row, 1].Style.Font.Size = 14;
                    worksheet.Cells[row, 1].Style.Font.Bold = true;
                    worksheet.Cells[row, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    worksheet.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(226, 232, 240));
                    row++;

                    var statusGroups = deals
                        .GroupBy(d => d.Status)
                        .Select(g => new
                        {
                            Status = g.Key,
                            Count = g.Count(),
                            Amount = g.Where(d => d.Status == DealStatus.Successful).Sum(d => d.Amount)
                        })
                        .ToList();

                    foreach (var group in statusGroups)
                    {
                        AddStatistic(worksheet, ref row, group.Status.ToString(),
                                   $"{group.Count} сделок{(group.Status == DealStatus.Successful ? $" ({group.Amount:N0} ₽)" : "")}");
                    }

                    row += 2;

                    // 3. ТОП КЛИЕНТЫ
                    worksheet.Cells[row, 1].Value = "3. ТОП КЛИЕНТЫ ПО СДЕЛКАМ";
                    worksheet.Cells[row, 1, row, 5].Merge = true;
                    worksheet.Cells[row, 1].Style.Font.Size = 14;
                    worksheet.Cells[row, 1].Style.Font.Bold = true;
                    worksheet.Cells[row, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    worksheet.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(226, 232, 240));
                    row++;

                    var topClients = deals
                        .Where(d => d.Status == DealStatus.Successful && d.Client != null)
                        .GroupBy(d => d.Client)
                        .Select(g => new
                        {
                            Client = g.Key,
                            DealCount = g.Count(),
                            TotalAmount = g.Sum(d => d.Amount)
                        })
                        .OrderByDescending(x => x.TotalAmount)
                        .Take(10)
                        .ToList();

                    int rank = 1;
                    foreach (var client in topClients)
                    {
                        if (client.Client != null)
                        {
                            AddStatistic(worksheet, ref row, $"{rank}. {client.Client.Name}",
                                       $"{client.DealCount} сделок, {client.TotalAmount:N0} ₽");
                            rank++;
                        }
                    }

                    worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
                    package.SaveAs(new FileInfo(filePath));
                }
            }
        }

        private static void AddStatistic(ExcelWorksheet worksheet, ref int row, string name, string value)
        {
            worksheet.Cells[row, 1].Value = name;
            worksheet.Cells[row, 2].Value = value;

            worksheet.Cells[row, 1].Style.Font.Bold = true;
            worksheet.Cells[row, 2].Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;

            worksheet.Cells[row, 1, row, 2].Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            worksheet.Cells[row, 1, row, 2].Style.Border.Bottom.Color.SetColor(Color.LightGray);

            row++;
        }
    }
}