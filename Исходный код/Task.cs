using System;
using System.ComponentModel.DataAnnotations;

namespace MyFirstCRM
{
    public class CRMTask
    {
        public int Id { get; set; }

        [Required]
        public string Title { get; set; } = "";

        public string Description { get; set; } = "";

        public DateTime CreatedAt { get; set; }

        public DateTime? DueDate { get; set; }

        public TaskStatus Status { get; set; }

        public TaskPriority Priority { get; set; }

        public int? DealId { get; set; }
        public Deal? Deal { get; set; }

        public int? ClientId { get; set; }
        public Client? Client { get; set; }

        public string AssignedTo { get; set; } = "";

        public int CompanyId { get; set; }

        // Пользователь, создавший задачу
        public int? CreatedByUserId { get; set; }

        // Навигационное свойство для пользователя, создавшего задачу
        public User? CreatedByUser { get; set; }

        // Пользователь, назначенный на задачу (ID)
        public int? AssignedToUserId { get; set; }

        // Навигационное свойство для ответственного пользователя
        public User? AssignedToUser { get; set; }

        [Display(Name = "Обновлено пользователем")]
        public int? UpdatedByUserId { get; set; }

        // Навигационное свойство для пользователя, обновившего задачу
        public User? UpdatedByUser { get; set; }

        public DateTime? CompletedAt { get; set; }
    }

    public enum TaskStatus
    {
        Todo,        // К выполнению
        InProgress,  // В работе
        Completed,   // Завершено
        Cancelled    // Отменено
    }

    public enum TaskPriority
    {
        Low,
        Medium,
        High,
        Urgent
    }

    // История взаимодействий
    public class Interaction
    {
        public int Id { get; set; }

        public int ClientId { get; set; }
        public Client? Client { get; set; }

        public int? DealId { get; set; }
        public Deal? Deal { get; set; }

        public DateTime DateTime { get; set; }

        public InteractionType Type { get; set; }

        public string Description { get; set; } = "";

        public string Outcome { get; set; } = "";

        public string EmployeeName { get; set; } = "";

        public int CompanyId { get; set; }

        // Пользователь, создавший взаимодействие
        public int? UserId { get; set; }

        // Навигационное свойство для пользователя
        public User? User { get; set; }
    }

    public enum InteractionType
    {
        Call,        // Звонок
        Email,       // Email
        Meeting,     // Встреча
        Note,        // Заметка
        Task         // Задача
    }
}