using OfficeOpenXml;
using OfficeOpenXml.Style;
using System;
using System.Drawing;
using System.IO;

namespace MyFirstCRM
{
    public static class ExcelTemplates
    {
        public static void CreateClientTemplate(string filePath)
        {
            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Клиенты");

                // Заголовок
                worksheet.Cells["A1"].Value = "ШАБЛОН ДЛЯ ИМПОРТА КЛИЕНТОВ";
                worksheet.Cells["A1"].Style.Font.Size = 16;
                worksheet.Cells["A1"].Style.Font.Bold = true;
                worksheet.Cells["A1"].Style.Font.Color.SetColor(Color.White);
                worksheet.Cells["A1"].Style.Fill.PatternType = ExcelFillStyle.Solid;
                worksheet.Cells["A1"].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(59, 130, 246));
                worksheet.Cells["A1:D1"].Merge = true;

                // Инструкция
                worksheet.Cells["A2"].Value = "Инструкция:";
                worksheet.Cells["A2"].Style.Font.Bold = true;
                worksheet.Cells["A3"].Value = "1. Заполните данные в строках ниже";
                worksheet.Cells["A4"].Value = "2. Обязательное поле - 'ФИО'";
                worksheet.Cells["A5"].Value = "3. Сохраните файл и импортируйте в CRM";
                worksheet.Cells["A3:A5"].Style.Font.Italic = true;
                worksheet.Cells["A3:A5"].Style.Font.Color.SetColor(Color.Gray);

                // Заголовки столбцов
                string[] headers = { "ФИО (обязательно)", "Телефон", "Email", "Примечания" };
                for (int i = 0; i < headers.Length; i++)
                {
                    var cell = worksheet.Cells[7, i + 1];
                    cell.Value = headers[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    cell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(226, 232, 240));
                    cell.Style.Border.BorderAround(ExcelBorderStyle.Thin, Color.Black);
                    cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                }

                // Примеры данных
                string[,] examples = {
                    { "Иванов Иван Иванович", "+7 (999) 123-45-67", "ivanov@example.com", "Постоянный клиент" },
                    { "Петрова Анна Сергеевна", "+7 (999) 987-65-43", "petrova@example.com", "VIP клиент" },
                    { "Сидоров Алексей", "", "sidorov@mail.ru", "Новый клиент" },
                    { "Козлова Мария", "+7 (999) 555-44-33", "", "Интересуется оптом" }
                };

                for (int row = 0; row < 4; row++)
                {
                    for (int col = 0; col < 4; col++)
                    {
                        var cell = worksheet.Cells[8 + row, col + 1];
                        cell.Value = examples[row, col];
                        cell.Style.Border.BorderAround(ExcelBorderStyle.Thin, Color.LightGray);

                        if (row == 2 && col == 1) // Пустой телефон
                            cell.Style.Font.Color.SetColor(Color.Gray);
                    }
                }

                // Настройка ширины столбцов
                worksheet.Column(1).Width = 30;
                worksheet.Column(2).Width = 20;
                worksheet.Column(3).Width = 25;
                worksheet.Column(4).Width = 30;

                package.SaveAs(new FileInfo(filePath));
            }
        }

        public static void CreateDealTemplate(string filePath)
        {
            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Сделки");

                // Заголовок
                worksheet.Cells["A1"].Value = "ШАБЛОН ДЛЯ ИМПОРТА СДЕЛОК";
                worksheet.Cells["A1"].Style.Font.Size = 16;
                worksheet.Cells["A1"].Style.Font.Bold = true;
                worksheet.Cells["A1"].Style.Font.Color.SetColor(Color.White);
                worksheet.Cells["A1"].Style.Fill.PatternType = ExcelFillStyle.Solid;
                worksheet.Cells["A1"].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(16, 185, 129));
                worksheet.Cells["A1:F1"].Merge = true;

                // Инструкция
                worksheet.Cells["A2"].Value = "ВАЖНО: Клиент должен быть уже в системе или будет создан автоматически";
                worksheet.Cells["A2"].Style.Font.Bold = true;
                worksheet.Cells["A2"].Style.Font.Color.SetColor(Color.Red);

                worksheet.Cells["A3"].Value = "Статусы: Новые, В работе, Успешные, Проваленные";
                worksheet.Cells["A4"].Value = "Формат даты: ДД.ММ.ГГГГ (например: 15.12.2023)";
                worksheet.Cells["A5"].Value = "Вероятность: от 0 до 100%";
                worksheet.Cells["A3:A5"].Style.Font.Italic = true;

                // Заголовки столбцов
                string[] headers = {
                    "Название сделки",
                    "Клиент (ФИО)",
                    "Сумма (₽)",
                    "Статус",
                    "Срок (ДД.ММ.ГГГГ)",
                    "Вероятность (%)"
                };

                for (int i = 0; i < headers.Length; i++)
                {
                    var cell = worksheet.Cells[7, i + 1];
                    cell.Value = headers[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                    cell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(226, 232, 240));
                    cell.Style.Border.BorderAround(ExcelBorderStyle.Thin, Color.Black);
                    cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                }

                // Примеры данных
                string[,] examples = {
                    { "Поставка офисной техники", "Иванов Иван Иванович", "150000", "В работе", "30.12.2023", "80" },
                    { "Ремонт помещения", "Петрова Анна Сергеевна", "50000", "Успешные", "15.11.2023", "100" },
                    { "Консультационные услуги", "Сидоров Алексей", "25000", "Новые", "20.01.2024", "50" },
                    { "Закупка материалов", "Новый клиент", "75000", "В работе", "10.01.2024", "70" }
                };

                for (int row = 0; row < 4; row++)
                {
                    for (int col = 0; col < 6; col++)
                    {
                        var cell = worksheet.Cells[8 + row, col + 1];
                        cell.Value = examples[row, col];
                        cell.Style.Border.BorderAround(ExcelBorderStyle.Thin, Color.LightGray);

                        // Подсветка важных полей
                        if (col == 0 || col == 1) // Название и клиент
                            cell.Style.Font.Bold = true;

                        if (col == 2) // Сумма
                            cell.Style.Numberformat.Format = "#,##0";
                    }
                }

                // Валидация данных
                var statusValidation = worksheet.DataValidations.AddListValidation("D8:D100");
                statusValidation.Formula.Values.Add("Новые");
                statusValidation.Formula.Values.Add("В работе");
                statusValidation.Formula.Values.Add("Успешные");
                statusValidation.Formula.Values.Add("Проваленные");

                var probabilityValidation = worksheet.DataValidations.AddIntegerValidation("F8:F100");
                probabilityValidation.Formula.Value = 0;
                probabilityValidation.Formula2.Value = 100;
                probabilityValidation.ShowErrorMessage = true;
                probabilityValidation.ErrorTitle = "Ошибка";
                probabilityValidation.Error = "Введите значение от 0 до 100";

                // Настройка ширины столбцов
                worksheet.Column(1).Width = 30;
                worksheet.Column(2).Width = 25;
                worksheet.Column(3).Width = 15;
                worksheet.Column(4).Width = 15;
                worksheet.Column(5).Width = 15;
                worksheet.Column(6).Width = 15;

                package.SaveAs(new FileInfo(filePath));
            }
        }

        public static void CreateCombinedTemplate(string filePath)
        {
            using (var package = new ExcelPackage())
            {
                // Лист для клиентов
                CreateClientTemplateSheet(package);

                // Лист для сделок
                CreateDealTemplateSheet(package);

                // Лист с инструкцией
                CreateInstructionSheet(package);

                package.SaveAs(new FileInfo(filePath));
            }
        }

        private static void CreateClientTemplateSheet(ExcelPackage package)
        {
            var ws = package.Workbook.Worksheets.Add("Клиенты_шаблон");
            CreateClientTemplate(ws);
        }

        private static void CreateDealTemplateSheet(ExcelPackage package)
        {
            var ws = package.Workbook.Worksheets.Add("Сделки_шаблон");
            CreateDealTemplate(ws);
        }

        private static void CreateClientTemplate(ExcelWorksheet worksheet)
        {
            // Та же логика, но для существующего worksheet
            worksheet.Cells["A1"].Value = "ШАБЛОН ДЛЯ КЛИЕНТОВ (обязательные поля отмечены *)";
            // ... остальной код из CreateClientTemplate
        }

        private static void CreateDealTemplate(ExcelWorksheet worksheet)
        {
            worksheet.Cells["A1"].Value = "ШАБЛОН ДЛЯ СДЕЛОК";
            // ... остальной код из CreateDealTemplate
        }

        private static void CreateInstructionSheet(ExcelPackage package)
        {
            var ws = package.Workbook.Worksheets.Add("Инструкция");

            ws.Cells["A1"].Value = "ИНСТРУКЦИЯ ПО ИМПОРТУ ДАННЫХ В CRM";
            ws.Cells["A1"].Style.Font.Size = 18;
            ws.Cells["A1"].Style.Font.Bold = true;
            ws.Cells["A1"].Style.Font.Color.SetColor(Color.DarkBlue);
            ws.Cells["A1:H1"].Merge = true;

            // Основная инструкция
            string[] instructions = {
                "1. ЗАПОЛНЕНИЕ ШАБЛОНА:",
                "   • Используйте листы 'Клиенты_шаблон' и 'Сделки_шаблон'",
                "   • Обязательные поля отмечены звездочкой (*) или выделены",
                "   • Сохраните файл под новым именем",
                "",
                "2. ИМПОРТ В CRM:",
                "   • Откройте раздел 'Клиенты' или 'Сделки'",
                "   • Нажмите кнопку 'Импорт Excel'",
                "   • Выберите заполненный файл",
                "   • Настройте параметры импорта",
                "",
                "3. ВАЖНЫЕ МОМЕНТЫ:",
                "   • Клиенты для сделок должны быть сначала импортированы",
                "   • Формат дат: ДД.ММ.ГГГГ",
                "   • Поддерживается копирование из других программ",
                "",
                "4. ПОДДЕРЖКА:",
                "   • При ошибках - проверьте формат данных",
                "   • Можно импортировать несколько файлов",
                "   • Данные не удаляются при повторном импорте"
            };

            int row = 3;
            foreach (var line in instructions)
            {
                var cell = ws.Cells[row, 1];
                cell.Value = line;

                if (line.StartsWith("1.") || line.StartsWith("2.") ||
                    line.StartsWith("3.") || line.StartsWith("4."))
                {
                    cell.Style.Font.Bold = true;
                    cell.Style.Font.Color.SetColor(Color.FromArgb(59, 130, 246));
                }

                row++;
            }

            ws.Column(1).Width = 80;
        }
    }
}