using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace MyFirstCRM
{
    public static class NotificationManager
    {
        private static readonly List<Notification> _pendingNotifications = new List<Notification>();

        // Загрузка всех уведомлений
        public static List<Notification> LoadNotifications(bool onlyUnread = false)
        {
            try
            {
                using (var context = MultiUserSecurityManager.CreateCompanyContext())
                {
                    // Проверяем, существует ли таблица
                    var tableExists = context.Database.CanConnect() &&
                                    context.Model.GetEntityTypes().Any(e => e.ClrType == typeof(Notification));

                    if (!tableExists)
                    {
                        Console.WriteLine("Таблица Notifications не существует, создаем...");
                        context.Database.EnsureCreated();
                        return new List<Notification>();
                    }

                    var query = context.Notifications.OrderByDescending(n => n.CreatedAt).AsQueryable();

                    if (onlyUnread)
                        query = query.Where(n => !n.IsRead);

                    return query.ToList();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка загрузки уведомлений: {ex.Message}");
                return new List<Notification>();
            }
        }

        // Создание уведомления
        public static void CreateNotification(Notification notification)
        {
            var settings = AppSettings.Load();
            
            // Проверяем включены ли уведомления
            if (!settings.ShowNotifications)
                return;
                
            using (var context = MultiUserSecurityManager.CreateCompanyContext())
            {
                notification.CreatedAt = DateTime.Now;
                context.Notifications.Add(notification);
                context.SaveChanges();
                
                // Показываем всплывающее уведомление если включены звуковые уведомления
                if (settings.SoundNotifications)
                {
                    ShowToastNotification(notification);
                }
            }
        }

        // Показать всплывающее уведомление
        private static void ShowToastNotification(Notification notification)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var toast = new System.Windows.Controls.ToolTip
                    {
                        Content = $"{notification.Icon} {notification.Title}\n{notification.Message}",
                        Background = new System.Windows.Media.SolidColorBrush(
                            System.Windows.Media.Color.FromRgb(243, 244, 246)),
                        Foreground = System.Windows.Media.Brushes.Black,
                        Padding = new System.Windows.Thickness(15),
                        FontSize = 14,
                        MaxWidth = 400
                    };

                    // Проигрываем системный звук уведомления
                    System.Media.SystemSounds.Beep.Play();
                    
                    // Показываем уведомление на 3 секунды
                    var timer = new System.Windows.Threading.DispatcherTimer
                    {
                        Interval = TimeSpan.FromSeconds(3)
                    };
                    timer.Tick += (s, e) =>
                    {
                        timer.Stop();
                        toast.IsOpen = false;
                    };
                    
                    toast.IsOpen = true;
                    timer.Start();
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка показа уведомления: {ex.Message}");
            }
        }

        // Отметить как прочитанное
        public static void MarkAsRead(int notificationId)
        {
            using (var context = MultiUserSecurityManager.CreateCompanyContext())
            {
                var notification = context.Notifications.Find(notificationId);
                if (notification != null)
                {
                    notification.IsRead = true;
                    context.SaveChanges();
                }
            }
        }

        // Отметить все как прочитанные
        public static void MarkAllAsRead()
        {
            using (var context = MultiUserSecurityManager.CreateCompanyContext())
            {
                var unreadNotifications = context.Notifications.Where(n => !n.IsRead).ToList();
                foreach (var notification in unreadNotifications)
                {
                    notification.IsRead = true;
                }
                context.SaveChanges();
            }
        }

        // Удалить уведомление
        public static void DeleteNotification(int notificationId)
        {
            using (var context = MultiUserSecurityManager.CreateCompanyContext())
            {
                var notification = context.Notifications.Find(notificationId);
                if (notification != null)
                {
                    context.Notifications.Remove(notification);
                    context.SaveChanges();
                }
            }
        }

        // Проверить предстоящие сделки и создать напоминания
        public static void CheckDealReminders()
        {
            var settings = AppSettings.Load();
            
            // Если уведомления отключены, не выполняем проверку
            if (!settings.ShowNotifications)
                return;
                
            using (var context = MultiUserSecurityManager.CreateCompanyContext())
            {
                var today = DateTime.Now.Date;
                var tomorrow = today.AddDays(1);
                var threeDays = today.AddDays(3);
                var week = today.AddDays(7);

                // Сделки с дедлайном сегодня
                var todayDeals = context.Deals
                    .Include(d => d.Client)
                    .Where(d => d.Deadline.Date == today && d.Status != DealStatus.Successful && d.Status != DealStatus.Failed)
                    .ToList();

                foreach (var deal in todayDeals)
                {
                    // Проверим, не создано ли уже уведомление на сегодня
                    var exists = context.Notifications.Any(n =>
                        n.DealId == deal.Id &&
                        n.DueDate.HasValue &&
                        n.DueDate.Value.Date == today &&
                        n.Type == NotificationType.Deadline);

                    if (!exists)
                    {
                        var notification = new Notification
                        {
                            Title = $"🔴 СРОЧНО: Срок сделки сегодня!",
                            Message = $"Сделка '{deal.Title}' должна быть завершена сегодня. Клиент: {deal.Client?.Name ?? "Не указан"}",
                            Type = NotificationType.Deadline,
                            DealId = deal.Id,
                            ClientId = deal.ClientId,
                            DueDate = today,
                            Icon = "⏰",
                            Color = "#EF4444"
                        };
                        CreateNotification(notification);
                    }
                }

                // Сделки с дедлайном завтра
                var tomorrowDeals = context.Deals
                    .Include(d => d.Client)
                    .Where(d => d.Deadline.Date == tomorrow && d.Status != DealStatus.Successful && d.Status != DealStatus.Failed)
                    .ToList();

                foreach (var deal in tomorrowDeals)
                {
                    var exists = context.Notifications.Any(n =>
                        n.DealId == deal.Id &&
                        n.DueDate.HasValue &&
                        n.DueDate.Value.Date == tomorrow &&
                        n.Type == NotificationType.DealReminder);

                    if (!exists)
                    {
                        var notification = new Notification
                        {
                            Title = $"🟡 Напоминание: Срок сделки завтра",
                            Message = $"Сделка '{deal.Title}' должна быть завершена завтра. Клиент: {deal.Client?.Name ?? "Не указан"}",
                            Type = NotificationType.DealReminder,
                            DealId = deal.Id,
                            ClientId = deal.ClientId,
                            DueDate = tomorrow,
                            Icon = "📅",
                            Color = "#F59E0B"
                        };
                        CreateNotification(notification);
                    }
                }

                // Сделки с дедлайном через 3 дня
                var threeDaysDeals = context.Deals
                    .Include(d => d.Client)
                    .Where(d => d.Deadline.Date == threeDays && d.Status != DealStatus.Successful && d.Status != DealStatus.Failed)
                    .ToList();

                foreach (var deal in threeDaysDeals)
                {
                    var exists = context.Notifications.Any(n =>
                        n.DealId == deal.Id &&
                        n.DueDate.HasValue &&
                        n.DueDate.Value.Date == threeDays &&
                        n.Type == NotificationType.DealReminder);

                    if (!exists)
                    {
                        var notification = new Notification
                        {
                            Title = $"🔵 Сделка через 3 дня",
                            Message = $"Сделка '{deal.Title}' должна быть завершена через 3 дня. Клиент: {deal.Client?.Name ?? "Не указан"}",
                            Type = NotificationType.DealReminder,
                            DealId = deal.Id,
                            ClientId = deal.ClientId,
                            DueDate = threeDays,
                            Icon = "💼",
                            Color = "#3B82F6"
                        };
                        CreateNotification(notification);
                    }
                }
            }
        }

        // Создать уведомление о завершении сделки
        public static void CreateDealCompletedNotification(Deal deal)
        {
            var notification = new Notification
            {
                Title = $"✅ Сделка завершена: {deal.Amount:N0} ₽",
                Message = $"Сделка '{deal.Title}' успешно завершена. Клиент: {deal.Client?.Name ?? "Не указан"}",
                Type = deal.Status == DealStatus.Successful ? NotificationType.Success : NotificationType.Warning,
                DealId = deal.Id,
                ClientId = deal.ClientId,
                Icon = deal.Status == DealStatus.Successful ? "💰" : "❌",
                Color = deal.Status == DealStatus.Successful ? "#10B981" : "#EF4444"
            };
            CreateNotification(notification);
        }

        // Создать уведомление о новом клиенте
        public static void CreateNewClientNotification(Client client)
        {
            var notification = new Notification
            {
                Title = "👥 Добавлен новый клиент",
                Message = $"Добавлен клиент: {client.Name}",
                Type = NotificationType.ClientAction,
                ClientId = client.Id,
                Icon = "👤",
                Color = "#8B5CF6"
            };
            CreateNotification(notification);
        }

        // Получить количество непрочитанных уведомлений
        public static int GetUnreadCount()
        {
            using (var context = MultiUserSecurityManager.CreateCompanyContext())
            {
                return context.Notifications.Count(n => !n.IsRead);
            }
        }

        // Очистить старые уведомления (старше 30 дней)
        public static void CleanOldNotifications()
        {
            using (var context = MultiUserSecurityManager.CreateCompanyContext())
            {
                var oldDate = DateTime.Now.AddDays(-30);
                var oldNotifications = context.Notifications
                    .Where(n => n.CreatedAt < oldDate)
                    .ToList();

                if (oldNotifications.Any())
                {
                    context.Notifications.RemoveRange(oldNotifications);
                    context.SaveChanges();
                }
            }
        }

        // Отправить email уведомление
        public static async Task<bool> SendEmailNotificationAsync(string toEmail, string subject, string message, NotificationType type = NotificationType.Info)
        {
            try
            {
                var settings = AppSettings.Load();
                if (string.IsNullOrEmpty(settings.SmtpUser) || string.IsNullOrEmpty(settings.SmtpPassword))
                {
                    return false;
                }

                var emailService = new EmailService(settings.SmtpHost, settings.SmtpPort, settings.SmtpUser, settings.SmtpPassword, settings.UseSsl);
                var result = await emailService.SendEmailAsync(toEmail, subject, message);
                return result.Success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка отправки email: {ex.Message}");
                return false;
            }
        }

        // Отправить SMS уведомление
        public static async Task<bool> SendSmsNotificationAsync(string phoneNumber, string message, NotificationType type = NotificationType.Info)
        {
            try
            {
                var settings = AppSettings.Load();
                if (string.IsNullOrEmpty(settings.SmsApiKey))
                {
                    return false;
                }

                var smsService = new SmsService(settings.SmsApiKey, settings.SmsSenderName, Enum.Parse<SmsProvider>(settings.SmsProvider));
                var result = await smsService.SendSmsAsync(phoneNumber, message);
                return result.Success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка отправки SMS: {ex.Message}");
                return false;
            }
        }

        // Отправить мультиканальное уведомление (Email + SMS)
        public static async Task<NotificationResult> SendMultiChannelNotificationAsync(string toEmail, string phoneNumber, string subject, string message, NotificationType type = NotificationType.Info)
        {
            try
            {
                var settings = AppSettings.Load();
                var emailService = !string.IsNullOrEmpty(settings.SmtpUser) && !string.IsNullOrEmpty(settings.SmtpPassword) 
                    ? new EmailService(settings.SmtpHost, settings.SmtpPort, settings.SmtpUser, settings.SmtpPassword, settings.UseSsl)
                    : null;

                var smsService = !string.IsNullOrEmpty(settings.SmsApiKey)
                    ? new SmsService(settings.SmsApiKey, settings.SmsSenderName, Enum.Parse<SmsProvider>(settings.SmsProvider))
                    : null;

                var notificationService = new NotificationService(emailService, smsService);
                return await notificationService.SendMultiChannelNotificationAsync(toEmail, phoneNumber, subject, message, type, settings.PreferEmailForNotifications);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка отправки мультиканального уведомления: {ex.Message}");
                return new NotificationResult { OverallSuccess = false, EmailError = ex.Message, SmsError = ex.Message };
            }
        }

        // Отправить уведомление о сделке клиенту
        public static async Task SendDealNotificationToClientAsync(Deal deal, string subject, string message, NotificationType type = NotificationType.Info)
        {
            if (deal.Client == null) return;

            var settings = AppSettings.Load();
            
            // Определяем куда отправлять
            string email = !string.IsNullOrEmpty(deal.Client.Email) ? deal.Client.Email : "";
            string phone = !string.IsNullOrEmpty(deal.Client.Phone) ? deal.Client.Phone : "";

            if (settings.SendBothChannels && !string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(phone))
            {
                // Отправляем через оба канала
                await SendMultiChannelNotificationAsync(email, phone, subject, message, type);
            }
            else if (settings.PreferEmailForNotifications && !string.IsNullOrEmpty(email))
            {
                // Предпочитаем email
                await SendEmailNotificationAsync(email, subject, message, type);
            }
            else if (!string.IsNullOrEmpty(phone))
            {
                // Отправляем SMS
                await SendSmsNotificationAsync(phone, message, type);
            }
        }

        // Отправить напоминание о сделке
        public static async Task SendDealReminderAsync(Deal deal, int daysBefore = 1)
        {
            if (deal.Client == null) return;

            var urgency = daysBefore switch
            {
                0 => "СРОЧНО: ",
                1 => "Напоминание: ",
                _ => "Предупреждение: "
            };

            var subject = $"{urgency}Срок сделки {deal.Title}";
            var message = $"Уважаемый клиент! Напоминаем о сделке '{deal.Title}'";
            
            if (daysBefore == 0)
                message += " которая должна быть завершена сегодня!";
            else if (daysBefore == 1)
                message += " которая должна быть завершена завтра!";
            else
                message += $" которая должна быть завершена через {daysBefore} дней!";

            message += $"\n\nСумма: {deal.Amount:N0} ₽";
            message += $"\nКрайний срок: {deal.Deadline:dd.MM.yyyy HH:mm}";

            await SendDealNotificationToClientAsync(deal, subject, message, daysBefore == 0 ? NotificationType.Deadline : NotificationType.DealReminder);
        }
    }
}