using System;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Linq;
using System.Windows;

namespace MyFirstCRM
{
    public static class DatabaseInitializer
    {
        public static void Initialize()
        {
            try
            {
                using (var context = new AppDbContext())
                {
                    // Проверяем существование таблицы CalendarEvents
                    var calendarEventsExists = CheckTableExists(context, "CalendarEvents");
                    var notificationsExists = CheckTableExists(context, "Notifications");

                    if (!calendarEventsExists)
                    {
                        Console.WriteLine("Таблица CalendarEvents не найдена, создаем...");
                        CreateCalendarEventsTable();
                    }
                    
                    if (!notificationsExists)
                    {
                        Console.WriteLine("Таблица Notifications не найдена, создаем...");
                        CreateNotificationsTable();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка инициализации базы: {ex.Message}");
            }
        }

        private static bool CheckTableExists(AppDbContext context, string tableName)
        {
            try
            {
                var connection = context.Database.GetDbConnection();
                connection.Open();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $"SELECT name FROM sqlite_master WHERE type='table' AND name='{tableName}'";
                    var result = command.ExecuteScalar();

                    connection.Close();
                    return result != null;
                }
            }
            catch
            {
                return false;
            }
        }

        private static void CreateCalendarEventsTable()
        {
            try
            {
                using (var context = new AppDbContext())
                {
                    var connection = context.Database.GetDbConnection();
                    connection.Open();
                    
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = @"
                            CREATE TABLE IF NOT EXISTS CalendarEvents (
                                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                Title TEXT NOT NULL,
                                Description TEXT,
                                StartDate TEXT NOT NULL,
                                EndDate TEXT NOT NULL,
                                IsAllDay INTEGER NOT NULL DEFAULT 0,
                                Location TEXT,
                                EventType INTEGER NOT NULL,
                                Priority INTEGER NOT NULL,
                                Color TEXT NOT NULL,
                                ReminderMinutes INTEGER NOT NULL DEFAULT 15,
                                CreatedAt TEXT NOT NULL DEFAULT datetime('now'),
                                UpdatedAt TEXT NOT NULL DEFAULT datetime('now'),
                                DealId INTEGER,
                                ClientId INTEGER,
                                Status INTEGER NOT NULL DEFAULT 0,
                                FOREIGN KEY (DealId) REFERENCES Deals(Id) ON DELETE SET NULL,
                                FOREIGN KEY (ClientId) REFERENCES Clients(Id) ON DELETE SET NULL
                            )";
                        
                        command.ExecuteNonQuery();
                    }
                    connection.Close();
                    
                    Console.WriteLine("✅ Таблица CalendarEvents успешно создана");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка создания таблицы CalendarEvents: {ex.Message}");
            }
        }

        private static void CreateNotificationsTable()
        {
            try
            {
                using (var context = new AppDbContext())
                {
                    var connection = context.Database.GetDbConnection();
                    connection.Open();
                    
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = @"
                            CREATE TABLE IF NOT EXISTS Notifications (
                                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                Title TEXT NOT NULL,
                                Message TEXT,
                                CreatedAt TEXT NOT NULL DEFAULT datetime('now'),
                                Icon TEXT,
                                Color TEXT
                            )";
                        
                        command.ExecuteNonQuery();
                    }
                    connection.Close();
                    
                    Console.WriteLine("✅ Таблица Notifications успешно создана");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка создания таблицы Notifications: {ex.Message}");
            }
        }
    }
}