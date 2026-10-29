using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AionMeter.Core;

namespace AionMeter;

/// <summary>Class icons: a PNG from the Icons folder if present, otherwise a drawn badge.</summary>
public static class ClassIcons
{
    static readonly Dictionary<AionClass, ImageSource> Icons = [];
    static readonly Dictionary<AionClass, SolidColorBrush> Brushes = [];

    public static ImageSource Get(AionClass cls)
    {
        if (!Icons.TryGetValue(cls, out var icon))
            Icons[cls] = icon = LoadFile(cls) ?? Draw(cls);
        return icon;
    }

    public static SolidColorBrush Brush(AionClass cls)
    {
        if (!Brushes.TryGetValue(cls, out var brush))
        {
            brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(AionClassInfo.Color(cls)));
            brush.Freeze();
            Brushes[cls] = brush;
        }
        return brush;
    }

    static ImageSource? LoadFile(AionClass cls)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Icons", cls + ".png");
        if (!File.Exists(path))
            return null;
        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(path);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or FileFormatException)
        {
            return null;
        }
    }

    static ImageSource Draw(AionClass cls)
    {
        const double size = 32;
        var group = new DrawingGroup();
        var fill = Brush(cls);
        var border = new Pen(new SolidColorBrush(Color.FromArgb(160, 255, 255, 255)), 1.5);
        group.Children.Add(new GeometryDrawing(fill, border,
            new RectangleGeometry(new Rect(1, 1, size - 2, size - 2), 7, 7)));

        var text = new FormattedText(AionClassInfo.ShortName(cls), CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal,
                FontWeights.Bold, FontStretches.Normal), 14, System.Windows.Media.Brushes.White, 1.0);
        var origin = new Point((size - text.Width) / 2, (size - text.Height) / 2);
        group.Children.Add(new GeometryDrawing(System.Windows.Media.Brushes.White, null, text.BuildGeometry(origin)));

        var image = new DrawingImage(group);
        image.Freeze();
        return image;
    }
}
