using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.Collections.Generic;

namespace MyFirstCRM
{
    public class SmsService
    {
        private readonly string _apiKey;
        private readonly string _senderName;
        private readonly SmsProvider _provider;
        private readonly HttpClient _httpClient;

        public SmsService()
        {
            // Используем SMS.ru как базовый провайдер
            _apiKey = ""; // Пользователь должен будет настроить
            _senderName = "CRM";
            _provider = SmsProvider.SMSRU;
            _httpClient = new HttpClient();
        }

        public SmsService(string apiKey, string senderName = "CRM", SmsProvider provider = SmsProvider.SMSRU)
        {
            _apiKey = apiKey;
            _senderName = senderName;
            _provider = provider;
            _httpClient = new HttpClient();
        }

        public async Task<(bool Success, string ErrorMessage)> SendSmsAsync(string phoneNumber, string message)
        {
            try
            {
                if (string.IsNullOrEmpty(_apiKey))
                {
                    return (false, "Настройки SMS не настроены. Пожалуйста, настройте отправку SMS в настройках.");
                }

                if (string.IsNullOrEmpty(phoneNumber) || string.IsNullOrEmpty(message))
                {
                    return (false, "Номер телефона и сообщение не могут быть пустыми.");
                }

                // Очищаем номер телефона
                var cleanPhone = CleanPhoneNumber(phoneNumber);

                switch (_provider)
                {
                    case SmsProvider.SMSRU:
                        return await SendViaSMSRU(cleanPhone, message);
                    case SmsProvider.Twilio:
                        return await SendViaTwilio(cleanPhone, message);
                    case SmsProvider.Nexmo:
                        return await SendViaNexmo(cleanPhone, message);
                    default:
                        return (false, "Неподдерживаемый SMS провайдер.");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Ошибка отправки SMS: {ex.Message}");
            }
        }

        private async Task<(bool Success, string ErrorMessage)> SendViaSMSRU(string phoneNumber, string message)
        {
            try
            {
                var url = $"https://sms.ru/sms/send?api_id={_apiKey}&to={phoneNumber}&msg={Uri.EscapeDataString(message)}&json=1";
                
                if (!string.IsNullOrEmpty(_senderName))
                {
                    url += $"&from={Uri.EscapeDataString(_senderName)}";
                }

                var response = await _httpClient.GetAsync(url);
                var content = await response.Content.ReadAsStringAsync();
                
                // Отладочный вывод
                System.Diagnostics.Debug.WriteLine($"SMS API Response: {content}");
                
                // Безопасная обработка JSON
                JsonElement result;
                try
                {
                    result = JsonSerializer.Deserialize<JsonElement>(content);
                }
                catch (JsonException ex)
                {
                    return (false, $"Ошибка парсинга ответа API: {ex.Message}");
                }
                
                if (result.TryGetProperty("status", out var status))
                {
                    // Статус может быть строкой "OK" или числом
                    string statusValue = status.ValueKind == JsonValueKind.String 
                        ? status.GetString() 
                        : status.GetInt32().ToString();
                        
                    if (statusValue == "OK" || statusValue == "100")
                    {
                        if (result.TryGetProperty("sms", out var smsProperty))
                        {
                            foreach (var sms in smsProperty.EnumerateObject())
                            {
                                if (sms.Value.TryGetProperty("status", out var smsStatus))
                                {
                                    string smsStatusValue = smsStatus.ValueKind == JsonValueKind.String 
                                        ? smsStatus.GetString() 
                                        : smsStatus.GetInt32().ToString();
                                        
                                    if (smsStatusValue == "OK" || smsStatusValue == "100")
                                    {
                                        return (true, string.Empty);
                                    }
                                }
                                if (sms.Value.TryGetProperty("status_text", out var statusText))
                                {
                                    return (false, $"SMS не отправлено: {statusText.GetString()}");
                                }
                            }
                        }
                        return (true, string.Empty);
                    }
                    else
                    {
                        // Если статус не OK, пытаемся получить описание ошибки
                        if (result.TryGetProperty("status_text", out var statusText))
                        {
                            return (false, $"SMS не отправлено: {statusText.GetString()}");
                        }
                        return (false, $"SMS не отправлено. Статус: {statusValue}");
                    }
                }
                else
                {
                    var errorMsg = result.TryGetProperty("status_code", out var code) && 
                                 result.TryGetProperty("status_text", out var text) 
                                 ? $"{code.GetInt32()}: {text.GetString()}" 
                                 : "Неизвестная ошибка";
                    return (false, $"SMS.ru ошибка: {errorMsg}");
                }
            }
            catch (Exception ex)
            {
                return (false, $"Ошибка SMS.ru: {ex.Message}");
            }
        }

        private async Task<(bool Success, string ErrorMessage)> SendViaTwilio(string phoneNumber, string message)
        {
            try
            {
                // Twilio требует Account SID и Auth Token, а не просто API ключ
                // Это упрощенная реализация - для реального использования нужно больше параметров
                return (false, "Twilio провайдер требует дополнительной настройки. Используйте SMS.ru или настройте параметры Twilio.");
            }
            catch (Exception ex)
            {
                return (false, $"Ошибка Twilio: {ex.Message}");
            }
        }

        private async Task<(bool Success, string ErrorMessage)> SendViaNexmo(string phoneNumber, string message)
        {
            try
            {
                // Nexmo (Vonage) также требует API Key и API Secret
                return (false, "Nexmo провайдер требует дополнительной настройки. Используйте SMS.ru или настройте параметры Nexmo.");
            }
            catch (Exception ex)
            {
                return (false, $"Ошибка Nexmo: {ex.Message}");
            }
        }

        private string CleanPhoneNumber(string phoneNumber)
        {
            // Удаляем все символы кроме цифр
            var cleaned = new string(phoneNumber.Where(char.IsDigit).ToArray());
            
            // Если номер начинается с 8, заменяем на 7 (для России)
            if (cleaned.StartsWith("8") && cleaned.Length == 11)
            {
                cleaned = "7" + cleaned.Substring(1);
            }
            
            // Добавляем + если нужно
            if (!cleaned.StartsWith("+") && cleaned.Length == 11)
            {
                cleaned = "+" + cleaned;
            }
            
            return cleaned;
        }

        public bool IsConfigured()
        {
            return !string.IsNullOrEmpty(_apiKey);
        }

        public async Task<(bool Success, string Message)> TestConnectionAsync()
        {
            try
            {
                if (!IsConfigured())
                {
                    return (false, "❌ API ключ не настроен");
                }

                // Проверяем баланс для SMS.ru
                if (_provider == SmsProvider.SMSRU)
                {
                    var url = $"https://sms.ru/my/balance?api_id={_apiKey}&json=1";
                    var response = await _httpClient.GetAsync(url);
                    var content = await response.Content.ReadAsStringAsync();
                    
                    // Отладочный вывод
                    System.Diagnostics.Debug.WriteLine($"SMS Balance API Response: {content}");
                    
                    // Безопасная обработка JSON
                    JsonElement result;
                    try
                    {
                        result = JsonSerializer.Deserialize<JsonElement>(content);
                    }
                    catch (JsonException ex)
                    {
                        return (false, $"Ошибка парсинга ответа API: {ex.Message}");
                    }
                    
                    if (result.TryGetProperty("status", out var status))
                    {
                        // Статус может быть строкой "OK" или числом
                        string statusValue = status.ValueKind == JsonValueKind.String 
                            ? status.GetString() 
                            : status.GetInt32().ToString();
                            
                        if (statusValue == "OK" || statusValue == "100")
                        {
                            if (result.TryGetProperty("balance", out var balance))
                            {
                                return (true, $"✅ Соединение успешно. Баланс: {balance.GetString()} руб.");
                            }
                            return (true, "✅ Соединение успешно установлено");
                        }
                        else
                        {
                            // Если статус не OK, пытаемся получить описание ошибки
                            if (result.TryGetProperty("status_text", out var statusText))
                            {
                                return (false, $"❌ Ошибка: {statusText.GetString()}");
                            }
                            return (false, $"❌ Ошибка подключения. Статус: {statusValue}");
                        }
                    }
                    else
                    {
                        var errorMsg = result.TryGetProperty("status_code", out var code) && 
                                     result.TryGetProperty("status_text", out var text) 
                                     ? $"{code.GetInt32()}: {text.GetString()}" 
                                     : "Неизвестная ошибка";
                        return (false, $"❌ Ошибка подключения: {errorMsg}");
                    }
                }

                return (true, "✅ Настройки провайдера корректны");
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
                return $"SMS: {_provider} (отправитель: {_senderName})";
            }
            return "SMS не настроен";
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    public enum SmsProvider
    {
        SMSRU,
        Twilio,
        Nexmo
    }
}
