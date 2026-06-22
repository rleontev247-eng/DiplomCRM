using System;

namespace MyFirstCRM
{
    /// <summary>
    /// Модель для логирования Email сообщений
    /// </summary>
    public class EmailMessage
    {
        public int Id { get; set; }
        
        // Основная информация
        public string ToEmail { get; set; } = "";
        public string Subject { get; set; } = "";
        public string Body { get; set; } = "";
        public string FromEmail { get; set; } = "";
        
        // Статус и результаты
        public EmailMessageStatus Status { get; set; }
        public string? ErrorMessage { get; set; }
        public string? MessageId { get; set; } // ID сообщения от почтового сервера
        
        // SMTP настройки (для логирования)
        public string SmtpHost { get; set; } = "";
        public int SmtpPort { get; set; }
        public bool UseSsl { get; set; }
        
        // Связи с другими сущностями
        public int? DealId { get; set; }
        public int? ClientId { get; set; }
        public int? NotificationId { get; set; }
        
        // Метаданные
        public DateTime CreatedAt { get; set; }
        public DateTime? SentAt { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public int AttachmentCount { get; set; }
        public long MessageSize { get; set; } // Размер в байтах
        
        // Компания
        public int CompanyId { get; set; }
        
        // Навигационные свойства
        public Deal? Deal { get; set; }
        public Client? Client { get; set; }
        public Notification? Notification { get; set; }
    }

    /// <summary>
    /// Статусы Email сообщения
    /// </summary>
    public enum EmailMessageStatus
    {
        Pending,        // Ожидает отправки
        Sent,           // Отправлено
        Delivered,      // Доставлено
        Failed,         // Ошибка отправки
        Rejected,       // Отклонено почтовым сервером
        Bounced,        // Вернулось (неверный адрес)
        Unknown         // Неизвестный статус
    }
}
