using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using EnterpriseUI.Editors.Interactivity;
using Timer = System.Windows.Forms.Timer;

namespace EnterpriseUI.WinForms;

/// <summary>Drives a 0..1 style value toward a target on a UI timer; jumps straight to the target when the system has animations turned off.</summary>
internal sealed class ValueAnimator : IDisposable
{
    private readonly Timer _timer = new() { Interval = 15 };
    private readonly Stopwatch _clock = new();
    private double _from, _to;
    private int _duration;
    private EasingKind _easing;

    public ValueAnimator(double initial = 0)
    {
        Value = initial;
        _timer.Tick += (_, _) => Tick();
    }

    public double Value { get; private set; }
    public bool IsRunning => _timer.Enabled;
    public event EventHandler? Updated;

    public void AnimateTo(double target, int milliseconds = 160, EasingKind easing = EasingKind.EaseOut)
    {
        if (!SystemInformation.UIEffectsEnabled || milliseconds <= 0)
        {
            Jump(target);
            return;
        }

        if (Math.Abs(target - Value) < 1e-6 && !_timer.Enabled) return;
        _from = Value;
        _to = target;
        _duration = milliseconds;
        _easing = easing;
        _clock.Restart();
        _timer.Start();
    }

    public void Jump(double value)
    {
        _timer.Stop();
        Value = value;
        Updated?.Invoke(this, EventArgs.Empty);
    }

    private void Tick()
    {
        var t = Math.Clamp(_clock.ElapsedMilliseconds / (double)Math.Max(1, _duration), 0, 1);
        Value = _from + (_to - _from) * Easing.Apply(_easing, t);
        if (t >= 1)
        {
            _timer.Stop();
            Value = _to;
        }

        Updated?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
    }
}

/// <summary>Frame ticker for continuously running animations (spinners, marquees, tickers).</summary>
internal sealed class FrameClock : IDisposable
{
    private readonly Timer _timer = new() { Interval = 16 };
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    public FrameClock() => _timer.Tick += (_, _) => Frame?.Invoke(this, EventArgs.Empty);

    public event EventHandler? Frame;
    public bool Enabled
    {
        get => _timer.Enabled;
        set
        {
            if (value == _timer.Enabled) return;
            if (value) _timer.Start(); else _timer.Stop();
        }
    }

    public int Interval { get => _timer.Interval; set => _timer.Interval = Math.Max(8, value); }
    public double Seconds => _clock.Elapsed.TotalSeconds;

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
    }
}

/// <summary>Accessible object that exposes a value string and extra states for controls that are not plain text.</summary>
internal sealed class ValueAccessibleObject : Control.ControlAccessibleObject
{
    private readonly Func<string?> _value;
    private readonly Func<AccessibleStates>? _states;
    private readonly Action<string>? _setter;
    private readonly Func<string?>? _name;

    public ValueAccessibleObject(Control owner, Func<string?> value, Func<AccessibleStates>? states = null, Action<string>? setter = null, string? defaultAction = null, Func<string?>? name = null) : base(owner)
    {
        _value = value;
        _states = states;
        _setter = setter;
        _name = name;
        DefaultAction = defaultAction ?? string.Empty;
    }

    public override string? Name
    {
        get => Owner.AccessibleName ?? _name?.Invoke() ?? base.Name;
        set => base.Name = value;
    }

    public override string? DefaultAction { get; }

    public override string? Value
    {
        get => _value();
        set => _setter?.Invoke(value ?? string.Empty);
    }

    public override AccessibleStates State => base.State | (_states?.Invoke() ?? AccessibleStates.None);
}

internal static class Shapes
{
    public static GraphicsPath Star(RectangleF bounds, int points = 5, double innerRatio = 0.46)
    {
        var path = new GraphicsPath();
        var cx = bounds.X + bounds.Width / 2;
        var cy = bounds.Y + bounds.Height / 2 + bounds.Height * 0.03f;
        var outer = Math.Min(bounds.Width, bounds.Height) / 2;
        var inner = outer * innerRatio;
        var pts = new PointF[points * 2];
        for (var i = 0; i < pts.Length; i++)
        {
            var radius = i % 2 == 0 ? outer : inner;
            var angle = -Math.PI / 2 + i * Math.PI / points;
            pts[i] = new PointF(cx + (float)(Math.Cos(angle) * radius), cy + (float)(Math.Sin(angle) * radius));
        }

        path.AddPolygon(pts);
        return path;
    }

    public static GraphicsPath Heart(RectangleF b)
    {
        var path = new GraphicsPath();
        var x = b.X;
        var y = b.Y;
        var w = b.Width;
        var h = b.Height;
        path.AddBezier(x + w / 2, y + h, x - w * 0.1f, y + h * 0.45f, x + w * 0.2f, y - h * 0.1f, x + w / 2, y + h * 0.28f);
        path.AddBezier(x + w / 2, y + h * 0.28f, x + w * 0.8f, y - h * 0.1f, x + w * 1.1f, y + h * 0.45f, x + w / 2, y + h);
        path.CloseFigure();
        return path;
    }

    public static GraphicsPath Circle(RectangleF b)
    {
        var path = new GraphicsPath();
        path.AddEllipse(b);
        return path;
    }

    public static GraphicsPath Diamond(RectangleF b)
    {
        var path = new GraphicsPath();
        path.AddPolygon(new[] { new PointF(b.X + b.Width / 2, b.Y), new PointF(b.Right, b.Y + b.Height / 2), new PointF(b.X + b.Width / 2, b.Bottom), new PointF(b.X, b.Y + b.Height / 2) });
        return path;
    }

    /// <summary>Soft shadow under a path by stacking translucent strokes; cheap enough for popups.</summary>
    public static void DrawSoftShadow(Graphics g, GraphicsPath path, int blur, Color color, int offsetY = 3)
    {
        var state = g.Save();
        g.TranslateTransform(0, offsetY);
        for (var i = blur; i >= 1; i--)
        {
            var alpha = (int)(color.A * Math.Pow(1 - i / (double)(blur + 1), 2.2) / Math.Max(2, blur / 2.0));
            using var pen = new Pen(Color.FromArgb(Math.Clamp(alpha, 0, 255), color), i * 2) { LineJoin = LineJoin.Round };
            g.DrawPath(pen, path);
        }

        g.Restore(state);
    }
}

internal static class ColorUtil
{
    private static readonly Regex Hex = new("^#?[0-9a-fA-F]{6}([0-9a-fA-F]{2})?$", RegexOptions.Compiled);

    public static Color? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try
        {
            if (Hex.IsMatch(value) && value.Length is 8 or 9 && value.TrimStart('#').Length == 8)
            {
                var v = Convert.ToUInt32(value.TrimStart('#'), 16);
                return Color.FromArgb((int)v);
            }

            return ColorTranslator.FromHtml(value.Trim());
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            return null;
        }
    }

    public static Color Readable(Color back) => (0.299 * back.R + 0.587 * back.G + 0.114 * back.B) > 160 ? Color.FromArgb(24, 24, 24) : Color.White;

    public static Color WithAlpha(Color color, int alpha) => Color.FromArgb(Math.Clamp(alpha, 0, 255), color);
}

internal static class GlyphFonts
{
    private static readonly Dictionary<float, Font> Cache = new();

    public static Font Get(float size)
    {
        lock (Cache)
        {
            if (!Cache.TryGetValue(size, out var font)) Cache[size] = font = new Font("Segoe UI Symbol", size, GraphicsUnit.Point);
            return font;
        }
    }
}

/// <summary>Lays out and paints inline-formatted text (bold, italic, code, links, colors) with wrapping, alignment and ellipsis.</summary>
internal sealed class MarkupLayout : IDisposable
{
    public sealed record Piece(string Text, Font Font, Rectangle Bounds, Color Color, Color? Back, string? Href);

    private sealed record Token(string Text, MarkupRun Run, Font Font, int Width, bool Space, bool Break);

    private readonly Dictionary<(bool, bool, bool, bool, bool), Font> _fonts = new();
    private readonly List<Piece> _pieces = new();

    private MarkupLayout()
    {
    }

    public IReadOnlyList<Piece> Pieces => _pieces;
    public Size Size { get; private set; }
    public int LineCount { get; private set; }
    public bool Truncated { get; private set; }

    public static MarkupLayout Create(IReadOnlyList<MarkupRun> runs, Font baseFont, ControlColors colors, int maxWidth, bool wrap, HorizontalAlignment align = HorizontalAlignment.Left, int maxLines = 0, bool ellipsis = true, Color? textColor = null)
    {
        var layout = new MarkupLayout();
        layout.Build(runs, baseFont, colors, Math.Max(1, maxWidth), wrap, align, maxLines, ellipsis, textColor ?? colors.Text);
        return layout;
    }

    private Font FontFor(Font baseFont, MarkupRun run)
    {
        var key = (run.Bold, run.Italic, run.Underline, run.Strike, run.Code);
        if (_fonts.TryGetValue(key, out var font)) return font;
        var style = (run.Bold ? FontStyle.Bold : 0) | (run.Italic ? FontStyle.Italic : 0) | (run.Underline ? FontStyle.Underline : 0) | (run.Strike ? FontStyle.Strikeout : 0);
        font = run.Code ? new Font("Consolas", baseFont.Size * 0.95f, style) : new Font(baseFont, style);
        return _fonts[key] = font;
    }

    private static int Measure(string text, Font font) => text.Length == 0 ? 0 : TextRenderer.MeasureText(text, font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine).Width;

    private void Build(IReadOnlyList<MarkupRun> runs, Font baseFont, ControlColors colors, int maxWidth, bool wrap, HorizontalAlignment align, int maxLines, bool ellipsis, Color textColor)
    {
        var tokens = new List<Token>();
        foreach (var run in runs)
        {
            var font = FontFor(baseFont, run);
            if (run.LineBreak)
            {
                tokens.Add(new Token(string.Empty, run, font, 0, false, true));
                continue;
            }

            foreach (Match m in Regex.Matches(run.Text, @"\s+|\S+"))
                tokens.Add(new Token(m.Value, run, font, Measure(m.Value, font), char.IsWhiteSpace(m.Value[0]), false));
        }

        var lines = new List<List<Token>> { new() };
        var x = 0;
        foreach (var token in tokens)
        {
            if (token.Break)
            {
                lines.Add(new List<Token>());
                x = 0;
                continue;
            }

            var line = lines[^1];
            if (wrap && !token.Space && x + token.Width > maxWidth && line.Any(t => !t.Space))
            {
                while (line.Count > 0 && line[^1].Space) line.RemoveAt(line.Count - 1);
                line = new List<Token>();
                lines.Add(line);
                x = 0;
            }

            if (token.Space && line.Count == 0 && lines.Count > 1 && wrap) continue;
            line.Add(token);
            x += token.Width;
        }

        foreach (var line in lines) while (line.Count > 0 && line[^1].Space) line.RemoveAt(line.Count - 1);

        var truncated = false;
        if (maxLines > 0 && lines.Count > maxLines)
        {
            lines.RemoveRange(maxLines, lines.Count - maxLines);
            truncated = true;
        }

        var y = 0;
        var widest = 0;
        var cut = false;
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var forceEllipsis = truncated && i == lines.Count - 1;
            var width = line.Sum(t => t.Width);
            if (ellipsis && (width > maxWidth || forceEllipsis) && line.Count > 0)
            {
                width = Ellipsize(line, maxWidth, forceEllipsis && width <= maxWidth);
                cut = true;
            }
            var height = line.Count == 0 ? baseFont.Height : line.Max(t => t.Font.Height);
            var left = align == HorizontalAlignment.Center ? (maxWidth - width) / 2 : align == HorizontalAlignment.Right ? maxWidth - width : 0;
            var px = Math.Max(0, left);
            foreach (var token in line)
            {
                if (!token.Space || token.Run.Code)
                {
                    var color = ColorUtil.Parse(token.Run.Color) ?? (token.Run.Href != null ? colors.Primary : textColor);
                    var back = token.Run.Code ? Chrome.Blend(colors.Surface, colors.Muted, 0.18) : (Color?)null;
                    _pieces.Add(new Piece(token.Text, token.Font, new Rectangle(px, y + (height - token.Font.Height) / 2, token.Width, token.Font.Height), color, back, token.Run.Href));
                }

                px += token.Width;
            }

            widest = Math.Max(widest, width);
            y += height + 2;
        }

        LineCount = lines.Count;
        Truncated = truncated || cut;
        Size = new Size(Math.Min(widest, maxWidth), Math.Max(0, y - 2));
    }

    private static int Ellipsize(List<Token> line, int maxWidth, bool alwaysAppend)
    {
        var last = line[^1];
        var dots = Measure("…", last.Font);
        var width = line.Sum(t => t.Width);
        if (alwaysAppend && width + dots <= maxWidth)
        {
            line.Add(new Token("…", last.Run, last.Font, dots, false, false));
            return width + dots;
        }

        while (line.Count > 0 && width + dots > maxWidth)
        {
            var token = line[^1];
            line.RemoveAt(line.Count - 1);
            width -= token.Width;
            var text = token.Text;
            while (text.Length > 1 && width + Measure(text, token.Font) + dots > maxWidth) text = text[..^1];
            if (text.Length > 0 && width + Measure(text, token.Font) + dots <= maxWidth && !token.Space)
            {
                var kept = new Token(text, token.Run, token.Font, Measure(text, token.Font), false, false);
                line.Add(kept);
                width += kept.Width;
                break;
            }
        }

        var tail = line.Count > 0 ? line[^1] : last;
        line.Add(new Token("…", tail.Run, tail.Font, dots, false, false));
        return width + dots;
    }

    public void Draw(Graphics g, Point origin, Color? overrideColor = null)
    {
        foreach (var piece in _pieces)
        {
            var bounds = new Rectangle(origin.X + piece.Bounds.X, origin.Y + piece.Bounds.Y, piece.Bounds.Width, piece.Bounds.Height);
            if (piece.Back is { } back) Chrome.FillRounded(g, Rectangle.Inflate(bounds, 2, 0), back, 3);
            TextRenderer.DrawText(g, piece.Text, piece.Font, bounds, overrideColor ?? piece.Color, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoClipping);
        }
    }

    public string? LinkAt(Point point, Point origin)
    {
        foreach (var piece in _pieces)
        {
            if (piece.Href != null && new Rectangle(origin.X + piece.Bounds.X, origin.Y + piece.Bounds.Y, piece.Bounds.Width, piece.Bounds.Height).Contains(point)) return piece.Href;
        }

        return null;
    }

    public void Dispose()
    {
        foreach (var font in _fonts.Values) font.Dispose();
        _fonts.Clear();
    }
}

/// <summary>32-bit premultiplied surface backed by a DIB section, pushed to a layered window with per-pixel alpha.</summary>
internal sealed class LayeredSurface : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public int Size, Width, Height;
        public short Planes, BitCount;
        public int Compression, SizeImage, XPelsPerMeter, YPelsPerMeter, ColorsUsed, ColorsImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Pt { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct Sz { public int Cx, Cy; }

    [StructLayout(LayoutKind.Sequential)]
    private struct Blend { public byte Op, Flags, Alpha, Format; }

    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateDIBSection(IntPtr dc, ref BitmapInfoHeader info, uint usage, out IntPtr bits, IntPtr section, uint offset);
    [DllImport("user32.dll")] private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr dst, ref Pt location, ref Sz size, IntPtr src, ref Pt srcPos, int key, ref Blend blend, int flags);

    private readonly IntPtr _dc;
    private readonly IntPtr _dib;
    private readonly IntPtr _previous;

    public LayeredSurface(Size size)
    {
        Size = new Size(Math.Max(1, size.Width), Math.Max(1, size.Height));
        var screen = GetDC(IntPtr.Zero);
        _dc = CreateCompatibleDC(screen);
        var info = new BitmapInfoHeader { Size = Marshal.SizeOf<BitmapInfoHeader>(), Width = Size.Width, Height = -Size.Height, Planes = 1, BitCount = 32 };
        _dib = CreateDIBSection(_dc, ref info, 0, out var bits, IntPtr.Zero, 0);
        _ = ReleaseDC(IntPtr.Zero, screen);
        if (_dib == IntPtr.Zero || bits == IntPtr.Zero) throw new InvalidOperationException("The popup surface could not be created.");
        _previous = SelectObject(_dc, _dib);
        Bitmap = new Bitmap(Size.Width, Size.Height, Size.Width * 4, PixelFormat.Format32bppPArgb, bits);
    }

    public Size Size { get; }
    public Bitmap Bitmap { get; }

    public Graphics Begin()
    {
        var g = Graphics.FromImage(Bitmap);
        g.CompositingMode = CompositingMode.SourceCopy;
        g.Clear(Color.Transparent);
        g.CompositingMode = CompositingMode.SourceOver;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        return g;
    }

    public void Present(IntPtr window, Point location, byte alpha = 255)
    {
        var screen = GetDC(IntPtr.Zero);
        var dst = new Pt { X = location.X, Y = location.Y };
        var size = new Sz { Cx = Size.Width, Cy = Size.Height };
        var src = new Pt();
        var blend = new Blend { Op = 0, Alpha = alpha, Format = 1 };
        _ = UpdateLayeredWindow(window, screen, ref dst, ref size, _dc, ref src, 0, ref blend, 2);
        _ = ReleaseDC(IntPtr.Zero, screen);
    }

    public void Dispose()
    {
        Bitmap.Dispose();
        SelectObject(_dc, _previous);
        DeleteObject(_dib);
        DeleteDC(_dc);
    }
}

/// <summary>Borderless, never-activating top-most window painted from a <see cref="LayeredSurface"/>; base of callouts and toasts.</summary>
public abstract class LayeredPopupForm : Form
{
    private const int WsExLayered = 0x80000, WsExToolWindow = 0x80, WsExNoActivate = 0x08000000, WsExTopMost = 0x8;
    private const int WmMouseActivate = 0x21, MaNoActivate = 3;
    private LayeredSurface? _surface;
    private Point _location;

    protected LayeredPopupForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WsExLayered | WsExToolWindow | WsExNoActivate | WsExTopMost;
            return cp;
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmMouseActivate)
        {
            m.Result = (IntPtr)MaNoActivate;
            return;
        }

        base.WndProc(ref m);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
    }

    protected override void OnPaint(PaintEventArgs e)
    {
    }

    /// <summary>Draws the popup into a fresh surface of <paramref name="size"/> and shows it at the screen location.</summary>
    protected void Render(Size size, Point screenLocation, Action<Graphics> draw, byte alpha = 255)
    {
        if (!IsHandleCreated) CreateHandle();
        if (_surface == null || _surface.Size != size)
        {
            _surface?.Dispose();
            _surface = new LayeredSurface(size);
        }

        using (var g = _surface.Begin()) draw(g);
        SetBoundsCore(screenLocation.X, screenLocation.Y, size.Width, size.Height, BoundsSpecified.All);
        _location = screenLocation;
        _surface.Present(Handle, screenLocation, alpha);
    }

    /// <summary>Moves and/or fades the already rendered popup without redrawing it.</summary>
    protected void Present(Point screenLocation, byte alpha)
    {
        if (_surface == null || !IsHandleCreated) return;
        _location = screenLocation;
        _surface.Present(Handle, screenLocation, alpha);
    }

    protected Point PopupLocation => _location;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _surface?.Dispose();
            _surface = null;
        }

        base.Dispose(disposing);
    }
}

/// <summary>Reports Escape presses and mouse presses outside a popup anywhere in the application.</summary>
internal sealed class PopupDismissFilter : IMessageFilter
{
    private const int WmKeyDown = 0x100, VkEscape = 0x1B, WmLButtonDown = 0x201, WmRButtonDown = 0x204, WmMButtonDown = 0x207, WmNcLButtonDown = 0xA1;
    private readonly Func<Rectangle> _popupBounds;
    private readonly Action? _onEscape;
    private readonly Action? _onOutside;

    private PopupDismissFilter(Func<Rectangle> popupBounds, Action? onEscape, Action? onOutside)
    {
        _popupBounds = popupBounds;
        _onEscape = onEscape;
        _onOutside = onOutside;
    }

    public static IDisposable Attach(Func<Rectangle> popupBounds, Action? onEscape, Action? onOutside)
    {
        var filter = new PopupDismissFilter(popupBounds, onEscape, onOutside);
        Application.AddMessageFilter(filter);
        return new Detach(filter);
    }

    public bool PreFilterMessage(ref Message m)
    {
        if (m.Msg == WmKeyDown && (int)m.WParam == VkEscape) _onEscape?.Invoke();
        else if ((m.Msg is WmLButtonDown or WmRButtonDown or WmMButtonDown or WmNcLButtonDown) && !_popupBounds().Contains(Control.MousePosition)) _onOutside?.Invoke();
        return false;
    }

    private sealed class Detach : IDisposable
    {
        private readonly PopupDismissFilter _filter;
        public Detach(PopupDismissFilter filter) => _filter = filter;
        public void Dispose() => Application.RemoveMessageFilter(_filter);
    }
}

/// <summary>Menu look that follows the control palette (used by split and drop-down buttons).</summary>
internal sealed class ThemedMenuRenderer : ToolStripProfessionalRenderer
{
    private readonly ControlColors _c;

    public ThemedMenuRenderer(ControlColors colors) : base(new Table(colors)) => _c = colors;

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? _c.Text : _c.Disabled;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Selected || !e.Item.Enabled) return;
        var rect = new Rectangle(3, 1, e.Item.Width - 6, e.Item.Height - 2);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Chrome.FillRounded(e.Graphics, rect, _c.SelectionBack, 3);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        using var pen = new Pen(_c.Border);
        var y = e.Item.Height / 2;
        e.Graphics.DrawLine(pen, 8, y, e.Item.Width - 8, y);
    }

    private sealed class Table : ProfessionalColorTable
    {
        private readonly ControlColors _c;

        public Table(ControlColors colors)
        {
            _c = colors;
            UseSystemColors = false;
        }

        public override Color ToolStripDropDownBackground => _c.Surface;
        public override Color MenuBorder => _c.Border;
        public override Color MenuItemBorder => Color.Transparent;
        public override Color ImageMarginGradientBegin => _c.Surface;
        public override Color ImageMarginGradientMiddle => _c.Surface;
        public override Color ImageMarginGradientEnd => _c.Surface;
        public override Color SeparatorDark => _c.Border;
        public override Color SeparatorLight => _c.Surface;
    }
}
