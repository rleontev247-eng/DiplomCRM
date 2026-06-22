using System;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MimeKit;
using System.Text;

namespace MyFirstCRM
{
    public class EmailService : IDisposable
    {
        private readonly string _smtpHost;
        private readonly int _smtpPort;
        private readonly string _smtpUser;
        private readonly string _smtpPassword;
        private readonly bool _useSsl;

        public EmailService()
        {
            // Используем Gmail SMTP как базовый вариант
            // Пользователь может изменить эти настройки через интерфейс
            _smtpHost = "smtp.gmail.com";
            _smtpPort = 587;
            _smtpUser = ""; // Пользователь должен будет настроить
            _smtpPassword = ""; // Пользователь должен будет настроить
            _useSsl = true;
        }

        public EmailService(string smtpHost, int smtpPort, string smtpUser, string smtpPassword, bool useSsl = true)
        {
            _smtpHost = smtpHost;
            _smtpPort = smtpPort;
            _smtpUser = smtpUser;
            _smtpPassword = smtpPassword;
            _useSsl = useSsl;
        }

        public async Task<(bool Success, string ErrorMessage)> SendEmailAsync(string toEmail, string subject, string body)
        {
            try
            {
                if (string.IsNullOrEmpty(_smtpUser) || string.IsNullOrEmpty(_smtpPassword))
                {
                    return (false, "Настройки SMTP не настроены. Пожалуйста, настройте отправку email в настройках.");
                }

                var message = new MimeMessage();
                message.From.Add(new MailboxAddress("CRM System", _smtpUser));
                message.To.Add(MailboxAddress.Parse(toEmail));
                message.Subject = subject;

                var bodyBuilder = new BodyBuilder
                {
                    HtmlBody = $@"
                        <html>
                        <body>
                            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px;'>
                                <div style='background-color: #f8f9fa; padding: 20px; border-radius: 8px; margin-bottom: 20px;'>
                                    <h2 style='color: #333; margin: 0;'>CRM System</h2>
                                    <p style='color: #666; margin: 5px 0 0 0;'>Сообщение от системы управления клиентами</p>
                                </div>
                                <div style='background-color: white; padding: 20px; border-radius: 8px; border: 1px solid #e9ecef;'>
                                    {body.Replace("\n", "<br>")}
                                </div>
                                <div style='margin-top: 20px; padding: 15px; background-color: #f8f9fa; border-radius: 8px; text-align: center;'>
                                    <p style='color: #666; font-size: 12px; margin: 0;'>
                                        Это сообщение было отправлено автоматически из CRM системы<br>
                                        Время отправки: {DateTime.Now:dd.MM.yyyy HH:mm}
                                    </p>
                                </div>
                            </div>
                        </body>
                        </html>",
                    TextBody = body
                };

                message.Body = bodyBuilder.ToMessageBody();

                using var client = new SmtpClient();
                client.Timeout = 30000; // 30 секунд таймаут
                
                // Сначала пробуем стандартные настройки для провайдера
                MailKit.Security.SecureSocketOptions secureOptions;
                int port = _smtpPort;
                
                if (_smtpHost.Contains("gmail.com"))
                {
                    secureOptions = MailKit.Security.SecureSocketOptions.StartTls;
                    port = 587;
                }
                else if (_smtpHost.Contains("yahoo.com"))
                {
                    secureOptions = MailKit.Security.SecureSocketOptions.StartTls;
                    port = 587;
                }
                else if (_smtpHost.Contains("outlook.com") || _smtpHost.Contains("hotmail.com"))
                {
                    secureOptions = MailKit.Security.SecureSocketOptions.StartTls;
                    port = 587;
                }
                else if (_smtpHost.Contains("mail.ru"))
                {
                    secureOptions = MailKit.Security.SecureSocketOptions.SslOnConnect;
                    port = 465;
                }
                else if (_smtpHost.Contains("yandex.ru"))
                {
                    secureOptions = MailKit.Security.SecureSocketOptions.SslOnConnect;
                    port = 465;
                }
                else
                {
                    // Для других провайдеров используем настройки из конфигурации
                    secureOptions = _useSsl ? MailKit.Security.SecureSocketOptions.SslOnConnect : MailKit.Security.SecureSocketOptions.None;
                }
                
                try
                {
                    await client.ConnectAsync(_smtpHost, port, secureOptions);
                }
                catch (MailKit.Security.SslHandshakeException)
                {
                    // Если стандартный вариант не работает, пробуем другие
                    var fallbackOptions = new List<(int Port, MailKit.Security.SecureSocketOptions Options)>
                    {
                        (587, MailKit.Security.SecureSocketOptions.StartTlsWhenAvailable),
                        (587, MailKit.Security.SecureSocketOptions.Auto),
                        (465, MailKit.Security.SecureSocketOptions.Auto),
                        (587, MailKit.Security.SecureSocketOptions.None)
                    };
                    
                    bool connected = false;
                    foreach (var fallback in fallbackOptions)
                    {
                        try
                        {
                            await client.ConnectAsync(_smtpHost, fallback.Port, fallback.Options);
                            connected = true;
                            break;
                        }
                        catch
                        {
                            continue;
                        }
                    }
                    
                    if (!connected)
                    {
                        throw new Exception("Не удалось установить соединение ни с одним из вариантов SSL/TLS");
                    }
                }
                
                await client.AuthenticateAsync(_smtpUser, _smtpPassword);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                // Добавляем детальную информацию для отладки
                string detailedError = $"Ошибка отправки email: {ex.Message}";
                
                if (ex.InnerException != null)
                {
                    detailedError += $"\nВнутренняя ошибка: {ex.InnerException.Message}";
                }
                
                // Добавляем информацию о текущих настройках для отладки
                detailedError += $"\n\nНастройки SMTP:\nХост: {_smtpHost}\nПорт: {_smtpPort}\nSSL: {_useSsl}";
                
                return (false, detailedError);
            }
        }

        public bool IsConfigured()
        {
            return !string.IsNullOrEmpty(_smtpUser) && !string.IsNullOrEmpty(_smtpPassword);
        }

        public async Task<(bool Success, string Message)> TestConnectionAsync()
        {
            try
            {
                using var client = new SmtpClient();
                
                // Расширенный список вариантов для тестирования
                var testOptions = new List<(int Port, MailKit.Security.SecureSocketOptions Options, string Description)>
                {
                    // Gmail и другие современные провайдеры
                    (587, MailKit.Security.SecureSocketOptions.StartTls, "STARTTLS на порту 587"),
                    (587, MailKit.Security.SecureSocketOptions.StartTlsWhenAvailable, "STARTTLS когда доступно на порту 587"),
                    
                    // SSL варианты
                    (465, MailKit.Security.SecureSocketOptions.SslOnConnect, "SSL на порту 465"),
                    (587, MailKit.Security.SecureSocketOptions.SslOnConnect, "SSL на порту 587"),
                    
                    // Автоматические варианты
                    (587, MailKit.Security.SecureSocketOptions.Auto, "Автоматический на порту 587"),
                    (465, MailKit.Security.SecureSocketOptions.Auto, "Автоматический на порту 465"),
                    
                    // Без шифрования (для тестирования)
                    (25, MailKit.Security.SecureSocketOptions.None, "Без шифрования на порту 25"),
                    (587, MailKit.Security.SecureSocketOptions.None, "Без шифрования на порту 587")
                };
                
                foreach (var testOption in testOptions)
                {
                    try
                    {
                        // Создаем новый клиент для каждой попытки
                        using var testClient = new SmtpClient();
                        
                        // Устанавливаем таймауты
                        testClient.Timeout = 10000; // 10 секунд
                        
                        await testClient.ConnectAsync(_smtpHost, testOption.Port, testOption.Options);
                        await testClient.AuthenticateAsync(_smtpUser, _smtpPassword);
                        await testClient.DisconnectAsync(true);
                        
                        return (true, $"✅ Соединение успешно установлено: {testOption.Description}");
                    }
                    catch (MailKit.Security.SslHandshakeException sslEx)
                    {
                        // SSL ошибка - пробуем следующий вариант
                        continue;
                    }
                    catch (MailKit.Net.Smtp.SmtpProtocolException protoEx)
                    {
                        // Протокол ошибка - пробуем следующий вариант
                        continue;
                    }
                    catch (Exception ex)
                    {
                        // Другие ошибки - пробуем следующий вариант
                        continue;
                    }
                }
                
                return (false, "❌ Не удалось установить соединение ни с одним из вариантов SSL/TLS.\n\nВозможные решения:\n• Проверьте правильность логина и пароля\n• Для Gmail используйте 'Пароль приложения'\n• Проверьте что антивирус не блокирует порты 587/465\n• Попробуйте отключить VPN/прокси");
            }
            catch (Exception ex)
            {
                return (false, $"❌ Ошибка тестирования соединения: {ex.Message}");
            }
        }

        public string GetConfigurationInfo()
        {
            if (IsConfigured())
            {
                return $"SMTP: {_smtpHost}:{_smtpPort} (пользователь: {_smtpUser})";
            }
            return "SMTP не настроен";
        }

        public void Dispose()
        {
            // EmailService не имеет неуправляемых ресурсов для освобождения
            // Но реализуем IDisposable для совместимости
        }
    }
}
