using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Media.Imaging;

namespace AutoGrayscaleWindows.UI.Resources;

/// <summary>
/// Генератор иконки приложения
/// </summary>
public static class IconGenerator
{
    /// <summary>
    /// Создаёт иконку приложения (цветной круг, похожий на иконку в трее)
    /// </summary>
    public static Icon CreateAppIcon(int size = 256)
    {
        using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);

        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(System.Drawing.Color.Transparent);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            // Рисуем круг с градиентом (зелёный, как иконка "выключен" в трее)
            using var brush = new LinearGradientBrush(
                new Point(0, 0),
                new Point(size, size),
                System.Drawing.Color.FromArgb(255, 100, 220, 100),  // Светло-зелёный
                System.Drawing.Color.FromArgb(255, 50, 180, 50));   // Тёмно-зелёный
            
            g.FillEllipse(brush, 4, 4, size - 8, size - 8);

            // Обводка
            using var pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(255, 30, 150, 30), size / 32f);
            g.DrawEllipse(pen, 4, 4, size - 8, size - 8);

            // Символ G в центре
            using var font = new Font("Arial", size / 2.5f, FontStyle.Bold);
            using var textBrush = new SolidBrush(System.Drawing.Color.White);
            using var textFormat = new StringFormat()
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            
            // Тень для текста
            using var shadowBrush = new SolidBrush(System.Drawing.Color.FromArgb(100, 0, 0, 0));
            g.DrawString("G", font, shadowBrush, size / 2f + 2, size / 2f + 2, textFormat);
            g.DrawString("G", font, textBrush, size / 2f, size / 2f, textFormat);
        }

        return Icon.FromHandle(bitmap.GetHicon());
    }

    /// <summary>
    /// Создаёт BitmapFrame для использования в WPF
    /// </summary>
    public static BitmapFrame CreateAppIconBitmap(int size = 256)
    {
        using var icon = CreateAppIcon(size);
        using var bitmap = icon.ToBitmap();
        
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        stream.Position = 0;
        
        var decoder = PngBitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        return decoder.Frames[0];
    }

    /// <summary>
    /// Сохраняет иконку в файл .ico
    /// </summary>
    public static void SaveIconToFile(string filePath, int size = 256)
    {
        using var icon = CreateAppIcon(size);
        using var stream = new FileStream(filePath, FileMode.Create);
        icon.Save(stream);
    }
}