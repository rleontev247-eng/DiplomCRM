using System;
using System.ComponentModel.DataAnnotations;

namespace MyFirstCRM
{
    public class CalendarEvent
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Название события обязательно!")]
        [Display(Name = "Название события")]
        public string Title { get; set; } = "";

        [Display(Name = "Описание")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Дата начала обязательна!")]
        [Display(Name = "Дата начала")]
        public DateTime StartDate { get; set; }

        [Required(ErrorMessage = "Дата окончания обязательна!")]
        [Display(Name = "Дата окончания")]
        public DateTime EndDate { get; set; }

        [Display(Name = "Весь день")]
        public bool IsAllDay { get; set; } = false;

        [Display(Name = "Место проведения")]
        public string? Location { get; set; }

        [Display(Name = "Тип события")]
        public CalendarEventType EventType { get; set; } = CalendarEventType.Other;

        [Display(Name = "Приоритет")]
        public Priority Priority { get; set; } = Priority.Medium;

        [Display(Name = "Цвет")]
        public string Color { get; set; } = "#3498db";

        [Display(Name = "Напоминание за")]
        public int ReminderMinutes { get; set; } = 15;

        [Display(Name = "Создано")]
        public DateTime CreatedAt { get; set; }

        [Display(Name = "Обновлено")]
        public DateTime UpdatedAt { get; set; }

        // Связь со сделкой
        public int? DealId { get; set; }
        public Deal? Deal { get; set; }

        // Связь с клиентом
        public int? ClientId { get; set; }
        public Client? Client { get; set; }

        public int CompanyId { get; set; }

        // Пользователь, создавший событие
        public int? CreatedByUserId { get; set; }

        // Навигационное свойство для пользователя, создавшего событие
        public User? CreatedByUser { get; set; }

        // Пользователь, назначенный на событие (если применимо)
        public int? AssignedToUserId { get; set; }

        // Навигационное свойство для ответственного пользователя
        public User? AssignedToUser { get; set; }

        [Display(Name = "Обновлено пользователем")]
        public int? UpdatedByUserId { get; set; }

        // Навигационное свойство для пользователя, обновившего событие
        public User? UpdatedByUser { get; set; }

        [Display(Name = "Статус")]
        public EventStatus Status { get; set; } = EventStatus.Scheduled;
    }

    public enum CalendarEventType
    {
        Meeting,        // Встреча
        Call,          // Звонок
        Task,          // Задача
        Deadline,      // Дедлайн
        Presentation,  // Презентация
        FollowUp,      // Последующее действие
        Other          // Другое
    }

    public enum EventStatus
    {
        Scheduled,     // Запланировано
        InProgress,    // В процессе
        Completed,     // Завершено
        Cancelled      // Отменено
    }
}
