using SkiaSharp;
using DiabetesBot.Models;

namespace DiabetesBot.Utils;

public static class ChartGenerator
{
    public static byte[] GenerateGlucoseChart(List<Measurement> points)
    {
        int width = 900;
        int height = 500;

        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        var canvas = surface.Canvas;

        canvas.Clear(SKColors.White);

        // рамка
        var borderPaint = new SKPaint
        {
            Color = SKColors.Gray,
            StrokeWidth = 2,
            IsStroke = true
        };
        canvas.DrawRect(0, 0, width, height, borderPaint);

        float chartLeft = 70;
        float chartRight = width - 50;
        float chartBottom = height - 70;
        float chartTop = 40;

        // нормальный диапазон 4-7
        float yMin = 4f;
        float yMax = 7f;

        float normalTop = chartBottom - (yMax * 40);
        float normalBottom = chartBottom - (yMin * 40);

        var normalPaint = new SKPaint
        {
            Color = new SKColor(200, 255, 200),
            Style = SKPaintStyle.Fill
        };
        canvas.DrawRect(chartLeft, normalTop, chartRight - chartLeft, normalBottom - normalTop, normalPaint);

        // оси
        var axisPaint = new SKPaint
        {
            Color = SKColors.Black,
            StrokeWidth = 3
        };

        canvas.DrawLine(chartLeft, chartTop, chartLeft, chartBottom, axisPaint);
        canvas.DrawLine(chartLeft, chartBottom, chartRight, chartBottom, axisPaint);

        // точки
        var pointPaint = new SKPaint
        {
            Color = SKColors.Red,
            StrokeWidth = 10,
            IsStroke = false,
            IsAntialias = true
        };

        var linePaint = new SKPaint
        {
            Color = SKColors.Red,
            StrokeWidth = 3,
            IsStroke = true,
            IsAntialias = true
        };

        var valuePaint = new SKPaint
        {
            Color = SKColors.Black,
            TextSize = 22,
            IsAntialias = true
        };

        var datePaint = new SKPaint
        {
            Color = SKColors.Black,
            TextSize = 20,
            IsAntialias = true
        };

        var sorted = points.OrderBy(p => p.Timestamp).ToList();
        float stepX = (chartRight - chartLeft) / (float)(sorted.Count - 1);

        float prevX = 0, prevY = 0;

        for (int i = 0; i < sorted.Count; i++)
        {
            float x = chartLeft + i * stepX;
            float y = chartBottom - (float)sorted[i].Value * 40;

            // точка
            canvas.DrawCircle(x, y, 6, pointPaint);

            // соединяем линией
            if (i > 0)
                canvas.DrawLine(prevX, prevY, x, y, linePaint);

            // подпись значения
            string val = sorted[i].Value?.ToString("F1") ?? "0";
            canvas.DrawText(val, x - 20, y - 12, valuePaint);

            // подпись даты снизу
            string date = sorted[i].Timestamp.ToString("dd.MM");
            canvas.DrawText(date, x - 25, chartBottom + 30, datePaint);

            prevX = x;
            prevY = y;
        }

        using var img = surface.Snapshot();
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);

        return data.ToArray();
    }
}
