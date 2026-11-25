using System.ComponentModel;
using System.ComponentModel.Design.Serialization;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using EnterpriseUI.Icons;

namespace EnterpriseUI.WinForms;

/// <summary>
/// A monochrome vector image built from SVG path data or a small SVG subset. It is painted in any color at any size, so it stays sharp at every DPI and follows the theme.
/// Supported elements: path, rect, circle, ellipse, line, polyline and polygon with fill, stroke, stroke-width, fill-rule and inherited presentation attributes.
/// Gradients, filters, text, transforms and per-shape colors are not supported; every shape is painted in the color passed to <see cref="Draw"/>.
/// </summary>
[TypeConverter(typeof(EnterpriseSvgImageConverter))]
public sealed class EnterpriseSvgImage
{
    private const int MaxMarkupLength = 262144;
    private static readonly char[] ListSeparators = { ' ', ',', '\t', '\r', '\n' };
    private readonly SvgShape[] _shapes;

    /// <summary>Creates an image from one filled path.</summary>
    /// <param name="pathData">SVG path data (the <c>d</c> attribute).</param>
    /// <param name="viewBox">The coordinate system as "min-x min-y width height".</param>
    /// <exception cref="ArgumentException">The path data or view box is empty or malformed.</exception>
    public EnterpriseSvgImage(string pathData, string viewBox = "0 0 24 24")
    {
        if (string.IsNullOrWhiteSpace(pathData)) throw new ArgumentException("Path data is required.", nameof(pathData));
        PathData = pathData;
        ViewBoxText = viewBox;
        ViewBox = ParseViewBox(viewBox);
        try
        {
            _shapes = new[] { new SvgShape(SvgPathParser.Parse(pathData), true, 0f, false) };
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("The path data is malformed.", nameof(pathData), ex);
        }
    }

    private EnterpriseSvgImage(string markup, RectangleF viewBox, SvgShape[] shapes)
    {
        Markup = markup;
        ViewBox = viewBox;
        ViewBoxText = FormattableString.Invariant($"{viewBox.X} {viewBox.Y} {viewBox.Width} {viewBox.Height}");
        _shapes = shapes;
    }

    /// <summary>The SVG path data, or null when the image was created from markup.</summary>
    public string? PathData { get; }

    /// <summary>The original SVG markup, or null when the image was created from path data.</summary>
    public string? Markup { get; }

    /// <summary>The view box in SVG user units.</summary>
    public RectangleF ViewBox { get; }

    internal string ViewBoxText { get; }

    /// <summary>Creates an image from an <see cref="SvgIcon"/> of the icon registry.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="icon"/> is null.</exception>
    public static EnterpriseSvgImage FromIcon(SvgIcon icon)
    {
        ArgumentNullException.ThrowIfNull(icon);
        return new EnterpriseSvgImage(icon.PathData, icon.ViewBox);
    }

    /// <summary>Creates an image from SVG markup such as <c>&lt;svg viewBox="0 0 24 24"&gt;&lt;path d="..."/&gt;&lt;/svg&gt;</c>.</summary>
    /// <exception cref="ArgumentException">The markup is empty, too large, not valid XML or contains no drawable shape.</exception>
    public static EnterpriseSvgImage FromMarkup(string markup)
    {
        if (string.IsNullOrWhiteSpace(markup)) throw new ArgumentException("SVG markup is required.", nameof(markup));
        if (markup.Length > MaxMarkupLength) throw new ArgumentException("SVG markup is too large.", nameof(markup));
        var (viewBox, shapes) = SvgMarkupParser.Parse(markup);
        if (shapes.Length == 0) throw new ArgumentException("The SVG contains no supported shape.", nameof(markup));
        return new EnterpriseSvgImage(markup, viewBox, shapes);
    }

    /// <summary>Paints the image centered in <paramref name="bounds"/>, scaled uniformly to fit.</summary>
    public void Draw(Graphics graphics, Rectangle bounds, Color color)
    {
        ArgumentNullException.ThrowIfNull(graphics);
        if (bounds.Width <= 0 || bounds.Height <= 0 || ViewBox.Width <= 0 || ViewBox.Height <= 0) return;
        var scale = Math.Min(bounds.Width / ViewBox.Width, bounds.Height / ViewBox.Height);
        var state = graphics.Save();
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TranslateTransform(
            bounds.X + (bounds.Width - ViewBox.Width * scale) / 2f - ViewBox.X * scale,
            bounds.Y + (bounds.Height - ViewBox.Height * scale) / 2f - ViewBox.Y * scale);
        graphics.ScaleTransform(scale, scale);
        using var brush = new SolidBrush(color);
        foreach (var shape in _shapes)
        {
            if (shape.Fill) graphics.FillPath(brush, shape.Path);
            if (shape.StrokeWidth <= 0f) continue;
            using var pen = new Pen(color, shape.StrokeWidth) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round };
            graphics.DrawPath(pen, shape.Path);
        }

        graphics.Restore(state);
    }

    /// <summary>Renders the image into a new square bitmap.</summary>
    public Bitmap ToBitmap(int size, Color color)
    {
        if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size), size, "The size must be positive.");
        var bitmap = new Bitmap(size, size);
        using var g = Graphics.FromImage(bitmap);
        Draw(g, new Rectangle(0, 0, size, size), color);
        return bitmap;
    }

    private static RectangleF ParseViewBox(string text)
    {
        var parts = text.Split(ListSeparators, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4
            || !float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
            || !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y)
            || !float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var w)
            || !float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var h)
            || w <= 0 || h <= 0)
        {
            throw new ArgumentException("The view box must be 'min-x min-y width height' with a positive size.");
        }

        return new RectangleF(x, y, w, h);
    }

    internal sealed class SvgShape
    {
        public SvgShape(GraphicsPath path, bool fill, float strokeWidth, bool evenOdd)
        {
            Path = path;
            Fill = fill;
            StrokeWidth = strokeWidth;
            path.FillMode = evenOdd ? FillMode.Alternate : FillMode.Winding;
        }

        public GraphicsPath Path { get; }
        public bool Fill { get; }
        public float StrokeWidth { get; }
    }

    /// <summary>Parses the supported SVG subset into shapes.</summary>
    private static class SvgMarkupParser
    {
        public static (RectangleF ViewBox, SvgShape[] Shapes) Parse(string markup)
        {
            XDocument document;
            try
            {
                var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersFromEntities = 0 };
                using var text = new StringReader(markup);
                using var reader = XmlReader.Create(text, settings);
                document = XDocument.Load(reader);
            }
            catch (XmlException ex)
            {
                throw new ArgumentException("The SVG markup is not valid XML.", nameof(markup), ex);
            }

            var root = document.Root;
            if (root is null || root.Name.LocalName != "svg") throw new ArgumentException("The root element must be <svg>.", nameof(markup));
            var viewBox = ReadViewBox(root);
            var shapes = new List<SvgShape>();
            try
            {
                foreach (var element in root.Descendants())
                {
                    if (element.Ancestors().Any(a => a.Name.LocalName is "defs" or "clipPath" or "mask" or "symbol" or "pattern")) continue;
                    var shape = Build(element);
                    if (shape is not null) shapes.Add(shape);
                }
            }
            catch (FormatException ex)
            {
                throw new ArgumentException("The SVG contains a malformed shape.", nameof(markup), ex);
            }

            return (viewBox, shapes.ToArray());
        }

        private static RectangleF ReadViewBox(XElement root)
        {
            var viewBox = (string?)root.Attribute("viewBox");
            if (viewBox is not null && viewBox.Trim().Length > 0) return ParseViewBox(viewBox);
            var width = Number((string?)root.Attribute("width"), 24f);
            var height = Number((string?)root.Attribute("height"), 24f);
            return new RectangleF(0, 0, width > 0 ? width : 24f, height > 0 ? height : 24f);
        }

        private static SvgShape? Build(XElement e)
        {
            GraphicsPath path;
            switch (e.Name.LocalName)
            {
                case "path":
                    var d = (string?)e.Attribute("d");
                    if (d is null || d.Trim().Length == 0) return null;
                    path = SvgPathParser.Parse(d);
                    break;
                case "rect":
                    path = Rect(Attr(e, "x"), Attr(e, "y"), Attr(e, "width"), Attr(e, "height"), Attr(e, "rx"), Attr(e, "ry"));
                    break;
                case "circle":
                    path = new GraphicsPath();
                    var r = Attr(e, "r");
                    path.AddEllipse(Attr(e, "cx") - r, Attr(e, "cy") - r, r * 2, r * 2);
                    break;
                case "ellipse":
                    path = new GraphicsPath();
                    path.AddEllipse(Attr(e, "cx") - Attr(e, "rx"), Attr(e, "cy") - Attr(e, "ry"), Attr(e, "rx") * 2, Attr(e, "ry") * 2);
                    break;
                case "line":
                    path = new GraphicsPath();
                    path.AddLine(Attr(e, "x1"), Attr(e, "y1"), Attr(e, "x2"), Attr(e, "y2"));
                    break;
                case "polyline":
                case "polygon":
                    var points = Points((string?)e.Attribute("points"));
                    if (points.Length < 2) return null;
                    path = new GraphicsPath();
                    path.AddLines(points);
                    if (e.Name.LocalName == "polygon") path.CloseFigure();
                    break;
                default:
                    return null;
            }

            var fill = Inherited(e, "fill") ?? "black";
            var stroke = Inherited(e, "stroke") ?? "none";
            var paintFill = fill != "none";
            var strokeWidth = stroke != "none" ? Math.Max(0f, Number(Inherited(e, "stroke-width"), 1f)) : 0f;
            if (!paintFill && strokeWidth <= 0f)
            {
                path.Dispose();
                return null;
            }

            var evenOdd = (Inherited(e, "fill-rule") ?? string.Empty) == "evenodd";
            return new SvgShape(path, paintFill, strokeWidth, evenOdd);
        }

        private static GraphicsPath Rect(float x, float y, float w, float h, float rx, float ry)
        {
            var path = new GraphicsPath();
            if (rx <= 0 && ry > 0) rx = ry;
            if (ry <= 0 && rx > 0) ry = rx;
            rx = Math.Min(rx, w / 2);
            ry = Math.Min(ry, h / 2);
            if (rx <= 0 || ry <= 0)
            {
                path.AddRectangle(new RectangleF(x, y, w, h));
                return path;
            }

            var dx = rx * 2;
            var dy = ry * 2;
            path.AddArc(x, y, dx, dy, 180, 90);
            path.AddArc(x + w - dx, y, dx, dy, 270, 90);
            path.AddArc(x + w - dx, y + h - dy, dx, dy, 0, 90);
            path.AddArc(x, y + h - dy, dx, dy, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static PointF[] Points(string? text)
        {
            if (text is null || text.Trim().Length == 0) return Array.Empty<PointF>();
            var numbers = text.Split(ListSeparators, StringSplitOptions.RemoveEmptyEntries).Select(n => Number(n, 0f)).ToArray();
            var list = new List<PointF>();
            for (var i = 0; i + 1 < numbers.Length; i += 2) list.Add(new PointF(numbers[i], numbers[i + 1]));
            return list.ToArray();
        }

        private static float Attr(XElement e, string name) => Number((string?)e.Attribute(name), 0f);

        private static float Number(string? text, float fallback)
        {
            if (text is null || text.Trim().Length == 0) return fallback;
            var trimmed = text.Trim();
            if (trimmed.EndsWith("px", StringComparison.Ordinal)) trimmed = trimmed[..^2];
            return float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : fallback;
        }

        /// <summary>Presentation attribute from the element, its style attribute or the nearest ancestor that sets it.</summary>
        private static string? Inherited(XElement e, string name)
        {
            for (XElement? current = e; current is not null; current = current.Parent)
            {
                var direct = (string?)current.Attribute(name);
                if (direct is not null && direct.Trim().Length > 0) return direct.Trim();
                var style = (string?)current.Attribute("style");
                if (style is null || style.Trim().Length == 0) continue;
                foreach (var declaration in style.Split(';'))
                {
                    var colon = declaration.IndexOf(':');
                    if (colon > 0 && declaration.Substring(0, colon).Trim() == name) return declaration.Substring(colon + 1).Trim();
                }
            }

            return null;
        }
    }
}

/// <summary>Converts an <see cref="EnterpriseSvgImage"/> to and from text and to a constructor call so the designer can serialize it.</summary>
public sealed class EnterpriseSvgImageConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) =>
        destinationType == typeof(string) || destinationType == typeof(InstanceDescriptor) || base.CanConvertTo(context, destinationType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is not string text) return base.ConvertFrom(context, culture, value);
        text = text.Trim();
        if (text.Length == 0) return null;
        return text[0] == '<' ? EnterpriseSvgImage.FromMarkup(text) : new EnterpriseSvgImage(text);
    }

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (value is EnterpriseSvgImage image)
        {
            if (destinationType == typeof(string)) return image.Markup ?? image.PathData ?? string.Empty;
            if (destinationType == typeof(InstanceDescriptor))
            {
                if (image.Markup is not null)
                {
                    return new InstanceDescriptor(typeof(EnterpriseSvgImage).GetMethod(nameof(EnterpriseSvgImage.FromMarkup), new[] { typeof(string) }), new object[] { image.Markup });
                }

                ConstructorInfo? ctor = typeof(EnterpriseSvgImage).GetConstructor(new[] { typeof(string), typeof(string) });
                return new InstanceDescriptor(ctor, new object[] { image.PathData!, image.ViewBoxText });
            }
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }
}

/// <summary>Converts SVG path data into a <see cref="GraphicsPath"/> in the coordinates of the path.</summary>
internal static class SvgPathParser
{
    public static GraphicsPath Parse(string data)
    {
        var path = new GraphicsPath();
        var reader = new Reader(data);
        PointF current = default, start = default, lastCubic = default, lastQuad = default;
        var command = '\0';
        var previous = '\0';
        var started = false;
        try
        {
            while (true)
            {
                reader.SkipSeparators();
                if (reader.AtEnd) break;
                if (reader.AtCommand)
                {
                    command = reader.ReadCommand();
                    if (command is 'Z' or 'z')
                    {
                        path.CloseFigure();
                        current = start;
                        previous = 'Z';
                        continue;
                    }
                }
                else if (command == '\0')
                {
                    throw new FormatException("Path data must start with a command.");
                }

                if (!started && char.ToUpperInvariant(command) != 'M') throw new FormatException("Path data must start with a move-to command.");
                started = true;

                var relative = char.IsLower(command);
                var origin = relative ? current : default;
                var kind = char.ToUpperInvariant(command);
                switch (kind)
                {
                    case 'M':
                        current = start = reader.ReadPoint(origin);
                        path.StartFigure();
                        command = relative ? 'l' : 'L';
                        break;
                    case 'L':
                    {
                        var p = reader.ReadPoint(origin);
                        path.AddLine(current, p);
                        current = p;
                        break;
                    }
                    case 'H':
                    {
                        var p = new PointF(reader.ReadNumber() + origin.X, current.Y);
                        path.AddLine(current, p);
                        current = p;
                        break;
                    }
                    case 'V':
                    {
                        var p = new PointF(current.X, reader.ReadNumber() + origin.Y);
                        path.AddLine(current, p);
                        current = p;
                        break;
                    }
                    case 'C':
                    {
                        var c1 = reader.ReadPoint(origin);
                        var c2 = reader.ReadPoint(origin);
                        var p = reader.ReadPoint(origin);
                        path.AddBezier(current, c1, c2, p);
                        lastCubic = c2;
                        current = p;
                        break;
                    }
                    case 'S':
                    {
                        var c1 = previous is 'C' or 'S' ? Reflect(lastCubic, current) : current;
                        var c2 = reader.ReadPoint(origin);
                        var p = reader.ReadPoint(origin);
                        path.AddBezier(current, c1, c2, p);
                        lastCubic = c2;
                        current = p;
                        break;
                    }
                    case 'Q':
                    {
                        var q = reader.ReadPoint(origin);
                        var p = reader.ReadPoint(origin);
                        AddQuad(path, current, q, p);
                        lastQuad = q;
                        current = p;
                        break;
                    }
                    case 'T':
                    {
                        var q = previous is 'Q' or 'T' ? Reflect(lastQuad, current) : current;
                        var p = reader.ReadPoint(origin);
                        AddQuad(path, current, q, p);
                        lastQuad = q;
                        current = p;
                        break;
                    }
                    case 'A':
                    {
                        var rx = reader.ReadNumber();
                        var ry = reader.ReadNumber();
                        var rotation = reader.ReadNumber();
                        var large = reader.ReadFlag();
                        var sweep = reader.ReadFlag();
                        var p = reader.ReadPoint(origin);
                        AddArc(path, current, rx, ry, rotation, large, sweep, p);
                        current = p;
                        break;
                    }
                    default:
                        throw new FormatException($"Unsupported path command '{command}'.");
                }

                previous = kind;
            }
        }
        catch
        {
            path.Dispose();
            throw;
        }

        return path;
    }

    private static PointF Reflect(PointF control, PointF about) => new(2 * about.X - control.X, 2 * about.Y - control.Y);

    private static void AddQuad(GraphicsPath path, PointF p0, PointF q, PointF p1) =>
        path.AddBezier(p0, new PointF(p0.X + 2f / 3f * (q.X - p0.X), p0.Y + 2f / 3f * (q.Y - p0.Y)), new PointF(p1.X + 2f / 3f * (q.X - p1.X), p1.Y + 2f / 3f * (q.Y - p1.Y)), p1);

    /// <summary>Converts an SVG endpoint arc into cubic Bezier segments (W3C implementation notes, appendix F.6).</summary>
    private static void AddArc(GraphicsPath path, PointF p0, float rx, float ry, float rotationDegrees, bool large, bool sweep, PointF p1)
    {
        if (p0 == p1) return;
        rx = Math.Abs(rx);
        ry = Math.Abs(ry);
        if (rx < 1e-6f || ry < 1e-6f)
        {
            path.AddLine(p0, p1);
            return;
        }

        var phi = rotationDegrees * Math.PI / 180.0;
        double cos = Math.Cos(phi), sin = Math.Sin(phi);
        double dx = (p0.X - p1.X) / 2.0, dy = (p0.Y - p1.Y) / 2.0;
        var x1 = cos * dx + sin * dy;
        var y1 = -sin * dx + cos * dy;
        double rx2 = (double)rx * rx, ry2 = (double)ry * ry;
        var lambda = x1 * x1 / rx2 + y1 * y1 / ry2;
        if (lambda > 1)
        {
            var scale = Math.Sqrt(lambda);
            rx = (float)(rx * scale);
            ry = (float)(ry * scale);
            rx2 = (double)rx * rx;
            ry2 = (double)ry * ry;
        }

        var denominator = rx2 * y1 * y1 + ry2 * x1 * x1;
        var coefficient = denominator == 0 ? 0 : Math.Sqrt(Math.Max(0, (rx2 * ry2 - rx2 * y1 * y1 - ry2 * x1 * x1) / denominator));
        if (large == sweep) coefficient = -coefficient;
        var cxp = coefficient * rx * y1 / ry;
        var cyp = -coefficient * ry * x1 / rx;
        var cx = cos * cxp - sin * cyp + (p0.X + p1.X) / 2.0;
        var cy = sin * cxp + cos * cyp + (p0.Y + p1.Y) / 2.0;
        var theta = Math.Atan2((y1 - cyp) / ry, (x1 - cxp) / rx);
        var delta = Math.Atan2((-y1 - cyp) / ry, (-x1 - cxp) / rx) - theta;
        if (!sweep && delta > 0) delta -= 2 * Math.PI;
        else if (sweep && delta < 0) delta += 2 * Math.PI;

        var segments = Math.Max(1, (int)Math.Ceiling(Math.Abs(delta) / (Math.PI / 2) - 1e-9));
        var step = delta / segments;
        var t = 4.0 / 3.0 * Math.Tan(step / 4);
        var from = p0;
        for (var i = 0; i < segments; i++)
        {
            var a = theta + i * step;
            var b = a + step;
            var c1 = Map(Math.Cos(a) - t * Math.Sin(a), Math.Sin(a) + t * Math.Cos(a));
            var c2 = Map(Math.Cos(b) + t * Math.Sin(b), Math.Sin(b) - t * Math.Cos(b));
            var end = i == segments - 1 ? p1 : Map(Math.Cos(b), Math.Sin(b));
            path.AddBezier(from, c1, c2, end);
            from = end;
        }

        PointF Map(double x, double y) => new((float)(cx + cos * rx * x - sin * ry * y), (float)(cy + sin * rx * x + cos * ry * y));
    }

    private sealed class Reader
    {
        private readonly string _text;
        private int _position;

        public Reader(string text) => _text = text;

        public bool AtEnd => _position >= _text.Length;

        public bool AtCommand => !AtEnd && char.IsLetter(_text[_position]) && _text[_position] is not ('e' or 'E');

        public char ReadCommand() => _text[_position++];

        public void SkipSeparators()
        {
            while (_position < _text.Length && (char.IsWhiteSpace(_text[_position]) || _text[_position] == ',')) _position++;
        }

        public PointF ReadPoint(PointF origin)
        {
            var x = ReadNumber();
            var y = ReadNumber();
            return new PointF(x + origin.X, y + origin.Y);
        }

        public bool ReadFlag()
        {
            SkipSeparators();
            if (AtEnd || _text[_position] is not ('0' or '1')) throw new FormatException("Arc flag expected.");
            return _text[_position++] == '1';
        }

        public float ReadNumber()
        {
            SkipSeparators();
            var start = _position;
            if (_position < _text.Length && _text[_position] is '+' or '-') _position++;
            var digits = ConsumeDigits();
            if (_position < _text.Length && _text[_position] == '.')
            {
                _position++;
                digits += ConsumeDigits();
            }

            if (digits == 0) throw new FormatException("Number expected in path data.");
            if (_position < _text.Length && _text[_position] is 'e' or 'E')
            {
                var save = _position++;
                if (_position < _text.Length && _text[_position] is '+' or '-') _position++;
                if (ConsumeDigits() == 0) _position = save;
            }

            return float.Parse(_text.Substring(start, _position - start), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private int ConsumeDigits()
        {
            var count = 0;
            while (_position < _text.Length && _text[_position] is >= '0' and <= '9')
            {
                _position++;
                count++;
            }

            return count;
        }
    }
}
