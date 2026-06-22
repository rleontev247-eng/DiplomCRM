using System.Windows.Media;

namespace MyFirstCRM
{
    public static class ColorThemeManager
    {
        public static void ApplyTheme(string themeName)
        {
            var app = System.Windows.Application.Current;
            if (app == null) return;

            // Обновляем ресурсы приложения
            var accentColor = GetAccentColor(themeName);

            app.Resources["PrimaryAccent"] = new SolidColorBrush(accentColor);

            // Создаем более темный вариант для вторичного цвета
            Color darkerColor = Color.FromArgb(
                accentColor.A,
                (byte)(accentColor.R * 0.7),
                (byte)(accentColor.G * 0.7),
                (byte)(accentColor.B * 0.7)
            );

            app.Resources["SecondaryAccent"] = new SolidColorBrush(darkerColor);
        }

        private static Color GetAccentColor(string colorName)
        {
            return colorName switch
            {
                "Green" => Color.FromArgb(255, 16, 185, 129),     // #10B981
                "Purple" => Color.FromArgb(255, 139, 92, 246),   // #8B5CF6
                "Orange" => Color.FromArgb(255, 245, 158, 11),   // #F59E0B
                "Red" => Color.FromArgb(255, 239, 68, 68),       // #EF4444
                _ => Color.FromArgb(255, 59, 130, 246)           // #3B82F6 (синий по умолчанию)
            };
        }
    }
}