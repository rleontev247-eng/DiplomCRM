using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace MyFirstCRM
{
    public class MessageTemplate
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "";
        public string Subject { get; set; } = "";
        public string Body { get; set; } = "";
        public bool IsCustom { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class MessageTemplateManager
    {
        private static readonly string TemplatesPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MyFirstCRM",
            "message_templates.json");

        private static List<MessageTemplate> _templates = new List<MessageTemplate>();

        static MessageTemplateManager()
        {
            LoadTemplates();
        }

        public static List<MessageTemplate> GetTemplates()
        {
            return new List<MessageTemplate>(_templates);
        }

        public static void SaveTemplate(MessageTemplate template)
        {
            // Удаляем шаблон с таким же ID если он существует
            _templates.RemoveAll(t => t.Id == template.Id);
            
            // Добавляем новый шаблон
            _templates.Add(template);
            
            // Сохраняем в файл
            SaveTemplatesToFile();
        }

        public static void DeleteTemplate(string templateId)
        {
            _templates.RemoveAll(t => t.Id == templateId);
            SaveTemplatesToFile();
        }

        private static void LoadTemplates()
        {
            try
            {
                if (File.Exists(TemplatesPath))
                {
                    var json = File.ReadAllText(TemplatesPath);
                    var loadedTemplates = JsonSerializer.Deserialize<List<MessageTemplate>>(json);
                    if (loadedTemplates != null)
                    {
                        _templates = loadedTemplates;
                    }
                }

                // Если нет шаблонов, добавляем стандартные
                if (_templates.Count == 0)
                {
                    AddDefaultTemplates();
                }
            }
            catch (Exception ex)
            {
                // При ошибке загрузки добавляем стандартные шаблоны
                _templates.Clear();
                AddDefaultTemplates();
            }
        }

        private static void AddDefaultTemplates()
        {
            _templates.AddRange(new[]
            {
                new MessageTemplate
                {
                    Id = "greeting",
                    Name = "👋 Приветствие нового клиента",
                    Subject = "Добро пожаловать!",
                    Body = "Уважаемый клиент,\n\nДобро пожаловать в нашу компанию! Мы рады видеть вас среди наших клиентов и готовы предложить лучшие условия.\n\nС уважением,\nВаша команда",
                    IsCustom = false
                },
                new MessageTemplate
                {
                    Id = "meeting",
                    Name = "📅 Напоминание о встрече",
                    Subject = "Напоминание о встрече",
                    Body = "Уважаемый клиент,\n\nНапоминаем вам о запланированной встрече {дата}. Ждем вас по адресу: {адрес}.\n\nЕсли у вас возникнут вопросы, пожалуйста, свяжитесь с нами.\n\nС уважением,\nВаша команда",
                    IsCustom = false
                },
                new MessageTemplate
                {
                    Id = "proposal",
                    Name = "💰 Коммерческое предложение",
                    Subject = "Коммерческое предложение",
                    Body = "Уважаемый клиент,\n\nПодготовили для вас специальное коммерческое предложение. Уверены, наши условия вас заинтересуют.\n\nДетали предложения:\n{детали}\n\nБудем рады обсудить все вопросы в удобное для вас время.\n\nС уважением,\nВаша команда",
                    IsCustom = false
                },
                new MessageTemplate
                {
                    Id = "holiday",
                    Name = "🎉 Поздравление с праздником",
                    Subject = "Поздравляем с праздником!",
                    Body = "Уважаемый клиент,\n\nОт всей души поздравляем вас с {праздник}! Желаем успехов, благополучия и процветания.\n\nСпасибо, что вы с нами!\n\nС уважением,\nВаша команда",
                    IsCustom = false
                },
                new MessageTemplate
                {
                    Id = "urgent",
                    Name = "⚡ Срочное уведомление",
                    Subject = "Срочное уведомление",
                    Body = "Уважаемый клиент,\n\nИнформируем вас о срочной информации: {содержание}.\n\nПросим обратить внимание на данное сообщение и при необходимости связаться с нами.\n\nС уважением,\nВаша команда",
                    IsCustom = false
                }
            });

            SaveTemplatesToFile();
        }

        private static void SaveTemplatesToFile()
        {
            try
            {
                var directory = Path.GetDirectoryName(TemplatesPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(_templates, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                File.WriteAllText(TemplatesPath, json);
            }
            catch (Exception ex)
            {
                // Логируем ошибку, но не прерываем работу
                System.Diagnostics.Debug.WriteLine($"Ошибка сохранения шаблонов: {ex.Message}");
            }
        }
    }
}
