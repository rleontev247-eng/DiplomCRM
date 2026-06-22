using System;
using System.Threading.Tasks;

namespace MyFirstCRM
{
    public class NotificationService : IDisposable
    {
        private readonly EmailService _emailService;
        private readonly SmsService _smsService;

        public NotificationService()
        {
            _emailService = new EmailService();
            _smsService = new SmsService();
        }

        public NotificationService(EmailService emailService, SmsService smsService)
        {
            _emailService = emailService ?? new EmailService();
            _smsService = smsService ?? new SmsService();
        }

        // Отправка уведомления через email
        public async Task<(bool Success, string ErrorMessage)> SendEmailNotificationAsync(
            string toEmail, 
            string subject, 
            string message, 
            NotificationType notificationType = NotificationType.Info)
        {
            try
            {
                if (!_emailService.IsConfigured())
                {
                    return (false, "Email сервис не настроен");
                }

                var formattedSubject = FormatSubject(subject, notificationType);
                var formattedMessage = FormatMessage(message, notificationType);

                return await _emailService.SendEmailAsync(toEmail, formattedSubject, formattedMessage);
            }
            catch (Exception ex)
            {
                return (false, $"Ошибка отправки email уведомления: {ex.Message}");
            }
        }

        // Отправка уведомления через SMS
        public async Task<(bool Success, string ErrorMessage)> SendSmsNotificationAsync(
            string phoneNumber, 
            string message, 
            NotificationType notificationType = NotificationType.Info)
        {
            try
            {
                if (!_smsService.IsConfigured())
                {
                    return (false, "SMS сервис не настроен");
                }

                var formattedMessage = FormatSmsMessage(message, notificationType);
                return await _smsService.SendSmsAsync(phoneNumber, formattedMessage);
            }
            catch (Exception ex)
            {
                return (false, $"Ошибка отправки SMS уведомления: {ex.Message}");
            }
        }

        // Отправка уведомления через оба канала
        public async Task<NotificationResult> SendMultiChannelNotificationAsync(
            string toEmail,
            string phoneNumber,
            string subject,
            string message,
            NotificationType notificationType = NotificationType.Info,
            bool preferEmail = true)
        {
            var result = new NotificationResult();

            // Определяем приоритетный канал
            var primaryChannel = preferEmail ? NotificationChannel.Email : NotificationChannel.SMS;
            var secondaryChannel = preferEmail ? NotificationChannel.SMS : NotificationChannel.Email;

            // Сначала пробуем отправить через приоритетный канал
            if (primaryChannel == NotificationChannel.Email)
            {
                var emailResult = await SendEmailNotificationAsync(toEmail, subject, message, notificationType);
                result.EmailSuccess = emailResult.Success;
                result.EmailError = emailResult.ErrorMessage;

                if (!emailResult.Success && !string.IsNullOrEmpty(phoneNumber))
                {
                    // Если email не отправился, пробуем SMS
                    var smsResult = await SendSmsNotificationAsync(phoneNumber, message, notificationType);
                    result.SmsSuccess = smsResult.Success;
                    result.SmsError = smsResult.ErrorMessage;
                }
            }
            else
            {
                var smsResult = await SendSmsNotificationAsync(phoneNumber, message, notificationType);
                result.SmsSuccess = smsResult.Success;
                result.SmsError = smsResult.ErrorMessage;

                if (!smsResult.Success && !string.IsNullOrEmpty(toEmail))
                {
                    // Если SMS не отправился, пробуем email
                    var emailResult = await SendEmailNotificationAsync(toEmail, subject, message, notificationType);
                    result.EmailSuccess = emailResult.Success;
                    result.EmailError = emailResult.ErrorMessage;
                }
            }

            // Если оба канала настроены, отправляем через оба для надежности
            if (_emailService.IsConfigured() && _smsService.IsConfigured() && 
                !string.IsNullOrEmpty(toEmail) && !string.IsNullOrEmpty(phoneNumber))
            {
                if (primaryChannel == NotificationChannel.Email && result.EmailSuccess)
                {
                    // Email отправился успешно, отправляем также SMS как дубликат
                    var smsResult = await SendSmsNotificationAsync(phoneNumber, message, notificationType);
                    result.SmsSuccess = smsResult.Success;
                    result.SmsError = smsResult.ErrorMessage;
                }
                else if (primaryChannel == NotificationChannel.SMS && result.SmsSuccess)
                {
                    // SMS отправился успешно, отправляем также email как дубликат
                    var emailResult = await SendEmailNotificationAsync(toEmail, subject, message, notificationType);
                    result.EmailSuccess = emailResult.Success;
                    result.EmailError = emailResult.ErrorMessage;
                }
            }

            result.OverallSuccess = result.EmailSuccess || result.SmsSuccess;
            return result;
        }

        // Отправка уведомления о сделке
        public async Task<NotificationResult> SendDealNotificationAsync(
            string toEmail,
            string phoneNumber,
            string clientName,
            string dealTitle,
            DateTime? dueDate,
            NotificationType notificationType,
            bool preferEmail = true)
        {
            var subject = GetDealSubject(dealTitle, notificationType);
            var message = GetDealMessage(clientName, dealTitle, dueDate, notificationType);

            return await SendMultiChannelNotificationAsync(
                toEmail, phoneNumber, subject, message, notificationType, preferEmail);
        }

        // Отправка уведомления о сроке
        public async Task<NotificationResult> SendDeadlineNotificationAsync(
            string toEmail,
            string phoneNumber,
            string taskTitle,
            DateTime deadline,
            NotificationType notificationType,
            bool preferEmail = true)
        {
            var subject = GetDeadlineSubject(taskTitle, notificationType);
            var message = GetDeadlineMessage(taskTitle, deadline, notificationType);

            return await SendMultiChannelNotificationAsync(
                toEmail, phoneNumber, subject, message, notificationType, preferEmail);
        }

        // Проверка конфигурации сервисов
        public NotificationConfigurationStatus GetConfigurationStatus()
        {
            return new NotificationConfigurationStatus
            {
                EmailConfigured = _emailService.IsConfigured(),
                SmsConfigured = _smsService.IsConfigured(),
                EmailInfo = _emailService.GetConfigurationInfo(),
                SmsInfo = _smsService.GetConfigurationInfo()
            };
        }

        // Тестирование соединений
        public async Task<NotificationTestResult> TestConnectionsAsync()
        {
            var result = new NotificationTestResult();

            if (_emailService.IsConfigured())
            {
                var emailTest = await _emailService.TestConnectionAsync();
                result.EmailTestSuccess = emailTest.Success;
                result.EmailTestMessage = emailTest.Message;
            }
            else
            {
                result.EmailTestSuccess = false;
                result.EmailTestMessage = "Email не настроен";
            }

            if (_smsService.IsConfigured())
            {
                var smsTest = await _smsService.TestConnectionAsync();
                result.SmsTestSuccess = smsTest.Success;
                result.SmsTestMessage = smsTest.Message;
            }
            else
            {
                result.SmsTestSuccess = false;
                result.SmsTestMessage = "SMS не настроен";
            }

            return result;
        }

        // Приватные методы форматирования
        private string FormatSubject(string subject, NotificationType type)
        {
            var prefix = type switch
            {
                NotificationType.DealReminder => "📋 Напоминание о сделке",
                NotificationType.Deadline => "⏰ Срок выполнения",
                NotificationType.ClientAction => "👤 Действие с клиентом",
                NotificationType.System => "⚙️ Системное уведомление",
                NotificationType.Success => "✅ Успешное выполнение",
                NotificationType.Warning => "⚠️ Внимание",
                NotificationType.Info => "ℹ️ Информация",
                _ => "🔔 Уведомление CRM"
            };

            return $"{prefix}: {subject}";
        }

        private string FormatMessage(string message, NotificationType type)
        {
            var typeInfo = type switch
            {
                NotificationType.DealReminder => "Это напоминание о предстоящей или текущей сделке.",
                NotificationType.Deadline => "Это уведомление о приближающемся сроке выполнения задачи.",
                NotificationType.ClientAction => "Это уведомление о действии, связанном с клиентом.",
                NotificationType.System => "Это системное уведомление от CRM.",
                NotificationType.Success => "Операция выполнена успешно.",
                NotificationType.Warning => "Требуется ваше внимание.",
                NotificationType.Info => "Информационное сообщение.",
                _ => "Уведомление от системы CRM."
            };

            return $"{message}\n\n---\n{typeInfo}\nВремя: {DateTime.Now:dd.MM.yyyy HH:mm}";
        }

        private string FormatSmsMessage(string message, NotificationType type)
        {
            // SMS сообщения должны быть короче и без HTML
            var prefix = type switch
            {
                NotificationType.DealReminder => "📋",
                NotificationType.Deadline => "⏰",
                NotificationType.ClientAction => "👤",
                NotificationType.System => "⚙️",
                NotificationType.Success => "✅",
                NotificationType.Warning => "⚠️",
                NotificationType.Info => "ℹ️",
                _ => "🔔"
            };

            // Ограничиваем длину SMS (обычно 160 символов для латиницы, 70 для кириллицы)
            var fullMessage = $"{prefix} {message}";
            if (fullMessage.Length > 150)
            {
                fullMessage = fullMessage.Substring(0, 147) + "...";
            }

            return fullMessage;
        }

        private string GetDealSubject(string dealTitle, NotificationType type)
        {
            return $"Сделка: {dealTitle}";
        }

        private string GetDealMessage(string clientName, string dealTitle, DateTime? dueDate, NotificationType type)
        {
            var message = $"Клиент: {clientName}\nСделка: {dealTitle}";
            
            if (dueDate.HasValue)
            {
                message += $"\nСрок: {dueDate.Value:dd.MM.yyyy HH:mm}";
            }

            return message;
        }

        private string GetDeadlineSubject(string taskTitle, NotificationType type)
        {
            return $"Срок: {taskTitle}";
        }

        private string GetDeadlineMessage(string taskTitle, DateTime deadline, NotificationType type)
        {
            return $"Задача: {taskTitle}\nСрок выполнения: {deadline:dd.MM.yyyy HH:mm}";
        }

        public void Dispose()
        {
            _emailService?.Dispose();
            _smsService?.Dispose();
        }
    }

    // Вспомогательные классы
    public class NotificationResult
    {
        public bool OverallSuccess { get; set; }
        public bool EmailSuccess { get; set; }
        public bool SmsSuccess { get; set; }
        public string EmailError { get; set; } = string.Empty;
        public string SmsError { get; set; } = string.Empty;
    }

    public class NotificationConfigurationStatus
    {
        public bool EmailConfigured { get; set; }
        public bool SmsConfigured { get; set; }
        public string EmailInfo { get; set; } = string.Empty;
        public string SmsInfo { get; set; } = string.Empty;
    }

    public class NotificationTestResult
    {
        public bool EmailTestSuccess { get; set; }
        public bool SmsTestSuccess { get; set; }
        public string EmailTestMessage { get; set; } = string.Empty;
        public string SmsTestMessage { get; set; } = string.Empty;
    }

    public enum NotificationChannel
    {
        Email,
        SMS,
        Both
    }
}
