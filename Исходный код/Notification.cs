using System;

namespace MyFirstCRM
{
    public class Notification
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string Message { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        public DateTime? DueDate { get; set; }
        public bool IsRead { get; set; }
        public NotificationType Type { get; set; }
        public int? DealId { get; set; } // Ссылка на сделку
        public int? ClientId { get; set; } // Ссылка на клиента

        public int CompanyId { get; set; }

        // Создатель уведомления
        public int? CreatedByUserId { get; set; }

        public string Icon { get; set; } = "🔔";
        public string Color { get; set; } = "#3B82F6";
    }

    public enum NotificationType
    {
        DealReminder,    // Напоминание о сделке
        Deadline,        // Сроки
        ClientAction,    // Действия с клиентом
        System,          // Системное уведомление
        Success,         // Успех
        Warning,         // Предупреждение
        Info            // Информация
    }
}