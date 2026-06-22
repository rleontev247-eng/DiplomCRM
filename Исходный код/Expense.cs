using System;
using System.ComponentModel.DataAnnotations;

namespace MyFirstCRM
{
    public class Expense
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Название расхода обязательно!")]
        [Display(Name = "Наименование")]
        public string Title { get; set; } = "";

        [Display(Name = "Категория")]
        public string Category { get; set; } = "Прочее";

        [Required(ErrorMessage = "Сумма обязательна!")]
        [Display(Name = "Сумма")]
        public decimal Amount { get; set; }

        [Display(Name = "Дата")]
        public DateTime Date { get; set; }

        [Display(Name = "Примечание")]
        public string? Notes { get; set; }

        public int CompanyId { get; set; }

        [Display(Name = "Создано пользователем")]
        public int? CreatedByUserId { get; set; }

        // Навигационное свойство для пользователя, создавшего расход
        public User? CreatedByUser { get; set; }

        [Display(Name = "Дата создания")]
        public DateTime CreatedAt { get; set; }

        // Внешний ключ для клиента
        public int? ClientId { get; set; }

        [Display(Name = "Обновлено пользователем")]
        public int? UpdatedByUserId { get; set; }

        // Навигационное свойство для пользователя, обновившего расход
        public User? UpdatedByUser { get; set; }

        [Display(Name = "Тип")]
        public ExpenseType Type { get; set; } = ExpenseType.Other;
    }

    public enum ExpenseType
    {
        Rent,           // Аренда
        Salary,         // Зарплата
        Materials,      // Материалы
        Marketing,      // Маркетинг
        Taxes,          // Налоги
        Utilities,      // Коммунальные услуги
        Equipment,      // Оборудование
        Transport,      // Транспорт
        Other           // Прочее
    }
}