using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Theming;

namespace EnterpriseUI.WinForms;

public enum EnterpriseFormShape { Rectangle, RoundedRectangle, Ellipse, Polygon, Path, Mask }

/// <summary>Borderless window with a custom outline: rounded rectangle, ellipse, polygon, vector path or the opaque pixels of an image; empty areas can drag the window.</summary>
[DefaultEvent("ShapeChanged")]
public class EnterpriseShapedForm : Form, IThemeableControl
{
    private readonly EventHandler<ITheme> _themeHandler;
    private readonly List<PointF> _polygon = new();
    private ControlColors _colors = ControlColors.FromTheme(ThemeManager.Instance.CurrentTheme);
    private EnterpriseFormShape _shape = EnterpriseFormShape.RoundedRectangle;
    private int _cornerRadius = 24;
    private GraphicsPath? _customPath;
    private SizeF _customReference;
    private Bitmap? _mask;
    private int _maskThreshold = 128;
    private bool _dragByBody = true;
    private Color _borderColor = Color.Empty;
    private float _borderWidth = 2f;

    public EnterpriseShapedForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer, true);
        Font = new Font("Segoe UI", 9f);
        ClientSize = new Size(420, 300);
        _themeHandler = (_, theme) =>
        {
            if (IsDisposed) return;
            _colors = ControlColors.FromTheme(theme);
            ApplyColors();
        };
        ThemeManager.Instance.ThemeChanged += _themeHandler;
        ApplyColors();
        RebuildRegion();
    }

    public string? ThemeOverride { get; set; }

    public void OnThemeChanged(ITheme newTheme) => _themeHandler(this, newTheme);

    [DefaultValue(EnterpriseFormShape.RoundedRectangle)]
    public EnterpriseFormShape Shape
    {
        get => _shape;
        set
        {
            _shape = value;
            RebuildRegion();
        }
    }

    [DefaultValue(24)]
    public int CornerRadius
    {
        get => _cornerRadius;
        set
        {
            _cornerRadius = Math.Max(0, value);
            RebuildRegion();
        }
    }

    /// <summary>Corners of the <see cref="EnterpriseFormShape.Polygon"/> shape as fractions (0 to 1) of the window size, so the shape scales with it.</summary>
    [Browsable(false)]
    public IReadOnlyList<PointF> PolygonPoints => _polygon;

    /// <summary>Image whose pixels with an alpha above <see cref="MaskThreshold"/> form the window for <see cref="EnterpriseFormShape.Mask"/>.</summary>
    [DefaultValue(null)]
    public Image? MaskImage
    {
        get => _mask;
        set
        {
            _mask?.Dispose();
            _mask = value == null ? null : new Bitmap(value);
            if (_shape == EnterpriseFormShape.Mask) RebuildRegion();
        }
    }

    [DefaultValue(128)]
    public int MaskThreshold
    {
        get => _maskThreshold;
        set
        {
            _maskThreshold = Math.Clamp(value, 0, 255);
            if (_shape == EnterpriseFormShape.Mask) RebuildRegion();
        }
    }

    /// <summary>Lets a press on the empty window surface move the window.</summary>
    [DefaultValue(true)]
    public bool AllowDragByBody
    {
        get => _dragByBody;
        set => _dragByBody = value;
    }

    /// <summary>Outline color; the theme accent when empty.</summary>
    public Color BorderColor
    {
        get => _borderColor;
        set
        {
            _borderColor = value;
            Invalidate();
        }
    }

    private bool ShouldSerializeBorderColor() => !_borderColor.IsEmpty;
    private void ResetBorderColor() => BorderColor = Color.Empty;

    [DefaultValue(2f)]
    public float BorderWidth
    {
        get => _borderWidth;
        set
        {
            _borderWidth = Math.Clamp(value, 0f, 12f);
            Invalidate();
        }
    }

    public event EventHandler? ShapeChanged;

    /// <summary>Uses a polygon given in fractions of the window size.</summary>
    public void SetPolygon(IEnumerable<PointF> fractionalPoints)
    {
        _polygon.Clear();
        _polygon.AddRange(fractionalPoints);
        if (_polygon.Count < 3) throw new ArgumentException("A polygon needs at least three points.", nameof(fractionalPoints));
        _shape = EnterpriseFormShape.Polygon;
        RebuildRegion();
    }

    /// <summary>Uses a vector path drawn for <paramref name="referenceSize"/>; it is scaled when the window is resized.</summary>
    public void SetShape(GraphicsPath path, SizeF referenceSize)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (referenceSize.Width <= 0 || referenceSize.Height <= 0) throw new ArgumentOutOfRangeException(nameof(referenceSize));
        _customPath?.Dispose();
        _customPath = (GraphicsPath)path.Clone();
        _customReference = referenceSize;
        _shape = EnterpriseFormShape.Path;
        RebuildRegion();
    }

    /// <summary>Takes the outline drawn in a shape editor.</summary>
    public void SetShape(EnterpriseShapeEditor editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        var size = new SizeF(Math.Max(1, editor.Width), Math.Max(1, editor.Height));
        using var path = editor.CreatePath(new RectangleF(PointF.Empty, size));
        SetShape(path, size);
    }

    /// <summary>Goes back to the plain rectangle.</summary>
    public void ClearShape()
    {
        _customPath?.Dispose();
        _customPath = null;
        _polygon.Clear();
        _shape = EnterpriseFormShape.Rectangle;
        RebuildRegion();
    }

    /// <summary>The outline in client coordinates; the caller disposes it.</summary>
    public GraphicsPath CreateShapePath()
    {
        var w = Math.Max(1, ClientSize.Width);
        var h = Math.Max(1, ClientSize.Height);
        var path = new GraphicsPath();
        switch (_shape)
        {
            case EnterpriseFormShape.RoundedRectangle:
                path.Dispose();
                return Chrome.RoundedRect(new Rectangle(0, 0, w, h), Math.Min(_cornerRadius, Math.Min(w, h) / 2));
            case EnterpriseFormShape.Ellipse:
                path.AddEllipse(0, 0, w, h);
                break;
            case EnterpriseFormShape.Polygon when _polygon.Count >= 3:
                path.AddPolygon(_polygon.Select(p => new PointF(p.X * w, p.Y * h)).ToArray());
                break;
            case EnterpriseFormShape.Path when _customPath != null:
                path.AddPath(_customPath, false);
                using (var scale = new Matrix())
                {
                    scale.Scale(w / _customReference.Width, h / _customReference.Height);
                    path.Transform(scale);
                }

                break;
            case EnterpriseFormShape.Mask when _mask != null:
                AddMaskRuns(path, w, h);
                if (path.PointCount == 0) path.AddRectangle(new Rectangle(0, 0, w, h));
                break;
            default:
                path.AddRectangle(new Rectangle(0, 0, w, h));
                break;
        }

        return path;
    }

    /// <summary>Whether a press at the screen point moves the window.</summary>
    public bool IsDragPoint(Point screenPoint)
    {
        if (!_dragByBody || !Bounds.Contains(screenPoint)) return false;
        var client = PointToClient(screenPoint);
        if (GetChildAtPoint(client) != null) return false;
        using var path = CreateShapePath();
        return path.IsVisible(client);
    }

    private void AddMaskRuns(GraphicsPath path, int width, int height)
    {
        using var scaled = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(scaled))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            g.DrawImage(_mask!, 0, 0, width, height);
        }

        var data = scaled.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var bytes = new byte[Math.Abs(data.Stride) * height];
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
            for (var y = 0; y < height; y++)
            {
                var runStart = -1;
                for (var x = 0; x <= width; x++)
                {
                    var solid = x < width && bytes[y * data.Stride + x * 4 + 3] >= _maskThreshold;
                    if (solid && runStart < 0) runStart = x;
                    else if (!solid && runStart >= 0)
                    {
                        path.AddRectangle(new Rectangle(runStart, y, x - runStart, 1));
                        runStart = -1;
                    }
                }
            }
        }
        finally
        {
            scaled.UnlockBits(data);
        }
    }

    private void RebuildRegion()
    {
        if (IsDisposed || ClientSize.IsEmpty) return;
        using var path = CreateShapePath();
        var old = Region;
        Region = new Region(path);
        old?.Dispose();
        Invalidate();
        ShapeChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyColors()
    {
        BackColor = _colors.Surface;
        ForeColor = _colors.Text;
        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        RebuildRegion();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == ShellNative.WmNcHitTest && IsDragPoint(ShellNative.PointFromLParam(m.LParam)))
        {
            m.Result = (IntPtr)ShellNative.HtCaption;
            return;
        }

        base.WndProc(ref m);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (_borderWidth <= 0) return;
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = CreateShapePath();
        using var pen = new Pen(_borderColor.IsEmpty ? _colors.Primary : _borderColor, _borderWidth) { Alignment = PenAlignment.Inset };
        g.DrawPath(pen, path);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ThemeManager.Instance.ThemeChanged -= _themeHandler;
            _customPath?.Dispose();
            _mask?.Dispose();
        }

        base.Dispose(disposing);
    }
}
