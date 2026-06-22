using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MyFirstCRM
{
    public static class AdvancedAnalytics
    {
        // 1. RFM АНАЛИЗ КЛИЕНТОВ (Recency, Frequency, Monetary)
        public class RFMAnalysis
        {
            public class RFMClient
            {
                public string ClientName { get; set; }
                public int RecencyDays { get; set; } // Дней с последней сделки
                public int Frequency { get; set; }   // Количество сделок
                public decimal Monetary { get; set; } // Общая сумма покупок
                public string Segment { get; set; }  // Сегмент клиента
                public int RFMScore { get; set; }    // Общий балл (1-12)
            }

            public List<RFMClient> TopClients { get; set; } = new List<RFMClient>();
            public Dictionary<string, int> SegmentDistribution { get; set; } = new Dictionary<string, int>();
            public decimal TotalPotentialRevenue { get; set; }
        }

        public static RFMAnalysis PerformRFMAnalysis(DateTime startDate, DateTime endDate)
        {
            using (var context = MultiUserSecurityManager.CreateCompanyContext())
            {
                var analysis = new RFMAnalysis();

                var successfulDeals = context.Deals
                    .Include(d => d.Client)
                    .Where(d => d.Status == DealStatus.Successful &&
                               d.CreatedAt >= startDate && d.CreatedAt <= endDate)
                    .ToList();

                // Группируем по клиентам
                var clientGroups = successfulDeals
                    .GroupBy(d => d.Client)
                    .Where(g => g.Key != null)
                    .Select(g => new
                    {
                        Client = g.Key,
                        LastDealDate = g.Max(d => d.CreatedAt),
                        Frequency = g.Count(),
                        Monetary = g.Sum(d => d.Amount),
                        AllDeals = g.ToList()
                    })
                    .ToList();

                var rfmClients = new List<RFMAnalysis.RFMClient>();

                foreach (var group in clientGroups)
                {
                    int recencyDays = (DateTime.Now - group.LastDealDate).Days;

                    // Расчет RFM баллов (1-4, где 4 - лучший)
                    int rScore = recencyDays <= 30 ? 4 :
                                 recencyDays <= 90 ? 3 :
                                 recencyDays <= 180 ? 2 : 1;

                    int fScore = group.Frequency >= 10 ? 4 :
                                 group.Frequency >= 5 ? 3 :
                                 group.Frequency >= 2 ? 2 : 1;

                    decimal avgDealValue = group.Monetary / group.Frequency;
                    int mScore = avgDealValue >= 100000 ? 4 :
                                 avgDealValue >= 50000 ? 3 :
                                 avgDealValue >= 20000 ? 2 : 1;

                    int totalScore = rScore + fScore + mScore;
                    string segment = GetRFMSegment(rScore, fScore, mScore);

                    rfmClients.Add(new RFMAnalysis.RFMClient
                    {
                        ClientName = group.Client.Name,
                        RecencyDays = recencyDays,
                        Frequency = group.Frequency,
                        Monetary = group.Monetary,
                        Segment = segment,
                        RFMScore = totalScore
                    });
                }

                // Сортируем по RFM баллу
                analysis.TopClients = rfmClients
                    .OrderByDescending(c => c.RFMScore)
                    .Take(20)
                    .ToList();

                // Распределение по сегментам
                analysis.SegmentDistribution = rfmClients
                    .GroupBy(c => c.Segment)
                    .ToDictionary(g => g.Key, g => g.Count());

                // Потенциальная выручка (предполагаем, что клиенты с высоким RFM могут купить еще)
                analysis.TotalPotentialRevenue = analysis.TopClients
                    .Where(c => c.RFMScore >= 8)
                    .Sum(c => c.Monetary * 0.3m); // 30% от их текущих трат

                return analysis;
            }
        }

        private static string GetRFMSegment(int r, int f, int m)
        {
            if (r == 4 && f == 4 && m == 4) return "VIP Клиенты";
            if (r >= 3 && f >= 3 && m >= 3) return "Лояльные";
            if (r >= 3 && f <= 2) return "Новые";
            if (r <= 2 && f >= 3) return "Спящие";
            if (m >= 3) return "Крупные покупатели";
            return "Стандартные";
        }

        // 2. ПРОГНОЗ ДОХОДОВ НА СЛЕДУЮЩИЙ МЕСЯЦ (улучшенный)
        public class RevenueForecast
        {
            public decimal NextMonthForecast { get; set; }
            public decimal GrowthPercentage { get; set; }
            public decimal MinForecast { get; set; }
            public decimal MaxForecast { get; set; }
            public List<KeyValuePair<string, decimal>> Factors { get; set; } = new List<KeyValuePair<string, decimal>>();
            public string ForecastConfidence { get; set; }
            public List<string> Insights { get; set; } = new List<string>();
            public List<string> Recommendations { get; set; } = new List<string>();
        }

        public static RevenueForecast ForecastNextMonthRevenue()
        {
            try
            {
                using (var context = MultiUserSecurityManager.CreateCompanyContext())
                {
                    var forecast = new RevenueForecast();

                    var now = DateTime.Now;
                    var twelveMonthsAgo = now.AddMonths(-12);

                    // Получаем данные за последние 12 месяцев
                    var monthlyData = context.Deals
                        .Where(d => d.Status == DealStatus.Successful &&
                                   d.CreatedAt >= twelveMonthsAgo)
                        .AsEnumerable()
                        .GroupBy(d => new { d.CreatedAt.Year, d.CreatedAt.Month })
                        .Select(g => new
                        {
                            Year = g.Key.Year,
                            Month = g.Key.Month,
                            Revenue = g.Sum(d => d.Amount),
                            DealCount = g.Count(),
                            AvgDealValue = g.Average(d => d.Amount)
                        })
                        .OrderBy(x => x.Year).ThenBy(x => x.Month)
                        .ToList();

                    if (monthlyData.Count >= 3)
                    {
                        // 1. Анализ трендов и сезонности
                        var trendAnalysis = AnalyzeTrends(monthlyData.Cast<object>().ToList());
                        var seasonalityAnalysis = AnalyzeSeasonality(monthlyData.Cast<object>().ToList());
                        
                        // 2. Базовый прогноз на основе трендов
                        decimal lastMonthRevenue = monthlyData.Last().Revenue;
                        decimal baseForecast = lastMonthRevenue * (1 + trendAnalysis.GrowthRate);

                        // 3. Корректировка с учетом сезонности
                        var nextMonth = now.AddMonths(1);
                        var seasonalFactor = GetSeasonalFactor(monthlyData.Cast<object>().ToList(), nextMonth.Month);
                        baseForecast *= seasonalFactor;

                        // 4. Учет текущих факторов
                        var currentFactors = AnalyzeCurrentFactors(context);
                        
                        // 5. Итоговый прогноз
                        forecast.NextMonthForecast = baseForecast * (1 + currentFactors.Adjustment);
                        forecast.GrowthPercentage = trendAnalysis.GrowthRate * 100;
                        
                        // 6. Расчет доверительного интервала
                        var volatility = CalculateVolatility(monthlyData.Cast<object>().ToList());
                        forecast.MinForecast = forecast.NextMonthForecast * (1 - volatility);
                        forecast.MaxForecast = forecast.NextMonthForecast * (1 + volatility);

                        // 7. Факторы, влияющие на прогноз
                        forecast.Factors.Add(new KeyValuePair<string, decimal>(
                            "Исторический рост", trendAnalysis.GrowthRate * 100));
                        forecast.Factors.Add(new KeyValuePair<string, decimal>(
                            "Сезонность", (seasonalFactor - 1) * 100));
                        forecast.Factors.Add(new KeyValuePair<string, decimal>(
                            "Текущая воронка", currentFactors.PipelineValue));
                        forecast.Factors.Add(new KeyValuePair<string, decimal>(
                            "Активность продаж", currentFactors.ActivityLevel));

                        // 8. Генерация инсайтов
                        forecast.Insights.AddRange(GenerateForecastInsights(
                            monthlyData.Cast<object>().ToList(), trendAnalysis, seasonalityAnalysis, currentFactors));

                        // 9. Рекомендации
                        forecast.Recommendations.AddRange(GenerateForecastRecommendations(
                            forecast, currentFactors));

                        // 10. Уверенность в прогнозе
                        forecast.ForecastConfidence = CalculateForecastConfidence(
                            monthlyData.Count, volatility, currentFactors.DataQuality);
                    }
                    else
                    {
                        // Недостаточно данных - упрощенный прогноз
                        var currentMonthDeals = context.Deals
                            .Where(d => d.Status == DealStatus.Successful &&
                                       d.CreatedAt >= new DateTime(now.Year, now.Month, 1))
                            .ToList();

                        if (currentMonthDeals.Any())
                        {
                            forecast.NextMonthForecast = currentMonthDeals.Sum(d => d.Amount);
                            forecast.GrowthPercentage = 0;
                            forecast.MinForecast = forecast.NextMonthForecast * 0.7m;
                            forecast.MaxForecast = forecast.NextMonthForecast * 1.3m;
                            forecast.ForecastConfidence = "Низкая";
                            forecast.Insights.Add("Недостаточно исторических данных для точного прогноза");
                            forecast.Recommendations.Add("Накопите больше данных за несколько месяцев");
                        }
                        else
                        {
                            forecast.NextMonthForecast = 0;
                            forecast.GrowthPercentage = 0;
                            forecast.ForecastConfidence = "Нет данных";
                            forecast.Insights.Add("Отсутствуют данные о сделках");
                            forecast.Recommendations.Add("Начните добавлять сделки в систему");
                        }
                    }

                    return forecast;
                }
            }
            catch (Exception ex)
            {
                return new RevenueForecast
                {
                    NextMonthForecast = 0,
                    GrowthPercentage = 0,
                    ForecastConfidence = "Ошибка",
                    Insights = new List<string> { $"Ошибка при расчете прогноза: {ex.Message}" },
                    Recommendations = new List<string> { "Проверьте данные и повторите попытку" }
                };
            }
        }

        private static TrendAnalysis AnalyzeTrends(List<object> monthlyData)
        {
            var revenues = monthlyData.Select(x => (decimal)((dynamic)x).Revenue).ToList();
            var n = revenues.Count;
            
            // Простая линейная регрессия для определения тренда
            decimal sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
            
            for (int i = 0; i < n; i++)
            {
                sumX += i;
                sumY += revenues[i];
                sumXY += i * revenues[i];
                sumX2 += i * i;
            }
            
            decimal slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
            decimal avgRevenue = sumY / n;
            decimal growthRate = n > 1 ? slope / avgRevenue : 0;
            
            return new TrendAnalysis
            {
                GrowthRate = growthRate,
                Slope = slope,
                AverageRevenue = avgRevenue
            };
        }

        private static SeasonalityAnalysis AnalyzeSeasonality(List<object> monthlyData)
        {
            var monthlyAvg = monthlyData
                .GroupBy(x => ((dynamic)x).Month)
                .ToDictionary(
                    g => g.Key,
                    g => g.Average(x => (decimal)((dynamic)x).Revenue)
                );
            
            var overallAvg = monthlyData.Average(x => (decimal)((dynamic)x).Revenue);
            
            return new SeasonalityAnalysis
            {
                MonthlyFactors = monthlyAvg.ToDictionary(
                    kvp => (int)kvp.Key,
                    kvp => kvp.Value / overallAvg
                ),
                OverallAverage = overallAvg
            };
        }

        private static decimal GetSeasonalFactor(List<object> monthlyData, int month)
        {
            var seasonality = AnalyzeSeasonality(monthlyData);
            return seasonality.MonthlyFactors.ContainsKey(month) 
                ? seasonality.MonthlyFactors[month] 
                : 1.0m;
        }

        private static CurrentFactors AnalyzeCurrentFactors(AppDbContext context)
        {
            var now = DateTime.Now;
            
            // Текущая воронка продаж
            var pipelineValue = context.Deals
                .Where(d => d.Status == DealStatus.InProgress)
                .Select(d => new { d.Amount, d.Probability })
                .ToList()
                .Sum(d => d.Amount * d.Probability / 100m);

            // Активность за последние 30 дней
            var recentActivity = context.Deals
                .Count(d => d.CreatedAt >= now.AddDays(-30));

            // Новые клиенты за последний месяц
            var newClients = context.Clients
                .Count(c => c.CreatedAt >= now.AddDays(-30));

            // Средняя вероятность сделок в работе
            var avgProbability = context.Deals
                .Where(d => d.Status == DealStatus.InProgress)
                .DefaultIfEmpty()
                .Average(d => d != null ? (decimal)d.Probability : 0m);

            // Расчет корректировки
            decimal adjustment = 0;
            if (pipelineValue > 100000) adjustment += 0.1m;
            if (recentActivity > 10) adjustment += 0.05m;
            if (newClients > 5) adjustment += 0.03m;
            if (avgProbability > 60) adjustment += 0.05m;

            return new CurrentFactors
            {
                PipelineValue = pipelineValue,
                ActivityLevel = recentActivity,
                NewClientsCount = newClients,
                AverageProbability = avgProbability,
                Adjustment = adjustment,
                DataQuality = CalculateDataQuality(context)
            };
        }

        private static decimal CalculateVolatility(List<object> monthlyData)
        {
            var revenues = monthlyData.Select(x => (decimal)((dynamic)x).Revenue).ToList();
            var mean = revenues.Average();
            var variance = revenues.Sum(r => Math.Pow((double)(r - mean), 2)) / revenues.Count;
            var stdDev = Math.Sqrt(variance);
            
            return mean > 0 ? (decimal)(stdDev / (double)mean) : 0.2m; // 20% по умолчанию
        }

        private static List<string> GenerateForecastInsights(
            List<object> monthlyData, 
            TrendAnalysis trend, 
            SeasonalityAnalysis seasonality,
            CurrentFactors current)
        {
            var insights = new List<string>();
            
            if (trend.GrowthRate > 0.1m)
                insights.Add($"✅ Сильный рост выручки: {trend.GrowthRate * 100:F1}% в месяц");
            else if (trend.GrowthRate < -0.05m)
                insights.Add($"⚠️ Нисходящий тренд: {trend.GrowthRate * 100:F1}% в месяц");
            else
                insights.Add("📊 Стабильная динамика выручки");

            if (current.PipelineValue > 50000)
                insights.Add($"💰 Крупная воронка продаж: {current.PipelineValue:N0} ₽");
            
            if (current.ActivityLevel > 15)
                insights.Add($"🚀 Высокая активность: {current.ActivityLevel} сделок за месяц");

            var bestMonth = seasonality.MonthlyFactors
                .OrderByDescending(kvp => kvp.Value)
                .FirstOrDefault();
            
            if (bestMonth.Value > 1.2m)
            {
                var monthName = new DateTime(DateTime.Now.Year, bestMonth.Key, 1).ToString("MMMM");
                insights.Add($"📅 Пиковый месяц: {monthName} (в {(bestMonth.Value - 1) * 100:F0}% выше среднего)");
            }

            return insights;
        }

        private static List<string> GenerateForecastRecommendations(RevenueForecast forecast, CurrentFactors current)
        {
            var recommendations = new List<string>();
            
            if (forecast.GrowthPercentage < 5)
            {
                if (current.PipelineValue < 50000)
                    recommendations.Add("🎯 Увеличьте воронку продаж для роста выручки");
                if (current.ActivityLevel < 10)
                    recommendations.Add("📞 Повысьте активность продаж - больше звонков и встреч");
            }
            
            if (forecast.GrowthPercentage > 20)
            {
                recommendations.Add("📈 Отличный рост! Подготовьтесь к увеличению нагрузки");
                recommendations.Add("💼 Рассмотрите найм дополнительных сотрудников");
            }
            
            if (current.AverageProbability < 40)
                recommendations.Add("🎯 Работайте над повышением вероятности закрытия сделок");
            
            if (forecast.ForecastConfidence == "Низкая")
                recommendations.Add("📊 Накопите больше данных для более точных прогнозов");

            return recommendations;
        }

        private static string CalculateForecastConfidence(int dataPoints, decimal volatility, decimal dataQuality)
        {
            var confidenceScore = 0;
            
            // Качество данных
            if (dataPoints >= 12) confidenceScore += 40;
            else if (dataPoints >= 6) confidenceScore += 25;
            else if (dataPoints >= 3) confidenceScore += 15;
            
            // Волатильность
            if (volatility < 0.1m) confidenceScore += 30;
            else if (volatility < 0.2m) confidenceScore += 20;
            else if (volatility < 0.3m) confidenceScore += 10;
            
            // Полнота данных
            confidenceScore += (int)(dataQuality * 30);
            
            if (confidenceScore >= 80) return "Высокая";
            if (confidenceScore >= 60) return "Средняя";
            if (confidenceScore >= 40) return "Низкая";
            return "Очень низкая";
        }

        private static decimal CalculateDataQuality(AppDbContext context)
        {
            var totalDeals = context.Deals.Count();
            var dealsWithAmount = context.Deals.Count(d => d.Amount > 0);
            var dealsWithClient = context.Deals.Count(d => d.ClientId > 0);
            var dealsWithCategory = context.Deals.Count(d => !string.IsNullOrEmpty(d.Category));
            
            if (totalDeals == 0) return 0;
            
            return (dealsWithAmount + dealsWithClient + dealsWithCategory) / (3.0m * totalDeals);
        }

        // Вспомогательные классы
        private class TrendAnalysis
        {
            public decimal GrowthRate { get; set; }
            public decimal Slope { get; set; }
            public decimal AverageRevenue { get; set; }
        }

        private class SeasonalityAnalysis
        {
            public Dictionary<int, decimal> MonthlyFactors { get; set; } = new Dictionary<int, decimal>();
            public decimal OverallAverage { get; set; }
        }

        private class CurrentFactors
        {
            public decimal PipelineValue { get; set; }
            public int ActivityLevel { get; set; }
            public int NewClientsCount { get; set; }
            public decimal AverageProbability { get; set; }
            public decimal Adjustment { get; set; }
            public decimal DataQuality { get; set; }
        }

        // 3. АНАЛИЗ ЭФФЕКТИВНОСТИ ПО ВРЕМЕНИ
        public class TimeEfficiencyAnalysis
        {
            public decimal AverageDealDurationDays { get; set; }
            public Dictionary<string, decimal> DurationByCategory { get; set; } = new Dictionary<string, decimal>();
            public Dictionary<string, decimal> SuccessRateByWeekday { get; set; } = new Dictionary<string, decimal>();
            public List<string> Recommendations { get; set; } = new List<string>();
        }

        public static TimeEfficiencyAnalysis AnalyzeTimeEfficiency(DateTime startDate, DateTime endDate)
        {
            using (var context = MultiUserSecurityManager.CreateCompanyContext())
            {
                var analysis = new TimeEfficiencyAnalysis();

                var successfulDeals = context.Deals
                    .Where(d => d.Status == DealStatus.Successful &&
                               d.CreatedAt >= startDate && d.CreatedAt <= endDate &&
                               d.ClosedAt.HasValue)
                    .ToList();

                if (successfulDeals.Any())
                {
                    // Средняя продолжительность сделки
                    analysis.AverageDealDurationDays = (decimal)successfulDeals
                        .Average(d => (d.ClosedAt.Value - d.CreatedAt).TotalDays);

                    // Продолжительность по категориям
                    var dealsByCategory = successfulDeals
                        .Where(d => !string.IsNullOrEmpty(d.Category))
                        .GroupBy(d => d.Category)
                        .ToList();

                    foreach (var group in dealsByCategory)
                    {
                        decimal avgDuration = (decimal)group
                            .Average(d => (d.ClosedAt.Value - d.CreatedAt).TotalDays);
                        analysis.DurationByCategory[group.Key] = avgDuration;
                    }

                    // Успешность по дням недели
                    var dealsByWeekday = successfulDeals
                        .GroupBy(d => d.CreatedAt.DayOfWeek)
                        .Select(g => new
                        {
                            Weekday = g.Key,
                            Successful = g.Count(),
                            Total = context.Deals.Count(d =>
                                d.CreatedAt.DayOfWeek == g.Key &&
                                d.CreatedAt >= startDate && d.CreatedAt <= endDate)
                        })
                        .ToList();

                    foreach (var day in dealsByWeekday)
                    {
                        if (day.Total > 0)
                        {
                            decimal rate = (decimal)day.Successful / day.Total * 100;
                            analysis.SuccessRateByWeekday[day.Weekday.ToString()] = rate;
                        }
                    }

                    // Рекомендации
                    if (analysis.AverageDealDurationDays > 30)
                        analysis.Recommendations.Add("Средняя сделка занимает более 30 дней. Ускорьте процесс продаж.");

                    var fastestCategory = analysis.DurationByCategory
                        .OrderBy(x => x.Value)
                        .FirstOrDefault();
                    var slowestCategory = analysis.DurationByCategory
                        .OrderByDescending(x => x.Value)
                        .FirstOrDefault();

                    if (!fastestCategory.Equals(default(KeyValuePair<string, decimal>)))
                        analysis.Recommendations.Add($"Быстрее всего закрываются сделки в категории '{fastestCategory.Key}' ({fastestCategory.Value:F1} дней)");

                    var bestDay = analysis.SuccessRateByWeekday
                        .OrderByDescending(x => x.Value)
                        .FirstOrDefault();

                    if (!bestDay.Equals(default(KeyValuePair<string, decimal>)))
                        analysis.Recommendations.Add($"Лучший день для сделок: {bestDay.Key} ({bestDay.Value:F1}% успеха)");
                }

                return analysis;
            }
        }

        // 4. ИНДЕКС УДОВЛЕТВОРЕННОСТИ (CSAT)
        public class CustomerSatisfactionIndex
        {
            public decimal CSATScore { get; set; } // 0-100
            public int RespondentCount { get; set; }
            public Dictionary<string, int> FeedbackByCategory { get; set; } = new Dictionary<string, int>();
            public List<string> ImprovementAreas { get; set; } = new List<string>();
        }

        public static CustomerSatisfactionIndex CalculateCSAT(DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var csat = new CustomerSatisfactionIndex();
                
                // Если период не указан, берем последние 30 дней
                if (!startDate.HasValue) startDate = DateTime.Now.AddDays(-30);
                if (!endDate.HasValue) endDate = DateTime.Now;

                using (var context = MultiUserSecurityManager.CreateCompanyContext())
                {
                    // 1. Конверсия сделок (0-100 баллов)
                    var totalDeals = context.Deals.Count(d => d.CreatedAt >= startDate && d.CreatedAt <= endDate);
                    var successfulDeals = context.Deals.Count(d => d.Status == DealStatus.Successful && d.CreatedAt >= startDate && d.CreatedAt <= endDate);
                    decimal conversionScore = totalDeals > 0 ? (successfulDeals * 100m / totalDeals) : 0;

                    // 2. Процент успешных сделок (0-100 баллов)
                    decimal successRate = totalDeals > 0 ? (successfulDeals * 100m / totalDeals) : 0;

                    // 3. ABC-категории клиентов (0-100 баллов)
                    var clients = context.Clients.ToList();
                    var clientDeals = context.Deals
                        .Where(d => d.CreatedAt >= startDate && d.CreatedAt <= endDate)
                        .GroupBy(d => d.ClientId)
                        .ToDictionary(g => g.Key, g => g.Count());

                    int vipClients = 0, activeClients = 0, periodicClients = 0;
                    foreach (var client in clients)
                    {
                        int dealCount = clientDeals.GetValueOrDefault(client.Id, 0);
                        if (client.ABC_Category == "A") vipClients++;
                        else if (dealCount >= 3) activeClients++;
                        else if (dealCount >= 1) periodicClients++;
                    }

                    int totalClientsWithDeals = vipClients + activeClients + periodicClients;
                    decimal abcScore = totalClientsWithDeals > 0 ? 
                        ((vipClients * 100m + activeClients * 80m + periodicClients * 60m) / totalClientsWithDeals) : 50;

                    // 4. Повторные сделки (0-100 баллов)
                    var repeatClients = context.Deals
                        .Where(d => d.CreatedAt >= startDate && d.CreatedAt <= endDate)
                        .GroupBy(d => d.ClientId)
                        .Count(g => g.Count() > 1);

                    decimal repeatScore = clients.Any() ? (repeatClients * 100m / clients.Count) : 0;

                    // 5. Активность клиентов (0-100 баллов)
                    var activeClientsCount = context.Deals
                        .Where(d => d.CreatedAt >= startDate && d.CreatedAt <= endDate)
                        .Select(d => d.ClientId)
                        .Distinct()
                        .Count();

                    decimal activityScore = clients.Any() ? (activeClientsCount * 100m / clients.Count) : 0;

                    // Итоговый CSAT с весами
                    csat.CSATScore = (
                        conversionScore * 0.30m +
                        successRate * 0.25m +
                        abcScore * 0.20m +
                        repeatScore * 0.15m +
                        activityScore * 0.10m
                    );

                    // Ограничиваем диапазон 0-100
                    csat.CSATScore = Math.Max(0, Math.Min(100, csat.CSATScore));
                    
                    // Количество "респондентов" - это количество активных клиентов
                    csat.RespondentCount = activeClientsCount;

                    // Заполняем категории обратной связи на основе бизнес-метрик
                    csat.FeedbackByCategory["Качество продукта"] = (int)(successRate * 0.1m); // На основе успешности сделок
                    csat.FeedbackByCategory["Служба поддержки"] = (int)(repeatScore * 0.1m); // На основе повторных сделок
                    csat.FeedbackByCategory["Цена"] = (int)(abcScore * 0.1m); // На основе ABC категорий
                    csat.FeedbackByCategory["Сроки"] = (int)(conversionScore * 0.1m); // На основе конверсии
                    csat.FeedbackByCategory["Коммуникация"] = (int)(activityScore * 0.1m); // На основе активности

                    // Области для улучшения
                    var lowestCategories = csat.FeedbackByCategory
                        .OrderBy(x => x.Value)
                        .Take(2)
                        .Select(x => x.Key)
                        .ToList();

                    foreach (var category in lowestCategories)
                    {
                        csat.ImprovementAreas.Add($"Улучшить {category}");
                    }

                    if (csat.CSATScore < 70)
                        csat.ImprovementAreas.Add("Требуется срочное улучшение клиентского опыта");
                    else if (csat.CSATScore > 85)
                        csat.ImprovementAreas.Add("Отличные показатели! Продолжайте в том же духе");
                }

                return csat;
            }
            catch
            {
                // В случае ошибки возвращаем среднее значение
                return new CustomerSatisfactionIndex
                {
                    CSATScore = 75,
                    RespondentCount = 0,
                    FeedbackByCategory = new Dictionary<string, int>
                    {
                        ["Качество продукта"] = 7,
                        ["Служба поддержки"] = 7,
                        ["Цена"] = 7,
                        ["Сроки"] = 7,
                        ["Коммуникация"] = 7
                    },
                    ImprovementAreas = new List<string> { "Недостаточно данных для анализа" }
                };
            }
        }

        // 5. ПРОДВИНУТЫЙ ABC-АНАЛИЗ (УРОВЕНЬ 11/10)
        public class AdvancedABCAnalysis
        {
            public class ABCClient
            {
                public string ClientName { get; set; } = "";
                public decimal TotalRevenue { get; set; }
                public int DealCount { get; set; }
                public decimal AverageDealSize { get; set; }
                public DateTime LastDealDate { get; set; }
                public int DaysSinceLastDeal { get; set; }
                public decimal ProfitMargin { get; set; }
                public int SuccessfulDeals { get; set; }
                public decimal ConversionRate { get; set; }
                public string Category { get; set; } = "";
                public decimal CategoryContribution { get; set; }
                public int LoyaltyScore { get; set; } // 1-100
                public string RiskLevel { get; set; } = "";
                public List<string> Recommendations { get; set; } = new List<string>();
            }

            public class ABCSummary
            {
                public Dictionary<string, List<ABCClient>> ClientsByCategory { get; set; } = new Dictionary<string, List<ABCClient>>();
                public Dictionary<string, decimal> RevenueByCategory { get; set; } = new Dictionary<string, decimal>();
                public Dictionary<string, int> ClientCountByCategory { get; set; } = new Dictionary<string, int>();
                public decimal TotalRevenue { get; set; }
                public int TotalClients { get; set; }
                public decimal AverageRevenuePerClient { get; set; }
                public List<string> StrategicInsights { get; set; } = new List<string>();
                public List<string> ActionableRecommendations { get; set; } = new List<string>();
                public Dictionary<string, decimal> CategoryEfficiency { get; set; } = new Dictionary<string, decimal>();
                public int PotentialRevenueIncrease { get; set; }
                public int AtRiskClientsCount { get; set; }
            }

            public static ABCSummary PerformAdvancedABCAnalysis(DateTime? startDate = null, DateTime? endDate = null)
            {
                try
                {
                    if (!startDate.HasValue) startDate = DateTime.Now.AddMonths(-12);
                    if (!endDate.HasValue) endDate = DateTime.Now;

                    using (var context = MultiUserSecurityManager.CreateCompanyContext())
                    {
                        var analysis = new ABCSummary();

                        // Получаем все сделки за период
                        var deals = context.Deals
                            .Include(d => d.Client)
                            .Where(d => d.CreatedAt >= startDate && d.CreatedAt <= endDate)
                            .ToList();

                        var successfulDeals = deals.Where(d => d.Status == DealStatus.Successful).ToList();

                        // Группируем по клиентам с расширенными метриками
                        var clientData = successfulDeals
                            .GroupBy(d => d.Client)
                            .Where(g => g.Key != null)
                            .Select(g => new ABCClient
                            {
                                ClientName = g.Key.Name,
                                TotalRevenue = g.Sum(d => d.Amount),
                                DealCount = g.Count(),
                                AverageDealSize = g.Average(d => d.Amount),
                                LastDealDate = g.Max(d => d.CreatedAt),
                                SuccessfulDeals = g.Count(),
                                ProfitMargin = CalculateProfitMargin(g.ToList()),
                                LoyaltyScore = CalculateLoyaltyScore(g.ToList()),
                                DaysSinceLastDeal = (int)(DateTime.Now - g.Max(d => d.CreatedAt)).TotalDays
                            })
                            .Where(c => c.TotalRevenue > 0)
                            .OrderByDescending(c => c.TotalRevenue)
                            .ToList();

                        if (!clientData.Any())
                        {
                            return CreateEmptyAnalysis();
                        }

                        // Расчет конверсии для каждого клиента
                        var allClientDeals = deals.GroupBy(d => d.ClientId).ToDictionary(g => g.Key, g => g.ToList());
                        foreach (var client in clientData)
                        {
                            var clientId = context.Clients.First(c => c.Name == client.ClientName).Id;
                            var allDealsForClient = allClientDeals.GetValueOrDefault(clientId, new List<Deal>());
                            client.ConversionRate = allDealsForClient.Any() ? 
                                (decimal)allDealsForClient.Count(d => d.Status == DealStatus.Successful) * 100 / allDealsForClient.Count : 0;
                        }

                        // Классический ABC-анализ по принципу Парето (80/20)
                        analysis.TotalRevenue = clientData.Sum(c => c.TotalRevenue);
                        analysis.TotalClients = clientData.Count;
                        analysis.AverageRevenuePerClient = analysis.TotalRevenue / analysis.TotalClients;

                        var cumulativeRevenue = 0m;
                        var categoryThresholds = new Dictionary<string, decimal>
                        {
                            ["A"] = 0.80m,  // 80% выручки
                            ["B"] = 0.95m,  // 95% выручки
                            ["C"] = 1.00m   // 100% выручки
                        };

                        foreach (var client in clientData)
                        {
                            cumulativeRevenue += client.TotalRevenue;
                            var cumulativePercentage = cumulativeRevenue / analysis.TotalRevenue;

                            if (cumulativePercentage <= categoryThresholds["A"])
                                client.Category = "A";
                            else if (cumulativePercentage <= categoryThresholds["B"])
                                client.Category = "B";
                            else
                                client.Category = "C";

                            client.CategoryContribution = client.TotalRevenue / analysis.TotalRevenue * 100;

                            // Определяем уровень риска
                            client.RiskLevel = DetermineRiskLevel(client);

                            // Генерируем персонализированные рекомендации
                            client.Recommendations = GenerateClientRecommendations(client);
                        }

                        // Группируем по категориям
                        analysis.ClientsByCategory = clientData
                            .GroupBy(c => c.Category)
                            .ToDictionary(g => g.Key, g => g.ToList());

                        analysis.RevenueByCategory = analysis.ClientsByCategory
                            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Sum(c => c.TotalRevenue));

                        analysis.ClientCountByCategory = analysis.ClientsByCategory
                            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Count);

                        // Расчет эффективности категорий
                        analysis.CategoryEfficiency = CalculateCategoryEfficiency(analysis.ClientsByCategory);

                        // Генерация стратегических инсайтов
                        analysis.StrategicInsights = GenerateStrategicInsights(analysis);

                        // Генерация рекомендаций
                        analysis.ActionableRecommendations = GenerateActionableRecommendations(analysis);

                        // Расчет потенциального увеличения дохода
                        analysis.PotentialRevenueIncrease = CalculatePotentialRevenueIncrease(analysis);

                        // Подсчет клиентов в группе риска
                        analysis.AtRiskClientsCount = clientData.Count(c => c.RiskLevel == "Высокий");

                        return analysis;
                    }
                }
                catch (Exception ex)
                {
                    return new ABCSummary
                    {
                        StrategicInsights = new List<string> { $"Ошибка анализа: {ex.Message}" },
                        ActionableRecommendations = new List<string> { "Проверьте данные и повторите попытку" }
                    };
                }
            }

            private static decimal CalculateProfitMargin(List<Deal> deals)
            {
                // Условная прибыль 25% от суммы сделок
                return deals.Any() ? 25m : 0;
            }

            private static int CalculateLoyaltyScore(List<Deal> deals)
            {
                if (!deals.Any()) return 0;

                var score = 0;
                
                // Количество сделок (макс 40 баллов)
                score += Math.Min(40, deals.Count * 4);
                
                // Регулярность (макс 30 баллов)
                var daysSpan = (deals.Max(d => d.CreatedAt) - deals.Min(d => d.CreatedAt)).TotalDays;
                if (daysSpan > 0)
                {
                    var frequency = deals.Count / (daysSpan / 30); // сделок в месяц
                    score += Math.Min(30, (int)(frequency * 10));
                }
                
                // Размер средних сделок (макс 30 баллов)
                var avgSize = deals.Average(d => d.Amount);
                if (avgSize >= 100000) score += 30;
                else if (avgSize >= 50000) score += 20;
                else if (avgSize >= 20000) score += 10;

                return Math.Min(100, score);
            }

            private static string DetermineRiskLevel(ABCClient client)
            {
                var riskFactors = 0;

                // Факторы риска
                if (client.DaysSinceLastDeal > 90) riskFactors++;
                if (client.ConversionRate < 50) riskFactors++;
                if (client.DealCount < 2) riskFactors++;
                if (client.LoyaltyScore < 30) riskFactors++;
                if (client.AverageDealSize < 10000) riskFactors++;

                return riskFactors switch
                {
                    >= 4 => "Высокий",
                    >= 2 => "Средний",
                    _ => "Низкий"
                };
            }

            private static List<string> GenerateClientRecommendations(ABCClient client)
            {
                var recommendations = new List<string>();

                if (client.Category == "A")
                {
                    recommendations.Add("Персональный менеджер и VIP-обслуживание");
                    if (client.DaysSinceLastDeal > 30)
                        recommendations.Add("Срочно связаться - давно не было сделок");
                }
                else if (client.Category == "B")
                {
                    recommendations.Add("Регулярные контакты и специальные предложения");
                    if (client.ConversionRate < 70)
                        recommendations.Add("Работать над повышением конверсии");
                }
                else
                {
                    recommendations.Add("Автоматизированные маркетинговые кампании");
                    if (client.DealCount == 1)
                        recommendations.Add("Стимулировать повторные покупки");
                }

                if (client.RiskLevel == "Высокий")
                {
                    recommendations.Add("⚠️ Внимание: клиент в группе риска!");
                }

                return recommendations;
            }

            private static Dictionary<string, decimal> CalculateCategoryEfficiency(Dictionary<string, List<ABCClient>> clientsByCategory)
            {
                var efficiency = new Dictionary<string, decimal>();

                foreach (var category in clientsByCategory)
                {
                    var clients = category.Value;
                    if (!clients.Any())
                    {
                        efficiency[category.Key] = 0;
                        continue;
                    }

                    var avgRevenue = clients.Average(c => c.TotalRevenue);
                    var avgDeals = clients.Average(c => c.DealCount);
                    var avgLoyalty = clients.Average(c => c.LoyaltyScore);

                    // Комплексный показатель эффективности
                    efficiency[category.Key] = (avgRevenue / 100000m) * 0.4m + ((decimal)avgDeals / 10m) * 0.3m + ((decimal)avgLoyalty / 100m) * 0.3m;
                }

                return efficiency;
            }

            private static List<string> GenerateStrategicInsights(ABCSummary analysis)
            {
                var insights = new List<string>();

                // Анализ распределения выручки
                var revenueA = analysis.RevenueByCategory.GetValueOrDefault("A", 0);
                var revenueB = analysis.RevenueByCategory.GetValueOrDefault("B", 0);
                var revenueC = analysis.RevenueByCategory.GetValueOrDefault("C", 0);

                if (revenueA / analysis.TotalRevenue > 0.6m)
                    insights.Add("🎯 Высокая концентрация выручки в группе A - рискованно, нужно диверсифицировать");

                if (analysis.ClientCountByCategory.GetValueOrDefault("A", 0) < 5)
                    insights.Add("⚠️ Мало VIP-клиентов - необходимо развивать программу лояльности");

                var avgClientValue = analysis.AverageRevenuePerClient;
                if (avgClientValue > 100000)
                    insights.Add("💰 Высокий средний чек клиента - отличные показатели!");
                else if (avgClientValue < 30000)
                    insights.Add("📈 Низкий средний чек - потенциал для роста");

                if (analysis.AtRiskClientsCount > analysis.TotalClients * 0.3m)
                    insights.Add("🚨 Много клиентов в группе риска - нужна срочная работа с удержанием");

                return insights;
            }

            private static List<string> GenerateActionableRecommendations(ABCSummary analysis)
            {
                var recommendations = new List<string>();

                // Рекомендации по категориям
                if (analysis.ClientsByCategory.ContainsKey("A"))
                {
                    var groupA = analysis.ClientsByCategory["A"];
                    recommendations.Add($"👑 Группа A: {groupA.Count} VIP-клиентов. Выделить персональных менеджеров, создать эксклюзивные предложения");
                }

                if (analysis.ClientsByCategory.ContainsKey("B"))
                {
                    var groupB = analysis.ClientsByCategory["B"];
                    recommendations.Add($"🌟 Группа B: {groupB.Count} стабильных клиентов. Регулярные контакты, программа лояльности, кросс-продажи");
                }

                if (analysis.ClientsByCategory.ContainsKey("C"))
                {
                    var groupC = analysis.ClientsByCategory["C"];
                    recommendations.Add($"📊 Группа C: {groupC.Count} массовых клиентов. Автоматизация, email-маркетинг, специальные акции");
                }

                // Общие рекомендации
                if (analysis.PotentialRevenueIncrease > 1000000)
                    recommendations.Add($"💎 Потенциал роста дохода: {analysis.PotentialRevenueIncrease:N0} ₽ при работе с отточными клиентами");

                if (analysis.AtRiskClientsCount > 0)
                    recommendations.Add($"⚠️ Срочно работать с {analysis.AtRiskClientsCount} клиентами в группе риска");

                return recommendations;
            }

            private static int CalculatePotentialRevenueIncrease(ABCSummary analysis)
            {
                // Потенциал через удержание и развитие клиентов групп B и C
                var potential = 0;

                if (analysis.ClientsByCategory.ContainsKey("B"))
                {
                    var groupB = analysis.ClientsByCategory["B"];
                    potential += (int)(groupB.Sum(c => c.TotalRevenue) * 0.3m); // 30% рост
                }

                if (analysis.ClientsByCategory.ContainsKey("C"))
                {
                    var groupC = analysis.ClientsByCategory["C"];
                    potential += (int)(groupC.Sum(c => c.TotalRevenue) * 0.5m); // 50% рост
                }

                return potential;
            }

            private static ABCSummary CreateEmptyAnalysis()
            {
                return new ABCSummary
                {
                    StrategicInsights = new List<string> { "Недостаточно данных для ABC-анализа" },
                    ActionableRecommendations = new List<string> { "Накопите больше данных о сделках с клиентами" }
                };
            }
        }
    }
}