using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using static System.Net.Mime.MediaTypeNames;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Перетащите файл изображения на .exe или передайте путь аргументом.");
            Console.WriteLine("Пример: ImgToIco.exe logo.png");
            return;
        }

        string inputPath = args[0];
        if (!File.Exists(inputPath))
        {
            Console.WriteLine("Файл не найден.");
            return;
        }

        string outputPath = Path.ChangeExtension(inputPath, ".ico");
        // Стандартный набор размеров для качественной иконки в Windows
        int[] sizes = { 16, 32, 48, 64, 128, 256 };

        try
        {
            ConvertToIcon(inputPath, outputPath, sizes);
            Console.WriteLine($"Готово! Иконка сохранена: {outputPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка: {ex.Message}");
        }
    }

    static void ConvertToIcon(string inputPath, string outputPath, int[] sizes)
    {
        using var originalImage = System.Drawing.Image.FromFile(inputPath);
        using var fs = new FileStream(outputPath, FileMode.Create);
        using var writer = new BinaryWriter(fs);

        // 1. Заголовок ICONDIR (6 байт)
        writer.Write((short)0);            // Зарезервировано (всегда 0)
        writer.Write((short)1);            // Тип изображения (1 = ICO)
        writer.Write((short)sizes.Length); // Количество иконок внутри файла

        var imageBuffers = new List<byte[]>();

        // Подготавливаем сжатые PNG-данные для каждого размера
        foreach (int size in sizes)
        {
            using var resized = ResizeImage(originalImage, size, size);
            using var ms = new MemoryStream();
            resized.Save(ms, ImageFormat.Png);
            imageBuffers.Add(ms.ToArray());
        }

        // Вычисляем смещение, с которого начнутся сами данные картинок
        int offset = 6 + (16 * sizes.Length);

        // 2. Записываем каталог изображений ICONDIRENTRY (по 16 байт на размер)
        for (int i = 0; i < sizes.Length; i++)
        {
            int size = sizes[i];
            byte[] data = imageBuffers[i];

            // В формате ICO размер 256 записывается как 0
            writer.Write((byte)(size >= 256 ? 0 : size)); // Ширина
            writer.Write((byte)(size >= 256 ? 0 : size)); // Высота
            writer.Write((byte)0);                        // Палитра цветов (0 = без палитры)
            writer.Write((byte)0);                        // Зарезервировано
            writer.Write((short)1);                       // Цветовые плоскости
            writer.Write((short)32);                      // Бит на пиксель (32-bit RGBA)
            writer.Write(data.Length);                    // Размер данных в байтах
            writer.Write(offset);                         // Смещение от начала файла

            offset += data.Length;
        }

        // 3. Записываем сами байты картинок
        foreach (var data in imageBuffers)
        {
            writer.Write(data);
        }
    }

    // Метод для качественного изменения размера с сохранением сглаживания
    static Bitmap ResizeImage(System.Drawing.Image image, int width, int height)
    {
        var destRect = new Rectangle(0, 0, width, height);
        var destImage = new Bitmap(width, height, PixelFormat.Format32bppArgb);

        destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

        using var graphics = Graphics.FromImage(destImage);
        graphics.CompositingMode = CompositingMode.SourceCopy;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.SmoothingMode = SmoothingMode.HighQuality;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        using var wrapMode = new ImageAttributes();
        wrapMode.SetWrapMode(WrapMode.TileFlipXY);
        graphics.DrawImage(image, destRect, 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, wrapMode);

        return destImage;
    }
}
