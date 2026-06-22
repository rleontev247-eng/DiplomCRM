using System.ComponentModel.DataAnnotations;

namespace MyFirstCRM
{
    public class Client
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Имя обязательно!")]
        [Display(Name = "ФИО")]
        public string Name { get; set; } = "";

        [Display(Name = "Телефон")]
        public string? Phone { get; set; }

        [EmailAddress(ErrorMessage = "Неверный формат email")]
        [Display(Name = "Email")]
        public string? Email { get; set; }

        [Display(Name = "Дата добавления")]
        public DateTime CreatedAt { get; set; }

        public int CompanyId { get; set; }

        [Display(Name = "Создано пользователем")]
        public int? CreatedByUserId { get; set; }

        // Навигационное свойство для пользователя, создавшего клиента
        public User? CreatedByUser { get; set; }

        [Display(Name = "Обновлено пользователем")]
        public int? UpdatedByUserId { get; set; }

        // Навигационное свойство для пользователя, обновившего клиента
        public User? UpdatedByUser { get; set; }

        [Display(Name = "Примечания")]
        public string? Notes { get; set; }

        [Display(Name = "ABC категория")]
        public string? ABC_Category { get; set; }

        // Проверка на дубликат (по телефону или email)
        public bool IsDuplicateOf(Client other)
        {
            if (!string.IsNullOrEmpty(Phone) && !string.IsNullOrEmpty(other.Phone) &&
                Phone.Trim() == other.Phone.Trim())
                return true;

            if (!string.IsNullOrEmpty(Email) && !string.IsNullOrEmpty(other.Email) &&
                Email.Trim().ToLower() == other.Email.Trim().ToLower())
                return true;

            return false;
        }
    }
}