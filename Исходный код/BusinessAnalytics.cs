using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MyFirstCRM
{
    public static class BusinessAnalytics
    {
        public class BusinessInsights
        {
            public decimal MonthlyRevenue { get; set; }
            public decimal RevenueGrowth { get; set; }
            public decimal AverageDealValue { get; set; }
            public decimal ConversionRate { get; set; }
            public string BestMonth { get; set; }
            public string TopClient { get; set; }
            public decimal ClientRetentionRate { get; set; }
            public List<string> Recommendations { get; set; } = new List<string>();
        }

        public static BusinessInsights AnalyzeBusinessPerformance()
        {
            using (var context = MultiUserSecurityManager.CreateCompanyContext())
            {
                var insights = new BusinessInsights();
                var now = DateTime.Now;

                // Текущий месяц
                var currentMonthStart = new DateTime(now.Year, now.Month, 1);
                var currentMonthEnd = currentMonthStart.AddMonths(1).AddDays(-1);

                // Предыдущий месяц
                var prevMonthStart = currentMonthStart.AddMonths(-1);
                var prevMonthEnd = currentMonthStart.AddDays(-1);

                // Все успешные сделки - загружаем в память
                var allSuccessfulDeals = context.Deals
                    .Where(d => d.Status == DealStatus.Successful)
                    .Include(d => d.Client)
                    .ToList();

                // АНАЛИЗ ДОХОДОВ
                var currentMonthDeals = allSuccessfulDeals
                    .Where(d => d.CreatedAt >= currentMonthStart && d.CreatedAt <= currentMonthEnd)
                    .ToList();

                var prevMonthDeals = allSuccessfulDeals
                    .Where(d => d.CreatedAt >= prevMonthStart && d.CreatedAt <= prevMonthEnd)
                    .ToList();

                insights.MonthlyRevenue = currentMonthDeals.Sum(d => d.Amount);
                decimal prevMonthRevenue = prevMonthDeals.Sum(d => d.Amount);

                insights.RevenueGrowth = prevMonthRevenue > 0
                    ? ((insights.MonthlyRevenue - prevMonthRevenue) / prevMonthRevenue * 100)
                    : 0;
                // КОНВЕРСИЯ (используем единый метод)
                insights.ConversionRate = CalculateConversionRate(currentMonthStart, currentMonthEnd);

                // СРЕДНИЙ ЧЕК
                insights.AverageDealValue = currentMonthDeals.Any()
                    ? currentMonthDeals.Average(d => d.Amount)
                    : 0;

                // КОНВЕРСИЯ - загружаем сделки в память
                var allCurrentMonthDeals = context.Deals
                    .Where(d => d.CreatedAt >= currentMonthStart && d.CreatedAt <= currentMonthEnd)
                    .ToList();

                int successfulCount = allCurrentMonthDeals.Count(d => d.Status == DealStatus.Successful);
                int totalCount = allCurrentMonthDeals.Count;

                insights.ConversionRate = totalCount > 0 ? (successfulCount * 100.0m / totalCount) : 0;

                // ЛУЧШИЙ МЕСЯЦ
                var monthlyRevenue = allSuccessfulDeals
                    .GroupBy(d => new { d.CreatedAt.Year, d.CreatedAt.Month })
                    .Select(g => new
                    {
                        Month = g.Key,
                        Revenue = g.Sum(d => d.Amount),
                        MonthName = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMMM yyyy")
                    })
                    .OrderByDescending(x => x.Revenue)
                    .FirstOrDefault();

                insights.BestMonth = monthlyRevenue != null ? $"{monthlyRevenue.MonthName} ({monthlyRevenue.Revenue:N0} ₽)" : "Нет данных";

                // ЛУЧШИЙ КЛИЕНТ
                var topClient = allSuccessfulDeals
                    .GroupBy(d => d.Client)
                    .Select(g => new
                    {
                        Client = g.Key,
                        Total = g.Sum(d => d.Amount),
                        Count = g.Count()
                    })
                    .OrderByDescending(x => x.Total)
                    .FirstOrDefault();

                insights.TopClient = topClient != null ? $"{topClient.Client?.Name} ({topClient.Total:N0} ₽, {topClient.Count} сделок)" : "Нет данных";

                // УДЕРЖАНИЕ КЛИЕНТОВ
                var allClientsWithDeals = allSuccessfulDeals
                    .Select(d => d.ClientId)
                    .Distinct()
                    .ToList();

                var returningClients = allSuccessfulDeals
                    .Where(d => d.CreatedAt < prevMonthStart)
                    .Select(d => d.ClientId)
                    .Distinct()
                    .Count(id => allSuccessfulDeals.Any(d => d.ClientId == id && d.CreatedAt >= currentMonthStart));

                insights.ClientRetentionRate = allClientsWithDeals.Any()
                    ? (returningClients * 100.0m / allClientsWithDeals.Count)
                    : 0;

                // РЕКОМЕНДАЦИИ
                insights.Recommendations = GenerateRecommendations(insights);

                return insights;
            }
        }

        private static List<string> GenerateRecommendations(BusinessInsights insights)
        {
            var recommendations = new List<string>();

            if (insights.RevenueGrowth < 10 && insights.MonthlyRevenue > 0)
                recommendations.Add("📈 Рост выручки замедлился. Рассмотрите новые каналы продаж");

            if (insights.AverageDealValue < 50000)
                recommendations.Add("💼 Средний чек низкий. Предлагайте дополнительные услуги");

            if (insights.ConversionRate < 30)
                recommendations.Add("🎯 Низкая конверсия. Улучшите воронку продаж");

            if (insights.ClientRetentionRate < 50)
                recommendations.Add("🤝 Много клиентов не возвращаются. Улучшите сервис");

            if (recommendations.Count == 0)
                recommendations.Add("✅ Бизнес работает стабильно! Продолжайте в том же духе");

            return recommendations;
        }

        public static string GenerateAnalyticsReport()
        {
            var insights = AnalyzeBusinessPerformance();
            var sb = new StringBuilder();

            var startDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var endDate = DateTime.Now;
            var profitReport = CalculateProfit(startDate, endDate);

            sb.AppendLine("🔍 БИЗНЕС-АНАЛИТИКА");
            sb.AppendLine($"Период: {DateTime.Now:MMMM yyyy}");
            sb.AppendLine("=".PadRight(60, '='));
            sb.AppendLine();

            sb.AppendLine("📊 КЛЮЧЕВЫЕ МЕТРИКИ:");
            sb.AppendLine($"• Выручка за месяц: {insights.MonthlyRevenue:N0} ₽");
            sb.AppendLine($"• Расходы за месяц: {profitReport.TotalExpenses:N0} ₽");
            sb.AppendLine($"• Чистая прибыль: {profitReport.NetProfit:N0} ₽");
            sb.AppendLine($"• Рентабельность: {profitReport.ProfitMargin:F1}%");
            sb.AppendLine($"• Рост выручки: {insights.RevenueGrowth:+#;-#;0}%");
            sb.AppendLine($"• Средний чек: {insights.AverageDealValue:N0} ₽");
            sb.AppendLine($"• Конверсия: {insights.ConversionRate:F1}%");
            sb.AppendLine($"• Удержание клиентов: {insights.ClientRetentionRate:F1}%");
            sb.AppendLine();

            sb.AppendLine("🏆 ЛУЧШИЕ ПОКАЗАТЕЛИ:");
            sb.AppendLine($"• Лучший месяц: {insights.BestMonth}");
            sb.AppendLine($"• Лучший клиент: {insights.TopClient}");
            sb.AppendLine();

            sb.AppendLine("💡 РЕКОМЕНДАЦИИ:");
            foreach (var rec in insights.Recommendations)
                sb.AppendLine($"• {rec}");

            sb.AppendLine();
            sb.AppendLine("📈 ТРЕНДЫ:");
            sb.AppendLine(GenerateTrendsReport());

            sb.AppendLine();
            sb.AppendLine($"Сформировано: {DateTime.Now:dd.MM.yyyy HH:mm}");

            return sb.ToString();
        }

        private static string GenerateTrendsReport()
        {
            using (var context = MultiUserSecurityManager.CreateCompanyContext())
            {
                var last6Months = Enumerable.Range(0, 6)
                    .Select(i => DateTime.Now.AddMonths(-i))
                    .Select(d => new { Year = d.Year, Month = d.Month })
                    .Reverse()
                    .ToList();

                // Загружаем данные в память перед агрегацией
                var deals = context.Deals
                    .Where(d => d.Status == DealStatus.Successful)
                    .ToList();

                var monthlyData = deals
                    .GroupBy(d => new { d.CreatedAt.Year, d.CreatedAt.Month })
                    .Select(g => new
                    {
                        g.Key.Year,
                        g.Key.Month,
                        Revenue = g.Sum(d => d.Amount),
                        Count = g.Count()
                    })
                    .ToList();

                var sb = new StringBuilder();

                foreach (var month in last6Months)
                {
                    var data = monthlyData.FirstOrDefault(d => d.Year == month.Year && d.Month == month.Month);
                    string monthName = new DateTime(month.Year, month.Month, 1).ToString("MMM yy");

                    if (data != null)
                    {
                        sb.AppendLine($"  {monthName}: {data.Revenue:N0} ₽ ({data.Count} сделок)");
                    }
                    else
                    {
                        sb.AppendLine($"  {monthName}: Нет данных");
                    }
                }

                return sb.ToString();
            }
        }

        // Класс для отчета по прибыли и расходам
        public class ProfitReport
        {
            public decimal TotalRevenue { get; set; }
            public decimal TotalExpenses { get; set; }
            public decimal NetProfit { get; set; }
            public decimal ProfitMargin { get; set; }
            public Dictionary<string, decimal> ExpenseByCategory { get; set; } = new Dictionary<string, decimal>();
            public List<KeyValuePair<string, decimal>> TopExpenses { get; set; } = new List<KeyValuePair<string, decimal>>();
        }

        // Метод для расчета прибыли
        public static ProfitReport CalculateProfit(DateTime startDate, DateTime endDate)
        {
            using (var context = MultiUserSecurityManager.CreateCompanyContext())
            {
                var report = new ProfitReport();

                try
                {
                    // Доходы (успешные сделки) - загружаем в память
                    var successfulDeals = context.Deals
                        .Where(d => d.Status == DealStatus.Successful &&
                                   d.CreatedAt >= startDate && d.CreatedAt <= endDate)
                        .ToList();

                    report.TotalRevenue = successfulDeals.Sum(d => d.Amount);

                    // Расходы (если есть таблица Expenses)
                    decimal totalExpenses = 0;
                    List<Expense> expenses = new List<Expense>();

                    try
                    {
                        // Проверяем, существует ли таблица Expenses
                        var tableExists = context.Database.CanConnect() &&
                                         context.Model.GetEntityTypes().Any(e => e.ClrType == typeof(Expense));

                        if (tableExists)
                        {
                            expenses = context.Expenses
                                .Where(e => e.Date >= startDate && e.Date <= endDate)
                                .ToList();

                            totalExpenses = expenses.Sum(e => e.Amount);
                        }
                    }
                    catch
                    {
                        // Таблица расходов еще не создана
                        totalExpenses = 0;
                    }

                    report.TotalExpenses = totalExpenses;
                    report.NetProfit = report.TotalRevenue - report.TotalExpenses;
                    report.ProfitMargin = report.TotalRevenue > 0 ?
                        (report.NetProfit / report.TotalRevenue * 100) : 0;

                    // Расходы по категориям
                    if (expenses.Any())
                    {
                        report.ExpenseByCategory = expenses
                            .GroupBy(e => e.Category)
                            .ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));

                        // Топ расходов
                        report.TopExpenses = expenses
                            .OrderByDescending(e => e.Amount)
                            .Take(5)
                            .Select(e => new KeyValuePair<string, decimal>($"{e.Title} ({e.Category})", e.Amount))
                            .ToList();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка расчета прибыли: {ex.Message}");
                }

                return report;
            }
        }

        // Метод для генерации отчета по прибыли
        public static string GenerateProfitReport(DateTime startDate, DateTime endDate)
        {
            var report = CalculateProfit(startDate, endDate);
            var sb = new StringBuilder();

            sb.AppendLine("💰 ОТЧЕТ О ПРИБЫЛИ И РАСХОДАХ");
            sb.AppendLine($"Период: {startDate:dd.MM.yyyy} - {endDate:dd.MM.yyyy}");
            sb.AppendLine("=".PadRight(60, '='));
            sb.AppendLine();

            sb.AppendLine("📊 ФИНАНСОВЫЕ ПОКАЗАТЕЛИ:");
            sb.AppendLine($"• Выручка: {report.TotalRevenue:N0} ₽");
            sb.AppendLine($"• Расходы: {report.TotalExpenses:N0} ₽");
            sb.AppendLine($"• Чистая прибыль: {report.NetProfit:N0} ₽");
            sb.AppendLine($"• Рентабельность: {report.ProfitMargin:F1}%");
            sb.AppendLine();

            if (report.NetProfit < 0)
                sb.AppendLine("⚠️ ВНИМАНИЕ: Убыток! Необходимо снизить расходы или увеличить доходы");

            if (report.ExpenseByCategory.Any())
            {
                sb.AppendLine("📉 СТРУКТУРА РАСХОДОВ:");
                foreach (var category in report.ExpenseByCategory.OrderByDescending(x => x.Value))
                {
                    var percentage = report.TotalExpenses > 0 ? (category.Value / report.TotalExpenses * 100) : 0;
                    sb.AppendLine($"  {category.Key}: {category.Value:N0} ₽ ({percentage:F1}%)");
                }

                if (report.TopExpenses.Any())
                {
                    sb.AppendLine();
                    sb.AppendLine("📌 КРУПНЕЙШИЕ РАСХОДЫ:");
                    foreach (var expense in report.TopExpenses)
                    {
                        sb.AppendLine($"  • {expense.Key}: {expense.Value:N0} ₽");
                    }
                }
            }
            else
            {
                sb.AppendLine("📉 Расходы: Данные о расходах не добавлены в систему");
                sb.AppendLine("  Для учета расходов используйте раздел 'Финансы'");
            }

            // Рекомендации
            sb.AppendLine();
            sb.AppendLine("💡 РЕКОМЕНДАЦИИ:");

            if (report.ProfitMargin < 10 && report.TotalRevenue > 0)
                sb.AppendLine("• Низкая рентабельность. Оптимизируйте расходы");

            if (report.TotalExpenses > report.TotalRevenue * 0.7m && report.TotalRevenue > 0)
                sb.AppendLine("• Высокая доля расходов. Найдите способы сократить затраты");

            if (report.ExpenseByCategory.ContainsKey("Маркетинг") &&
                report.ExpenseByCategory["Маркетинг"] > report.TotalRevenue * 0.3m && report.TotalRevenue > 0)
                sb.AppendLine("• Высокие затраты на маркетинг. Оцените эффективность рекламы");

            if (report.TotalRevenue == 0 && report.TotalExpenses == 0)
                sb.AppendLine("• Нет данных за выбранный период. Начните добавлять сделки и расходы");

            sb.AppendLine();
            sb.AppendLine($"Сформировано: {DateTime.Now:dd.MM.yyyy HH:mm}");

            return sb.ToString();
        }

        // Дополнительный метод для быстрой оценки прибыльности
        public static string GetQuickProfitSummary()
        {
            var startDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var endDate = DateTime.Now;
            var report = CalculateProfit(startDate, endDate);

            var sb = new StringBuilder();
            sb.AppendLine($"💰 Прибыль за месяц: {report.NetProfit:N0} ₽");

            if (report.ProfitMargin >= 20)
                sb.AppendLine($"✅ Отличная рентабельность: {report.ProfitMargin:F1}%");
            else if (report.ProfitMargin >= 10)
                sb.AppendLine($"⚠️ Средняя рентабельность: {report.ProfitMargin:F1}%");
            else if (report.ProfitMargin > 0)
                sb.AppendLine($"❌ Низкая рентабельность: {report.ProfitMargin:F1}%");
            else if (report.NetProfit < 0)
                sb.AppendLine($"🔥 УБЫТОК! Нужны срочные меры");

            return sb.ToString();
        }
        // Единый метод для расчета конверсии
        public static decimal CalculateConversionRate(DateTime? startDate = null, DateTime? endDate = null)
        {
            using (var context = MultiUserSecurityManager.CreateCompanyContext())
            {
                var query = context.Deals.AsQueryable();

                if (startDate.HasValue)
                    query = query.Where(d => d.CreatedAt >= startDate.Value);

                if (endDate.HasValue)
                    query = query.Where(d => d.CreatedAt <= endDate.Value);

                var deals = query.ToList();
                int successfulCount = deals.Count(d => d.Status == DealStatus.Successful);
                int totalCount = deals.Count;

                return totalCount > 0 ? (successfulCount * 100.0m / totalCount) : 0;
            }
        }
        // Метод для сравнения с предыдущим периодом
        public static string GetPeriodComparison(DateTime currentStart, DateTime currentEnd)
        {
            var previousStart = currentStart.AddMonths(-1);
            var previousEnd = currentEnd.AddMonths(-1);

            var currentReport = CalculateProfit(currentStart, currentEnd);
            var previousReport = CalculateProfit(previousStart, previousEnd);

            var sb = new StringBuilder();
            sb.AppendLine("📊 СРАВНЕНИЕ С ПРЕДЫДУЩИМ МЕСЯЦЕМ:");
            sb.AppendLine();

            // Сравнение выручки
            decimal revenueChange = currentReport.TotalRevenue - previousReport.TotalRevenue;
            decimal revenueChangePercent = previousReport.TotalRevenue > 0 ?
                (revenueChange / previousReport.TotalRevenue * 100) : 0;

            sb.AppendLine($"Выручка: {currentReport.TotalRevenue:N0} ₽ " +
                         $"({revenueChange:+#;-#;0} ₽, {revenueChangePercent:+#;-#;0}%)");

            // Сравнение прибыли
            decimal profitChange = currentReport.NetProfit - previousReport.NetProfit;
            decimal profitChangePercent = Math.Abs(previousReport.NetProfit) > 0 ?
                (profitChange / Math.Abs(previousReport.NetProfit) * 100) : 0;

            sb.AppendLine($"Прибыль: {currentReport.NetProfit:N0} ₽ " +
                         $"({profitChange:+#;-#;0} ₽, {profitChangePercent:+#;-#;0}%)");

            // Сравнение рентабельности
            decimal marginChange = currentReport.ProfitMargin - previousReport.ProfitMargin;
            sb.AppendLine($"Рентабельность: {currentReport.ProfitMargin:F1}% " +
                         $"({marginChange:+#;-#;0} п.п.)");

            return sb.ToString();
        }
    }
}