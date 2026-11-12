using System.Drawing.Drawing2D;
using EnterpriseUI.Editors.Interactivity;

namespace EnterpriseUI.WinForms;

internal enum PopupHitKind { None, Close, Action, Link, Body }

internal readonly record struct PopupHit(PopupHitKind Kind, int Index = -1, string? Href = null);

/// <summary>Icon, title, formatted message, action buttons and close button shared by callouts and toasts; painted on an opaque surface.</summary>
internal sealed class PopupContent : IDisposable
{
    private readonly Font _font;
    private readonly Font _bold;
    private readonly ControlColors _c;
    private readonly float _scale;
    private MarkupLayout? _message;
    private Rectangle _glyph, _title, _close;
    private Point _messageOrigin;
    private readonly List<Rectangle> _actions = new();

    public PopupContent(Font font, ControlColors colors, float scale)
    {
        _font = font;
        _bold = new Font(font, FontStyle.Bold);
        _c = colors;
        _scale = scale;
    }

    public string? Title { get; set; }
    public string Message { get; set; } = string.Empty;
    public LabelMarkupMode Mode { get; set; }
    public string? Glyph { get; set; }
    public Image? Icon { get; set; }
    public NotificationSeverity Severity { get; set; }
    public IReadOnlyList<NotificationAction> Actions { get; set; } = Array.Empty<NotificationAction>();
    public bool ShowClose { get; set; } = true;
    public int MaxMessageLines { get; set; }
    public PopupHit Hover { get; set; }

    private int S(double v) => (int)Math.Round(v * _scale);

    public Color Accent => Severity switch
    {
        NotificationSeverity.Success => _c.Success,
        NotificationSeverity.Warning => _c.Warning,
        NotificationSeverity.Error => _c.Danger,
        _ => _c.Primary
    };

    private string GlyphText => Glyph ?? Severity switch { NotificationSeverity.Success => "✔", NotificationSeverity.Warning => "⚠", NotificationSeverity.Error => "✖", _ => "ℹ" };

    private bool HasLeading => Icon != null || GlyphText.Length > 0;
    private int Pad => S(14);
    private int LeadingColumn => HasLeading ? S(28) + S(10) : 0;
    private int CloseColumn => ShowClose ? S(22) + S(6) : 0;

    private IReadOnlyList<MarkupRun> Runs => LabelMarkup.Parse(Message, Mode);

    private int ActionsWidth => Actions.Count == 0 ? 0 : Actions.Sum(a => Chrome.MeasureText(a.Text, _bold) + S(26)) + S(8) * (Actions.Count - 1);

    /// <summary>Width that fits the text without wrapping, limited to the range.</summary>
    public int NaturalWidth(int min, int max)
    {
        using var layout = MarkupLayout.Create(Runs, _font, _c, 10000, false);
        var title = string.IsNullOrEmpty(Title) ? 0 : Chrome.MeasureText(Title, _bold) + CloseColumn;
        var body = Math.Max(Math.Max(layout.Size.Width, title), ActionsWidth);
        return Math.Clamp(body + LeadingColumn + Pad * 2, min, max);
    }

    public Size Measure(int width)
    {
        _message?.Dispose();
        var textLeft = Pad + LeadingColumn;
        var textWidth = Math.Max(S(60), width - textLeft - Pad);
        var y = Pad;
        _glyph = new Rectangle(Pad, y, S(28), S(28));
        var titleHeight = string.IsNullOrEmpty(Title) ? 0 : _bold.Height + S(2);
        _title = new Rectangle(textLeft, y + (HasLeading && titleHeight > 0 ? 0 : 0), Math.Max(10, textWidth - CloseColumn + Pad), titleHeight);
        _close = ShowClose ? new Rectangle(width - S(22) - S(8), S(8), S(22), S(22)) : Rectangle.Empty;
        y += titleHeight;
        _message = MarkupLayout.Create(Runs, _font, _c, textWidth, true, HorizontalAlignment.Left, MaxMessageLines);
        if (Message.Length > 0)
        {
            y += titleHeight > 0 ? S(4) : 0;
            _messageOrigin = new Point(textLeft, y);
            y += _message.Size.Height;
        }
        else _messageOrigin = new Point(textLeft, y);

        _actions.Clear();
        if (Actions.Count > 0)
        {
            y += S(12);
            var x = textLeft;
            foreach (var action in Actions)
            {
                var w = Chrome.MeasureText(action.Text, _bold) + S(26);
                _actions.Add(new Rectangle(x, y, w, S(28)));
                x += w + S(8);
            }

            y += S(28);
        }

        var minimum = HasLeading ? Pad * 2 + S(28) : Pad * 2;
        return new Size(width, Math.Max(y + Pad, minimum));
    }

    public PopupHit HitTest(Point p)
    {
        if (!_close.IsEmpty && _close.Contains(p)) return new PopupHit(PopupHitKind.Close);
        for (var i = 0; i < _actions.Count; i++) if (_actions[i].Contains(p)) return new PopupHit(PopupHitKind.Action, i);
        if (_message?.LinkAt(p, _messageOrigin) is { } href) return new PopupHit(PopupHitKind.Link, -1, href);
        return new PopupHit(PopupHitKind.Body);
    }

    public void Paint(Graphics g, Size size)
    {
        var accent = Accent;
        if (HasLeading)
        {
            var circle = Chrome.Blend(_c.Surface, accent, 0.16);
            using (var brush = new SolidBrush(circle)) g.FillEllipse(brush, _glyph);
            if (Icon != null) g.DrawImage(Icon, Rectangle.Inflate(_glyph, -S(5), -S(5)));
            else TextRenderer.DrawText(g, GlyphText, GlyphFonts.Get(_font.SizeInPoints + 1.5f), _glyph, accent, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        if (!string.IsNullOrEmpty(Title)) Chrome.DrawText(g, Title, _bold, _title, _c.Text);
        _message?.Draw(g, _messageOrigin);

        for (var i = 0; i < _actions.Count; i++)
        {
            var action = Actions[i];
            var r = _actions[i];
            var hot = Hover.Kind == PopupHitKind.Action && Hover.Index == i;
            if (action.IsPrimary)
            {
                var fill = Chrome.Blend(accent, Color.White, hot ? 0.15 : 0);
                Chrome.FillRounded(g, r, fill, 4);
                Chrome.DrawText(g, action.Text, _bold, r, ColorUtil.Readable(fill), ContentAlignment.MiddleCenter);
            }
            else
            {
                Chrome.FillRounded(g, r, hot ? Chrome.Blend(_c.Surface, accent, 0.14) : _c.Surface, 4);
                Chrome.DrawRoundedBorder(g, r, hot ? accent : _c.Border, 4);
                Chrome.DrawText(g, action.Text, _bold, r, _c.Text, ContentAlignment.MiddleCenter);
            }
        }

        if (_close.IsEmpty) return;
        var closeHot = Hover.Kind == PopupHitKind.Close;
        if (closeHot) Chrome.FillRounded(g, _close, Chrome.Blend(_c.Surface, _c.Muted, 0.25), _close.Width / 2);
        using var pen = new Pen(closeHot ? _c.Text : _c.Muted, Math.Max(1.5f, S(1.6))) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        var m = S(7);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.DrawLine(pen, _close.X + m, _close.Y + m, _close.Right - m, _close.Bottom - m);
        g.DrawLine(pen, _close.X + m, _close.Bottom - m, _close.Right - m, _close.Y + m);
    }

    public void Dispose()
    {
        _message?.Dispose();
        _bold.Dispose();
    }
}
