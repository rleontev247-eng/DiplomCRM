using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MyFirstCRM
{
    /// <summary>
    /// Режимы работы CRM системы
    /// </summary>
    public enum DeploymentMode
    {
        /// <summary>
        /// Локальный режим - все базы данных на этом компьютере
        /// </summary>
        Local = 0,

        /// <summary>
        /// Облачный режим - подключение к центральному серверу
        /// </summary>
        Cloud = 1,

        /// <summary>
        /// Гибридный режим - локальная база с синхронизацией через интернет
        /// </summary>
        Hybrid = 2,

        /// <summary>
        /// Режим сервера - этот компьютер работает как сервер для других
        /// </summary>
        Server = 3,

        /// <summary>
        /// Клиент сервера - подключение к локальному серверу в сети
        /// </summary>
        ServerClient = 4
    }

    /// <summary>
    /// Конфигурация развертывания системы
    /// </summary>
    public class DeploymentConfig
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Текущий режим работы
        /// </summary>
        public DeploymentMode Mode { get; set; } = DeploymentMode.Local;

        /// <summary>
        /// URL облачного сервера
        /// </summary>
        [MaxLength(500)]
        public string? CloudServerUrl { get; set; }

        /// <summary>
        /// API ключ для облачного сервера
        /// </summary>
        [MaxLength(200)]
        public string? CloudApiKey { get; set; }

        /// <summary>
        /// IP адрес локального сервера
        /// </summary>
        [MaxLength(50)]
        public string? ServerIpAddress { get; set; }

        /// <summary>
        /// Порт локального сервера
        /// </summary>
        public int ServerPort { get; set; } = 8080;

        /// <summary>
        /// Интервал синхронизации в минутах
        /// </summary>
        public int SyncIntervalMinutes { get; set; } = 30;

        /// <summary>
        /// Включена ли автоматическая синхронизация
        /// </summary>
        public bool AutoSyncEnabled { get; set; } = true;

        /// <summary>
        /// Последняя синхронизация
        /// </summary>
        public DateTime? LastSyncAt { get; set; }

        /// <summary>
        /// Статус последней синхронизации
        /// </summary>
        public SyncStatus LastSyncStatus { get; set; } = SyncStatus.None;

        /// <summary>
        /// Ошибка последней синхронизации
        /// </summary>
        [MaxLength(1000)]
        public string? LastSyncError { get; set; }

        /// <summary>
        /// Использовать сжатие при синхронизации
        /// </summary>
        public bool UseCompression { get; set; } = true;

        /// <summary>
        /// Использовать шифрование при синхронизации
        /// </summary>
        public bool UseEncryption { get; set; } = true;

        /// <summary>
        /// Только синхронизация в одну сторону (локальный -> сервер)
        /// </summary>
        public bool OneWaySyncOnly { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// Статус синхронизации
    /// </summary>
    public enum SyncStatus
    {
        None = 0,
        InProgress = 1,
        Success = 2,
        Failed = 3,
        Partial = 4,
        Conflict = 5
    }

    /// <summary>
    /// Лог синхронизации
    /// </summary>
    public class SyncLog
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// ID компании
        /// </summary>
        public int CompanyId { get; set; }

        /// <summary>
        /// Тип синхронизации
        /// </summary>
        public SyncType SyncType { get; set; }

        /// <summary>
        /// Статус
        /// </summary>
        public SyncStatus Status { get; set; }

        /// <summary>
        /// Начало синхронизации
        /// </summary>
        public DateTime StartedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Окончание синхронизации
        /// </summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>
        /// Количество записей отправлено
        /// </summary>
        public int RecordsSent { get; set; }

        /// <summary>
        /// Количество записей получено
        /// </summary>
        public int RecordsReceived { get; set; }

        /// <summary>
        /// Размер данных в байтах
        /// </summary>
        public long DataSizeBytes { get; set; }

        /// <summary>
        /// Ошибка если была
        /// </summary>
        [MaxLength(1000)]
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Дополнительная информация
        /// </summary>
        [MaxLength(2000)]
        public string? Details { get; set; }
    }

    /// <summary>
    /// Типы синхронизации
    /// </summary>
    public enum SyncType
    {
        Full = 0,
        Incremental = 1,
        Manual = 2,
        Scheduled = 3,
        ConflictResolution = 4
    }

    /// <summary>
    /// Информация о сервере
    /// </summary>
    public class ServerInfo
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Уникальный ID сервера
        /// </summary>
        [MaxLength(100)]
        public string ServerId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Название сервера
        /// </summary>
        [MaxLength(200)]
        public string ServerName { get; set; } = Environment.MachineName;

        /// <summary>
        /// IP адрес
        /// </summary>
        [MaxLength(50)]
        public string? IpAddress { get; set; }

        /// <summary>
        /// Порт
        /// </summary>
        public int Port { get; set; } = 8080;

        /// <summary>
        /// Версия сервера
        /// </summary>
        [MaxLength(50)]
        public string Version { get; set; } = "1.0.0";

        /// <summary>
        /// Сервер активен
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Максимальное количество клиентов
        /// </summary>
        public int MaxClients { get; set; } = 50;

        /// <summary>
        /// Текущее количество подключенных клиентов
        /// </summary>
        public int CurrentClients { get; set; } = 0;

        /// <summary>
        /// Дата запуска сервера
        /// </summary>
        public DateTime StartedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Последний раз был онлайн
        /// </summary>
        public DateTime LastOnlineAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Разрешенные IP адреса (через запятую)
        /// </summary>
        [MaxLength(500)]
        public string? AllowedIpAddresses { get; set; }

        /// <summary>
        /// Требуется ли аутентификация
        /// </summary>
        public bool RequiresAuth { get; set; } = true;

        /// <summary>
        /// Секретный ключ для API
        /// </summary>
        [MaxLength(200)]
        public string? ApiSecret { get; set; }
    }
}
