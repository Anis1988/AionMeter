using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AionMeter.Core;

namespace AionMeter;

/// <summary>
/// Vector class emblems drawn in a hexagon badge, plus monster/boss icons and bar brushes.
/// A PNG named after the class in the Icons folder next to the exe replaces the drawn emblem.
/// </summary>
public static class ClassIcons
{
    /// <summary>One shape of an emblem, in a 24x24 box. Stroke 0 means filled.</summary>
    sealed record Part(string Data, double Stroke = 0, bool Dark = false);

    static readonly Color DarkColor = Color.FromRgb(0x0B, 0x0F, 0x17);
    static readonly Color MonsterColor = Color.FromRgb(0x8F, 0xA8, 0x8C);
    public static readonly Color BossColor = Color.FromRgb(0xFF, 0x3B, 0x3B);
    public static readonly Color AccentColor = Color.FromRgb(0x00, 0xE5, 0xFF);
    public static readonly Color Accent2Color = Color.FromRgb(0x8B, 0x5C, 0xF6);

    static readonly Dictionary<AionClass, Part[]> Emblems = new()
    {
        [AionClass.Templar] =
        [
            new("M12,2 L20.5,5 V11 C20.5,16.6 16.9,20.6 12,22.5 C7.1,20.6 3.5,16.6 3.5,11 V5 Z"),
            new("M12,6 V18 M8,10.5 H16", 1.8, Dark: true),
        ],
        [AionClass.Gladiator] =
        [
            new("M10.5,8 C7.5,7.5 5,9.5 3.5,13 M13.5,8 C16.5,7.5 19,9.5 20.5,13", 1.5),
            new("M12,1.5 L14.2,4.5 V15 H9.8 V4.5 Z"),
            new("M6.5,15.2 H17.5", 2.2),
            new("M12,15.5 V19.8", 2.4),
            new("M10.6,21.3 A1.4,1.4 0 1 0 13.4,21.3 A1.4,1.4 0 1 0 10.6,21.3 Z"),
        ],
        [AionClass.Assassin] =
        [
            new("M5,4 L16,15 M19,4 L8,15", 2.2),
            new("M13.5,17.5 L18.5,12.5 M10.5,17.5 L5.5,12.5", 2),
            new("M16.5,16.5 L19.8,19.8 M7.5,16.5 L4.2,19.8", 2.6),
        ],
        [AionClass.Ranger] =
        [
            new("M8,3 C18,7 18,17 8,21", 2.2),
            new("M8,3 L8,21", 0.9),
            new("M3.5,12 H18.5", 1.8),
            new("M22,12 L17.5,9.3 L17.5,14.7 Z"),
            new("M5,12 L3.3,9.6 M5,12 L3.3,14.4", 1.3),
        ],
        [AionClass.Sorcerer] =
        [
            new("M12,2.5 C13.5,6.5 18.5,8.5 18.5,14 A6.5,6.5 0 0 1 5.5,14 C5.5,10.5 7.5,8.5 8.8,6.5 C9.3,9.5 10.5,10.8 11.8,11 C11.2,8 11,5.5 12,2.5 Z"),
            new("M12,13 C13.2,14.6 14.8,15.5 14.8,17.3 A2.8,2.8 0 0 1 9.2,17.3 C9.2,15.5 10.8,14.6 12,13 Z", Dark: true),
        ],
        [AionClass.Spiritmaster] =
        [
            new("M6,12 A6,6 0 1 0 18,12 A6,6 0 1 0 6,12 Z", 1.3),
            new("M8.8,12 A3.2,3.2 0 1 0 15.2,12 A3.2,3.2 0 1 0 8.8,12 Z"),
            new("M10.4,3.5 A1.6,1.6 0 1 0 13.6,3.5 A1.6,1.6 0 1 0 10.4,3.5 Z M18.9,12 A1.6,1.6 0 1 0 22.1,12 A1.6,1.6 0 1 0 18.9,12 Z M10.4,20.5 A1.6,1.6 0 1 0 13.6,20.5 A1.6,1.6 0 1 0 10.4,20.5 Z M1.9,12 A1.6,1.6 0 1 0 5.1,12 A1.6,1.6 0 1 0 1.9,12 Z"),
        ],
        [AionClass.Cleric] =
        [
            new("M3,12 A9,9 0 1 0 21,12 A9,9 0 1 0 3,12 Z", 1.4),
            new("M10.3,4.5 H13.7 V10.3 H19.5 V13.7 H13.7 V19.5 H10.3 V13.7 H4.5 V10.3 H10.3 Z"),
        ],
        [AionClass.Chanter] =
        [
            new("M12,9.8 V22", 2.2),
            new("M8.4,6 A3.6,3.6 0 1 0 15.6,6 A3.6,3.6 0 1 0 8.4,6 Z", 2),
            new("M4.5,9 Q8.5,11.5 10.5,10.2 M19.5,9 Q15.5,11.5 13.5,10.2", 1.6),
            new("M10.8,6 A1.2,1.2 0 1 0 13.2,6 A1.2,1.2 0 1 0 10.8,6 Z"),
        ],
        [AionClass.Gunner] =
        [
            new("M2.5,7.5 H19 L20.5,9 V11 H15 L14,13 H11 L10,19 H5.5 L7,13 L4,12 L2.5,10.5 Z"),
            new("M11.3,13.2 Q11.3,15.6 13.4,15.4", 1, Dark: true),
            new("M21.8,7.6 L23.2,6.4 M22,10 H23.6", 1),
        ],
        [AionClass.Songweaver] =
        [
            new("M7.5,3.5 C3.5,8 5,15.5 9,18 H15 C19,15.5 20.5,8 16.5,3.5", 2),
            new("M8.5,6.5 H15.5", 1.6),
            new("M10.2,6.5 V18 M12,6.5 V18 M13.8,6.5 V18", 0.9),
            new("M8.5,18 H15.5 L14.5,21 H9.5 Z"),
        ],
        [AionClass.Aethertech] =
        [
            new(GearPath(12, 12, 9.5, 7.2, 8)),
            new("M8.6,12 A3.4,3.4 0 1 0 15.4,12 A3.4,3.4 0 1 0 8.6,12 Z", Dark: true),
            new("M12.8,8.6 L10.6,12.4 H13 L11.2,15.6", 1.1),
        ],
    };

    static readonly Part[] MonsterParts =
    [
        new("M12,4 C16.5,4 19,7 19,11 C19,13.5 18,15 16.5,16 V19 H7.5 V16 C6,15 5,13.5 5,11 C5,7 7.5,4 12,4 Z"),
        new("M5.6,8 L2.5,2.8 L7.6,5.8 Z M18.4,8 L21.5,2.8 L16.4,5.8 Z"),
        new("M7.6,11.5 A1.7,1.7 0 1 0 11,11.5 A1.7,1.7 0 1 0 7.6,11.5 Z M13,11.5 A1.7,1.7 0 1 0 16.4,11.5 A1.7,1.7 0 1 0 13,11.5 Z", Dark: true),
        new("M10,16.4 V19 M12,16.4 V19 M14,16.4 V19", 0.9, Dark: true),
    ];

    // Heartbeat line for the app logo / cube.
    static readonly Part[] LogoParts =
    [
        new("M2.5,13 H7.5 L9.6,7.5 L12.6,18.5 L14.8,10.5 L16.4,13 H21.5", 1.9),
    ];

    static readonly Dictionary<string, ImageSource> Cache = [];
    static readonly Dictionary<string, Brush> Brushes = [];

    public static ImageSource Get(AionClass cls) =>
        Cached("class:" + cls, () => LoadFile(cls.ToString()) ?? Badge(ColorOf(cls), PartsOf(cls), cls == AionClass.Unknown));

    public static ImageSource Monster(bool boss) =>
        Cached(boss ? "boss" : "monster", () =>
            LoadFile(boss ? "Boss" : "Monster") ?? Badge(boss ? BossColor : MonsterColor, MonsterParts, false));

    public static ImageSource Logo => Cached("logo", () => Badge(AccentColor, LogoParts, false, Accent2Color));

    public static Color ColorOf(AionClass cls) => (Color)ColorConverter.ConvertFromString(AionClassInfo.Color(cls));

    public static Brush Brush(AionClass cls) => Solid("solid:" + cls, ColorOf(cls));

    public static Brush BossBrush => Solid("solid:boss", BossColor);

    /// <summary>Glowing left-to-right gradient for meter bars.</summary>
    public static Brush BarBrush(AionClass cls, bool boss, bool monster) =>
        CachedBrush($"bar:{cls}:{boss}:{monster}", () =>
        {
            var c = boss ? BossColor : monster ? MonsterColor : ColorOf(cls);
            var b = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 0),
                GradientStops =
                {
                    new GradientStop(WithAlpha(c, 0xE0), 0),
                    new GradientStop(WithAlpha(c, 0x90), 0.6),
                    new GradientStop(WithAlpha(c, 0x30), 1),
                },
            };
            return b;
        });

    static Part[] PartsOf(AionClass cls) => Emblems.TryGetValue(cls, out var parts) ? parts : [];

    static ImageSource Cached(string key, Func<ImageSource> create)
    {
        if (!Cache.TryGetValue(key, out var image))
            Cache[key] = image = create();
        return image;
    }

    static Brush CachedBrush(string key, Func<Brush> create)
    {
        if (!Brushes.TryGetValue(key, out var brush))
        {
            brush = create();
            brush.Freeze();
            Brushes[key] = brush;
        }
        return brush;
    }

    static Brush Solid(string key, Color c) => CachedBrush(key, () => new SolidColorBrush(c));

    static ImageSource? LoadFile(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Icons", name + ".png");
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

    /// <summary>Hexagon badge (32x32) with the emblem drawn inside.</summary>
    static ImageSource Badge(Color color, Part[] parts, bool questionMark, Color? edge2 = null)
    {
        var group = new DrawingGroup();

        var hex = Geometry.Parse("M16,1 L29.5,8.6 V23.4 L16,31 L2.5,23.4 V8.6 Z");
        var fill = new RadialGradientBrush(Color.FromRgb(0x1D, 0x26, 0x38), DarkColor)
        {
            Center = new Point(0.5, 0.35),
            GradientOrigin = new Point(0.5, 0.3),
            RadiusX = 0.7,
            RadiusY = 0.7,
        };
        var edge = new LinearGradientBrush(Lighten(color, 0.35), edge2 ?? WithAlpha(color, 0x70), 90);
        group.Children.Add(new GeometryDrawing(fill, new Pen(edge, 1.6) { LineJoin = PenLineJoin.Round }, hex));

        var ink = new LinearGradientBrush(Lighten(color, 0.45), color, 90);
        if (edge2 is { } c2)
            ink = new LinearGradientBrush(color, c2, 0);
        var dark = new SolidColorBrush(DarkColor);

        var emblem = new DrawingGroup { Transform = new TranslateTransform(4, 4) };
        foreach (var part in parts)
        {
            var geometry = Geometry.Parse(part.Data);
            Brush brush = part.Dark ? dark : ink;
            emblem.Children.Add(part.Stroke > 0
                ? new GeometryDrawing(null, new Pen(brush, part.Stroke)
                {
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round,
                    LineJoin = PenLineJoin.Round,
                }, geometry)
                : new GeometryDrawing(brush, null, geometry));
        }

        if (questionMark)
        {
            var text = new FormattedText("?", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                17, ink, 1.0);
            emblem.Children.Add(new GeometryDrawing(ink, null,
                text.BuildGeometry(new Point((24 - text.Width) / 2, (24 - text.Height) / 2))));
        }

        group.Children.Add(emblem);
        var image = new DrawingImage(group);
        image.Freeze();
        return image;
    }

    /// <summary>A gear outline with <paramref name="teeth"/> teeth, as path data.</summary>
    static string GearPath(double cx, double cy, double outer, double inner, int teeth)
    {
        var points = new List<string>();
        var steps = teeth * 4;
        for (var i = 0; i < steps; i++)
        {
            var r = (i % 4) is 0 or 1 ? outer : inner;
            var a = (i - 0.5) * Math.PI * 2 / steps;
            points.Add(string.Create(CultureInfo.InvariantCulture,
                $"{cx + r * Math.Cos(a):0.##},{cy + r * Math.Sin(a):0.##}"));
        }
        return "M" + string.Join(" L", points) + " Z";
    }

    static Color WithAlpha(Color c, byte a) => Color.FromArgb(a, c.R, c.G, c.B);

    static Color Lighten(Color c, double amount) => Color.FromRgb(
        (byte)(c.R + (255 - c.R) * amount),
        (byte)(c.G + (255 - c.G) * amount),
        (byte)(c.B + (255 - c.B) * amount));
}
