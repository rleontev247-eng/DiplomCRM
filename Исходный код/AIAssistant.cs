using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace MyFirstCRM
{
    public class AIRecommendation
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public decimal PotentialRevenue { get; set; }
        public int Confidence { get; set; } // 0-100%
        public string ActionRequired { get; set; } = "";
        public string Icon { get; set; } = "";
        public string Category { get; set; } = "";
    }

    public static class AIAssistant
    {
        public static List<AIRecommendation> GetDailyRecommendations()
        {
            try
            {
                using (var context = MultiUserSecurityManager.CreateCompanyContext())
                {
                    var recommendations = new List<AIRecommendation>();
                    var now = DateTime.Now;

                    // Проверяем наличие данных
                    var totalDeals = context.Deals.Count();
                    if (totalDeals == 0)
                    {
                        return new List<AIRecommendation>
                        {
                            new AIRecommendation
                            {
                                Title = "🚀 Начало работы",
                                Description = "Система готова к работе. Добавьте первых клиентов и сделки для получения AI-рекомендаций.",
                                Confidence = 100,
                                ActionRequired = "Создать первого клиента и первую сделку",
                                Icon = "🎯",
                                Category = "Начало"
                            }
                        };
                    }

                    // 1. Анализ "спящих" клиентов (улучшенный)
                    var inactiveClients = context.Deals
                        .Where(d => d.Status == DealStatus.Successful)
                        .Include(d => d.Client)
                        .AsEnumerable()
                        .GroupBy(d => d.ClientId)
                        .Select(g => new
                        {
                            ClientId = g.Key,
                            ClientName = g.FirstOrDefault()?.Client?.Name ?? "Неизвестный клиент",
                            LastDeal = g.Max(d => d.ClosedAt ?? d.CreatedAt),
                            AverageAmount = g.Average(d => d.Amount),
                            TotalDeals = g.Count(),
                            DaysSinceLastDeal = g.Max(d => d.ClosedAt ?? d.CreatedAt) > DateTime.MinValue
                                ? (DateTime.Now - g.Max(d => d.ClosedAt ?? d.CreatedAt)).Days
                                : 9999
                        })
                        .Where(x => x.DaysSinceLastDeal > 30 && x.DaysSinceLastDeal < 365)
                        .OrderByDescending(x => x.AverageAmount * x.TotalDeals)
                        .Take(5)
                        .ToList();

                    foreach (var client in inactiveClients.Take(3))
                    {
                        var priority = client.DaysSinceLastDeal < 60 ? "Высокий" : 
                                      client.DaysSinceLastDeal < 120 ? "Средний" : "Низкий";
                        
                        recommendations.Add(new AIRecommendation
                        {
                            Title = $"🔄 Реактивация: {client.ClientName}",
                            Description = $"Клиент не покупал {client.DaysSinceLastDeal} дней. " +
                                         $"История: {client.TotalDeals} сделок на сумму {client.AverageAmount * client.TotalDeals:N0} ₽. " +
                                         $"Приоритет: {priority}",
                            PotentialRevenue = client.AverageAmount,
                            Confidence = Math.Min(95, 70 + (int)(client.TotalDeals * 3.0)),
                            ActionRequired = client.DaysSinceLastDeal < 60 
                                ? "Позвонить сегодня с эксклюзивным предложением"
                                : "Отправить персональное письмо на следующей неделе",
                            Icon = "📞",
                            Category = "Реактивация"
                        });
                    }

                    // 2. Анализ лучшего времени для контакта (улучшенный)
                    var dealsByHour = context.Deals
                        .Where(d => d.Status == DealStatus.Successful)
                        .AsEnumerable()
                        .GroupBy(d => d.CreatedAt.Hour)
                        .Select(g => new { Hour = g.Key, Count = g.Count(), TotalAmount = g.Sum(d => d.Amount) })
                        .OrderByDescending(x => x.Count)
                        .FirstOrDefault();

                    if (dealsByHour != null && dealsByHour.Count > 2)
                    {
                        recommendations.Add(new AIRecommendation
                        {
                            Title = $"⏰ Оптимальное время: {dealsByHour.Hour:00}:00",
                            Description = $"Больше всего успешных сделок ({dealsByHour.Count}) закрывается в {dealsByHour.Hour:00}:00. " +
                                         $"Средняя сумма: {dealsByHour.TotalAmount / dealsByHour.Count:N0} ₽",
                            Confidence = Math.Min(90, 60 + dealsByHour.Count * 5),
                            ActionRequired = $"Планируйте важные звонки на {dealsByHour.Hour:00}:00",
                            Icon = "⏰",
                            Category = "Оптимизация"
                        });
                    }

                    // 3. Анализ трендов по категориям (улучшенный)
                    var lastMonth = now.AddMonths(-1);
                    var twoMonthsAgo = now.AddMonths(-2);

                    var categoryTrends = context.Deals
                        .Where(d => d.Status == DealStatus.Successful &&
                                   !string.IsNullOrEmpty(d.Category) &&
                                   d.CreatedAt >= twoMonthsAgo)
                        .AsEnumerable()
                        .GroupBy(d => d.Category)
                        .Select(g => new
                        {
                            Category = g.Key,
                            RecentCount = g.Count(d => d.CreatedAt >= lastMonth),
                            OldCount = g.Count(d => d.CreatedAt < lastMonth),
                            RecentRevenue = g.Where(d => d.CreatedAt >= lastMonth).Sum(d => d.Amount),
                            OldRevenue = g.Where(d => d.CreatedAt < lastMonth).Sum(d => d.Amount)
                        })
                        .Where(x => x.OldCount > 0)
                        .Select(x => new
                        {
                            x.Category,
                            x.RecentCount,
                            x.OldCount,
                            x.RecentRevenue,
                            x.OldRevenue,
                            GrowthPercent = x.OldCount > 0 ? ((double)(x.RecentCount - x.OldCount) * 100.0 / x.OldCount) : 0,
                            RevenueGrowth = x.OldRevenue > 0 ? (double)((x.RecentRevenue - x.OldRevenue) / x.OldRevenue * 100) : 0
                        })
                        .Where(x => x.GrowthPercent > 10 || x.RevenueGrowth > 10)
                        .OrderByDescending(x => (double)(x.GrowthPercent + x.RevenueGrowth))
                        .FirstOrDefault();

                    if (categoryTrends != null)
                    {
                        recommendations.Add(new AIRecommendation
                        {
                            Title = $"📈 Растущая категория: {categoryTrends.Category}",
                            Description = $"Спрос вырос на {categoryTrends.GrowthPercent:F0}% (+{categoryTrends.RevenueGrowth:F0}% выручки). " +
                                         $"Текущий месяц: {categoryTrends.RecentRevenue:N0} ₽ ({categoryTrends.RecentCount} сделок)",
                            PotentialRevenue = categoryTrends.RecentRevenue * 0.3m,
                            Confidence = 85,
                            ActionRequired = "Увеличить предложение и маркетинг в этой категории",
                            Icon = "📈",
                            Category = "Возможность"
                        });
                    }

                    // 4. Сделки под угрозой срыва (улучшенный)
                    var riskyDeals = context.Deals
                        .Where(d => d.Status == DealStatus.InProgress &&
                                   d.Deadline < DateTime.Now.AddDays(7) &&
                                   d.Deadline > DateTime.Now.AddDays(-1))
                        .Include(d => d.Client)
                        .OrderBy(d => d.Deadline)
                        .Take(5)
                        .ToList();

                    foreach (var deal in riskyDeals)
                    {
                        var daysLeft = (deal.Deadline - DateTime.Now).Days;
                        var urgency = daysLeft <= 1 ? "🔥 СРОЧНО" : daysLeft <= 3 ? "⚠️ Важно" : "📌 Контроль";

                        recommendations.Add(new AIRecommendation
                        {
                            Title = $"{urgency} Сделка: {deal.Title}",
                            Description = $"Дедлайн через {daysLeft} дн. " +
                                         $"Клиент: {deal.Client?.Name}. " +
                                         $"Сумма: {deal.Amount:N0} ₽. Вероятность: {deal.Probability}%",
                            PotentialRevenue = deal.Amount * (deal.Probability / 100m),
                            Confidence = Math.Max(40, 100 - daysLeft * 10),
                            ActionRequired = daysLeft <= 1 
                                ? "СВЯЗАТЬСЯ С КЛИЕНТОТ НЕМЕДЛЕННО!"
                                : daysLeft <= 3 
                                    ? "Позвонить сегодня для уточнения статуса"
                                    : "Запланировать контрольный контакт",
                            Icon = urgency.Contains("СРОЧНО") ? "🔥" : urgency.Contains("Важно") ? "⚠️" : "📌",
                            Category = "Риск"
                        });
                    }

                    // 5. Анализ конверсии (улучшенный)
                    var successfulDeals = context.Deals.Count(d => d.Status == DealStatus.Successful);
                    var conversionRate = totalDeals > 0 ? (successfulDeals * 100.0 / totalDeals) : 0;

                    if (conversionRate < 25 && totalDeals > 5)
                    {
                        recommendations.Add(new AIRecommendation
                        {
                            Title = "📉 Низкая конверсия сделок",
                            Description = $"Текущая конверсия: {conversionRate:F1}% ({successfulDeals} из {totalDeals}). " +
                                         "Отраслевой стандарт: 30-40%. Требуется анализ воронки.",
                            Confidence = 90,
                            ActionRequired = "Проанализировать причины отказов и улучшить скрипты продаж",
                            Icon = "📉",
                            Category = "Оптимизация"
                        });
                    }
                    else if (conversionRate > 50 && totalDeals > 10)
                    {
                        recommendations.Add(new AIRecommendation
                        {
                            Title = "🎯 Отличная конверсия!",
                            Description = $"Ваша конверсия: {conversionRate:F1}% - выше среднего! " +
                                         $"Это говорит о качественной работе команды.",
                            Confidence = 95,
                            ActionRequired = "Зафиксировать успешные практики и масштабировать",
                            Icon = "🎯",
                            Category = "Успех"
                        });
                    }

                    // 6. Клиенты с высоким потенциалом (улучшенный)
                    var highPotentialClients = context.Deals
                        .Where(d => d.Status == DealStatus.Successful)
                        .Include(d => d.Client)
                        .AsEnumerable()
                        .GroupBy(d => d.ClientId)
                        .Where(g => g.Count() >= 1 && g.First().Amount > 30000)
                        .Select(g => new
                        {
                            Client = g.First().Client,
                            TotalAmount = g.Sum(d => d.Amount),
                            DealCount = g.Count(),
                            LastDealDate = g.Max(d => d.CreatedAt),
                            DaysSinceLastDeal = (DateTime.Now - g.Max(d => d.CreatedAt)).Days
                        })
                        .Where(x => x.DaysSinceLastDeal >= 7 && x.DaysSinceLastDeal <= 60)
                        .OrderByDescending(x => x.TotalAmount)
                        .Take(3)
                        .ToList();

                    foreach (var client in highPotentialClients)
                    {
                        recommendations.Add(new AIRecommendation
                        {
                            Title = $"💎 Перспективный клиент: {client.Client?.Name}",
                            Description = $"История покупок: {client.DealCount} сделок на {client.TotalAmount:N0} ₽. " +
                                         $"Последняя покупка: {client.DaysSinceLastDeal} дней назад.",
                            PotentialRevenue = client.TotalAmount * 0.4m,
                            Confidence = 75,
                            ActionRequired = "Предложить премиум-продукты или индивидуальные условия",
                            Icon = "💎",
                            Category = "Возможность"
                        });
                    }

                    // 7. Анализ эффективности по дням недели
                    var weekdayAnalysis = context.Deals
                        .Where(d => d.Status == DealStatus.Successful)
                        .AsEnumerable()
                        .GroupBy(d => d.CreatedAt.DayOfWeek)
                        .Select(g => new
                        {
                            DayOfWeek = g.Key,
                            Count = g.Count(),
                            TotalAmount = g.Sum(d => d.Amount),
                            AvgAmount = g.Average(d => d.Amount)
                        })
                        .OrderByDescending(x => x.Count)
                        .FirstOrDefault();

                    if (weekdayAnalysis != null && weekdayAnalysis.Count > 3)
                    {
                        recommendations.Add(new AIRecommendation
                        {
                            Title = $"📅 Лучший день: {weekdayAnalysis.DayOfWeek}",
                            Description = $"Большинство успешных сделок ({weekdayAnalysis.Count}) закрывается в {weekdayAnalysis.DayOfWeek}. " +
                                         $"Средний чек: {weekdayAnalysis.AvgAmount:N0} ₽",
                            Confidence = 70,
                            ActionRequired = $"Планировать важные встречи на {weekdayAnalysis.DayOfWeek}",
                            Icon = "📅",
                            Category = "Оптимизация"
                        });
                    }

                    // Если рекомендаций мало, добавляем общие советы
                    if (recommendations.Count < 3)
                    {
                        recommendations.Add(new AIRecommendation
                        {
                            Title = "✅ Отличная работа!",
                            Description = "Все процессы под контролем. Продолжайте в том же духе и развивайте бизнес!",
                            Confidence = 100,
                            ActionRequired = "Проверить новые лиды и обновить данные клиентов",
                            Icon = "✅",
                            Category = "Общее"
                        });
                    }

                    return recommendations
                        .OrderByDescending(r => r.Confidence)
                        .ThenByDescending(r => r.PotentialRevenue)
                        .Take(8)
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                return new List<AIRecommendation>
                {
                    new AIRecommendation
                    {
                        Title = "⚠️ Ошибка загрузки рекомендаций",
                        Description = $"Произошла ошибка: {ex.Message}. Проверьте подключение к базе данных.",
                        Confidence = 100,
                        ActionRequired = "Обратиться к администратору системы",
                        Icon = "⚠️",
                        Category = "Ошибка"
                    }
                };
            }
        }

        // Анализ конкретной сделки
        public static DealAnalysis AnalyzeDeal(int dealId)
        {
            try
            {
                using (var context = MultiUserSecurityManager.CreateCompanyContext())
                {
                    var deal = context.Deals
                        .Include(d => d.Client)
                        .FirstOrDefault(d => d.Id == dealId);

                    if (deal == null)
                        return new DealAnalysis { ErrorMessage = "Сделка не найдена" };

                    var analysis = new DealAnalysis
                    {
                        DealTitle = deal.Title,
                        CurrentProbability = deal.Probability
                    };

                    // Анализ истории клиента
                    var clientHistory = context.Deals
                        .Where(d => d.ClientId == deal.ClientId && d.Id != dealId)
                        .ToList();

                    if (clientHistory.Any())
                    {
                        var successRate = clientHistory.Count > 0 ? (double)clientHistory.Count(d => d.Status == DealStatus.Successful) * 100.0 / clientHistory.Count : 0;
                        analysis.Insights.Add($"Клиент закрывает {successRate:F0}% сделок успешно");

                        var avgDealValue = clientHistory.Where(d => d.Status == DealStatus.Successful).Average(d => d.Amount);
                        analysis.Insights.Add($"Средний чек клиента: {avgDealValue:N0} ₽");

                        if (deal.Amount > avgDealValue * 1.5m)
                            analysis.Risks.Add("Сумма сделки значительно выше среднего чека клиента");
                        else if (deal.Amount < avgDealValue * 0.5m)
                            analysis.Insights.Add("Сумма сделки ниже среднего - может быть легкой для закрытия");
                    }
                    else
                    {
                        analysis.Insights.Add("Новый клиент - нет истории сделок");
                        analysis.Risks.Add("Отсутствует история взаимодействий с клиентом");
                    }

                    // Анализ сроков
                    var daysUntilDeadline = (deal.Deadline - DateTime.Now).Days;
                    var dealAge = (DateTime.Now - deal.CreatedAt).Days;

                    if (daysUntilDeadline < 0)
                        analysis.Risks.Add($"Срок сделки истек {Math.Abs(daysUntilDeadline)} дней назад!");
                    else if (daysUntilDeadline < 3)
                        analysis.Risks.Add($"Осталось всего {daysUntilDeadline} дн. до дедлайна!");

                    if (dealAge > 30 && deal.Status == DealStatus.InProgress)
                        analysis.Risks.Add("Сделка в работе более месяца");
                    else if (dealAge < 7)
                        analysis.Insights.Add("Новая сделка - хороший темп");

                    // Анализ вероятности
                    if (deal.Probability < 30)
                        analysis.Risks.Add("Низкая вероятность закрытия - требуется вмешательство");
                    else if (deal.Probability > 80)
                        analysis.Insights.Add("Высокая вероятность - готовьтесь к закрытию");

                    // Рекомендации
                    if (deal.Probability < 50)
                    {
                        analysis.Recommendations.Add("Уточнить потребности клиента и болевые точки");
                        analysis.Recommendations.Add("Предложить персональную скидку или бонус");
                    }
                    else if (deal.Probability >= 80)
                    {
                        analysis.Recommendations.Add("Подготовить договор и необходимые документы");
                        analysis.Recommendations.Add("Согласовать точные сроки поставки/оказания услуг");
                    }

                    if (daysUntilDeadline <= 3 && daysUntilDeadline > 0)
                        analysis.Recommendations.Add("СРОЧНО связаться с клиентом для уточнения статуса");

                    if (deal.Priority == Priority.High || deal.Priority == Priority.Critical)
                        analysis.Recommendations.Add("Высокий приоритет - уделить особое внимание");

                    analysis.SuggestedProbability = CalculateSuggestedProbability(deal, clientHistory);

                    return analysis;
                }
            }
            catch (Exception ex)
            {
                return new DealAnalysis 
                { 
                    ErrorMessage = $"Ошибка анализа сделки: {ex.Message}" 
                };
            }
        }

        private static int CalculateSuggestedProbability(Deal deal, List<Deal> clientHistory)
        {
            try
            {
                int baseProbability = deal.Probability;
                int adjustment = 0;

                // Положительные факторы
                if (clientHistory.Any(d => d.Status == DealStatus.Successful))
                    adjustment += 10;

                if (clientHistory.Count(d => d.Status == DealStatus.Successful) > clientHistory.Count / 2)
                    adjustment += 5;

                if (deal.Priority == Priority.High || deal.Priority == Priority.Critical)
                    adjustment += 5;

                // Отрицательные факторы
                var daysUntilDeadline = (deal.Deadline - DateTime.Now).Days;
                if (daysUntilDeadline < 0)
                    adjustment -= 20;
                else if (daysUntilDeadline < 3)
                    adjustment -= 10;

                var dealAge = (DateTime.Now - deal.CreatedAt).Days;
                if (dealAge > 60)
                    adjustment -= 10;
                else if (dealAge > 30)
                    adjustment -= 5;

                // Анализ суммы относительно истории
                if (clientHistory.Any(d => d.Status == DealStatus.Successful))
                {
                    var avgAmount = clientHistory.Where(d => d.Status == DealStatus.Successful).Average(d => d.Amount);
                    if (deal.Amount > avgAmount * 2)
                        adjustment -= 5;
                    else if (deal.Amount < avgAmount * 0.5m)
                        adjustment += 5;
                }

                return Math.Max(0, Math.Min(100, baseProbability + adjustment));
            }
            catch
            {
                return deal.Probability;
            }
        }
    }

    public class DealAnalysis
    {
        public string DealTitle { get; set; } = "";
        public int CurrentProbability { get; set; }
        public int SuggestedProbability { get; set; }
        public List<string> Insights { get; set; } = new List<string>();
        public List<string> Risks { get; set; } = new List<string>();
        public List<string> Recommendations { get; set; } = new List<string>();
        public string ErrorMessage { get; set; } = "";
    }
}
