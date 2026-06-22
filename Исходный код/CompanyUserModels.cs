using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MyFirstCRM
{
    /// <summary>
    /// Компания - верхний уровень изоляции данных
    /// Каждая компания имеет свою изолированную базу данных
    /// </summary>
    public class Company
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(200)]
        public string? ContactEmail { get; set; }

        [MaxLength(50)]
        public string? ContactPhone { get; set; }

        [MaxLength(300)]
        public string? Address { get; set; }

        /// <summary>
        /// Уникальный идентификатор компании для синхронизации/подключения
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string CompanyCode { get; set; } = Guid.NewGuid().ToString("N")[..8].ToUpper();

        /// <summary>
        /// Путь к базе данных компании (относительно или абсолютный)
        /// </summary>
        [Required]
        [MaxLength(500)]
        public string DatabasePath { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        /// <summary>
        /// Активна ли компания (можно отключить при задолженности и т.д.)
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Максимальное количество пользователей (-1 = без ограничений)
        /// </summary>
        public int MaxUsers { get; set; } = 10;

        /// <summary>
        /// Дата окончания лицензии
        /// </summary>
        public DateTime? LicenseExpiryDate { get; set; }

        // Навигационное свойство
        public virtual ICollection<User> Users { get; set; } = new List<User>();
    }

    /// <summary>
    /// Роли пользователей в системе
    /// </summary>
    public enum UserRole
    {
        /// <summary>
        /// Администратор компании - полный доступ, управление пользователями
        /// </summary>
        Admin = 0,

        /// <summary>
        /// Менеджер - доступ к клиентам, сделкам, отчетам. Нет доступа к управлению пользователями
        /// </summary>
        Manager = 1,

        /// <summary>
        /// Сотрудник - базовый доступ, ограниченные права на редактирование
        /// </summary>
        Employee = 2,

        /// <summary>
        /// Только просмотр - доступ только на чтение данных
        /// </summary>
        Viewer = 3
    }

    /// <summary>
    /// Пользователь системы - сотрудник компании
    /// </summary>
    public class User
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// ID компании, к которой принадлежит пользователь
        /// </summary>
        public int CompanyId { get; set; }

        [Required]
        [MaxLength(100)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string FullName { get; set; } = string.Empty;

        [MaxLength(200)]
        public string? Email { get; set; }

        [MaxLength(50)]
        public string? Phone { get; set; }

        /// <summary>
        /// Хэш пароля (SHA256)
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string PasswordHash { get; set; } = string.Empty;

        /// <summary>
        /// Соль для пароля
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string PasswordSalt { get; set; } = string.Empty;

        /// <summary>
        /// Роль пользователя
        /// </summary>
        public UserRole Role { get; set; } = UserRole.Employee;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? LastLoginAt { get; set; }

        /// <summary>
        /// Активен ли пользователь
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Количество неудачных попыток входа
        /// </summary>
        public int FailedLoginAttempts { get; set; } = 0;

        /// <summary>
        /// Время блокировки после неудачных попыток
        /// </summary>
        public DateTime? LockoutUntil { get; set; }

        /// <summary>
        /// Должность сотрудника
        /// </summary>
        [MaxLength(100)]
        public string? Position { get; set; }

        /// <summary>
        /// Отдел
        /// </summary>
        [MaxLength(100)]
        public string? Department { get; set; }

        /// <summary>
        /// Является ли пользователь первым/главным админом компании
        /// </summary>
        public bool IsPrimaryAdmin { get; set; } = false;

        /// <summary>
        /// Подсказка для пароля
        /// </summary>
        [MaxLength(200)]
        public string? PasswordHint { get; set; }

        // Навигационные свойства
        public virtual Company Company { get; set; } = null!;

        /// <summary>
        /// Проверяет, имеет ли пользователь указанную роль или выше
        /// </summary>
        public bool HasRole(UserRole minimumRole)
        {
            return Role <= minimumRole;
        }

        /// <summary>
        /// Проверяет, заблокирован ли пользователь
        /// </summary>
        public bool IsLockedOut()
        {
            return LockoutUntil.HasValue && LockoutUntil.Value > DateTime.Now;
        }
    }

    /// <summary>
    /// Сессия пользователя для отслеживания активности
    /// </summary>
    public class UserSession
    {
        [Key]
        public int Id { get; set; }

        public int UserId { get; set; }

        [MaxLength(100)]
        public string SessionToken { get; set; } = Guid.NewGuid().ToString();

        public DateTime StartedAt { get; set; } = DateTime.Now;

        public DateTime? EndedAt { get; set; }

        [MaxLength(50)]
        public string? IpAddress { get; set; }

        [MaxLength(200)]
        public string? UserAgent { get; set; }

        public bool IsActive { get; set; } = true;

        // Навигационное свойство
        public virtual User User { get; set; } = null!;
    }
}
