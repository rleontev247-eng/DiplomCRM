using System;

namespace MyFirstCRM
{
    /// <summary>
    /// Модель для логирования SMS сообщений
    /// </summary>
    public class SmsMessage
    {
        public int Id { get; set; }
        
        // Основная информация
        public string PhoneNumber { get; set; } = "";
        public string Message { get; set; } = "";
        public string SenderName { get; set; } = "";
        
        // Статус и результаты
        public SmsMessageStatus Status { get; set; }
        public string? ProviderMessageId { get; set; }
        public string? ErrorMessage { get; set; }
        
        // Провайдер
        public SmsProvider Provider { get; set; }
        
        // Связи с другими сущностями
        public int? DealId { get; set; }
        public int? ClientId { get; set; }
        public int? NotificationId { get; set; }
        
        // Метаданные
        public DateTime CreatedAt { get; set; }
        public DateTime? SentAt { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public decimal Cost { get; set; }
        public int CharacterCount { get; set; }
        public int SmsCount { get; set; } // Количество SMS частей (если сообщение длинное)
        
        // Компания
        public int CompanyId { get; set; }
        
        // Навигационные свойства
        public Deal? Deal { get; set; }
        public Client? Client { get; set; }
        public Notification? Notification { get; set; }
    }

    /// <summary>
    /// Статусы SMS сообщения
    /// </summary>
    public enum SmsMessageStatus
    {
        Pending,        // Ожидает отправки
        Sent,           // Отправлено
        Delivered,      // Доставлено
        Failed,         // Ошибка отправки
        Rejected,       // Отклонено провайдером
        Expired,        // Истекло время доставки
        Unknown         // Неизвестный статус
    }
}
