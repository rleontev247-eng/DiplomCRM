using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json.Linq;

namespace MyFirstCRM
{
    public static class YandexDiskHelper
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        /// <summary>
        /// Создаёт папку на Яндекс.Диске, если её нет.
        /// </summary>
        public static async Task<bool> CreateFolderAsync(string token, string folderPath)
        {
            try
            {
                var url = $"https://cloud-api.yandex.net/v1/disk/resources?path={Uri.EscapeDataString(folderPath)}";
                using (var request = new HttpRequestMessage(HttpMethod.Put, url))
                {
                    request.Headers.Add("Authorization", $"OAuth {token}");
                    var response = await _httpClient.SendAsync(request);
                    // 409 Conflict означает, что папка уже существует – считаем успехом
                    if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                        return true;
                    return response.IsSuccessStatusCode;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка создания папки: {ex.Message}", "Ошибка");
                return false;
            }
        }

        /// <summary>
        /// Загружает файл на Яндекс.Диск.
        /// </summary>
        /// <param name="token">OAuth-токен</param>
        /// <param name="localFilePath">Путь к локальному файлу</param>
        /// <param name="remotePath">Полный путь на диске (например, "/Отчеты/файл.xlsx")</param>
        public static async Task<bool> UploadFileAsync(string token, string localFilePath, string remotePath)
        {
            try
            {
                // 1. Получаем URL для загрузки
                var getUploadUrl = $"https://cloud-api.yandex.net/v1/disk/resources/upload?path={Uri.EscapeDataString(remotePath)}&overwrite=true";
                using (var request = new HttpRequestMessage(HttpMethod.Get, getUploadUrl))
                {
                    request.Headers.Add("Authorization", $"OAuth {token}");
                    var response = await _httpClient.SendAsync(request);
                    if (!response.IsSuccessStatusCode)
                    {
                        var error = await response.Content.ReadAsStringAsync();
                        MessageBox.Show($"Ошибка получения URL для загрузки: {response.StatusCode}\n{error}", "Ошибка Яндекс.Диска");
                        return false;
                    }
                    var json = await response.Content.ReadAsStringAsync();
                    var obj = JObject.Parse(json);
                    var uploadHref = obj["href"]?.ToString();
                    if (string.IsNullOrEmpty(uploadHref))
                    {
                        MessageBox.Show("Не удалось получить ссылку для загрузки.", "Ошибка");
                        return false;
                    }

                    // 2. Загружаем файл
                    using (var fileStream = File.OpenRead(localFilePath))
                    using (var uploadRequest = new HttpRequestMessage(HttpMethod.Put, uploadHref))
                    {
                        uploadRequest.Content = new StreamContent(fileStream);
                        var uploadResponse = await _httpClient.SendAsync(uploadRequest);
                        if (!uploadResponse.IsSuccessStatusCode)
                        {
                            var error = await uploadResponse.Content.ReadAsStringAsync();
                            MessageBox.Show($"Ошибка загрузки файла: {uploadResponse.StatusCode}\n{error}", "Ошибка");
                            return false;
                        }
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке на Яндекс.Диск: {ex.Message}", "Ошибка");
                return false;
            }
        }
    }
}