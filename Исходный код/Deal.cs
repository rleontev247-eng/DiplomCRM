using System;
using System.ComponentModel.DataAnnotations;

namespace MyFirstCRM
{
    public class Deal
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Название сделки обязательно!")]
        [Display(Name = "Название сделки")]
        public string Title { get; set; } = "";

        [Display(Name = "Описание")]
        public string? Description { get; set; }

        [Display(Name = "Сумма")]
        public decimal Amount { get; set; }

        [Display(Name = "Статус")]
        public DealStatus Status { get; set; }

        [Display(Name = "Дата создания")]
        public DateTime CreatedAt { get; set; }

        [Display(Name = "Дата обновления")]
        public DateTime UpdatedAt { get; set; }

        [Display(Name = "Дата закрытия")]
        public DateTime? ClosedAt { get; set; }

        [Display(Name = "Срок")]
        public DateTime Deadline { get; set; }

        // Внешний ключ для клиента
        public int ClientId { get; set; }
        public Client? Client { get; set; }

        public int CompanyId { get; set; }

        [Display(Name = "Создано пользователем")]
        public int? CreatedByUserId { get; set; }

        // Навигационное свойство для пользователя, создавшего сделку
        public User? CreatedByUser { get; set; }

        [Display(Name = "Назначено пользователю")]
        public int? AssignedToUserId { get; set; }

        // Навигационное свойство для ответственного пользователя
        public User? AssignedToUser { get; set; }

        [Display(Name = "Обновлено пользователем")]
        public int? UpdatedByUserId { get; set; }

        // Навигационное свойство для пользователя, обновившего сделку
        public User? UpdatedByUser { get; set; }

        [Display(Name = "Вероятность")]
        public int Probability { get; set; } = 0;

        [Display(Name = "Категория")]
        public string? Category { get; set; }

        [Display(Name = "Приоритет")]
        public Priority Priority { get; set; } = Priority.Medium;
    }

    public enum DealStatus
    {
        New,        // Новая
        InProgress, // В работе
        Successful, // Успешная
        Failed      // Проваленная
    }

    public enum Priority
    {
        Low,      // Низкий
        Medium,   // Средний
        High,     // Высокий
        Critical  // Критический
    }
}