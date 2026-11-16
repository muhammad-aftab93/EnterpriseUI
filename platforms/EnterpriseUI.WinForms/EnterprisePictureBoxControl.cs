using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace EnterpriseUI.WinForms;

public enum EnterpriseImageZoomMode
{
    Fit,
    ActualSize,
    Custom
}

public sealed class EnterpriseImageChangedEventArgs : EventArgs
{
    public Image? Image { get; }
    public EnterpriseImageChangedEventArgs(Image? image) => Image = image;
}

[ToolboxItem(true)]
public sealed class EnterprisePictureBoxControl : Control
{
    private Image? _image;
    private Image? _ownedImage;
    private EnterpriseImageZoomMode _zoomMode = EnterpriseImageZoomMode.Fit;
    private float _zoomFactor = 1;
    private float _minimumZoom = 0.05f;
    private float _maximumZoom = 32f;
    private PointF _panOffset;
    private Point _lastMousePoint;
    private bool _panning;
    private bool _showMiniToolbar = true;

    public EnterprisePictureBoxControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.Selectable, true);
        TabStop = true;
        BackColor = Color.FromArgb(32, 36, 39);
        ForeColor = Color.White;
        AccessibleRole = AccessibleRole.Graphic;
        AccessibleName = "Image viewer";
        Size = new Size(480, 320);
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Image? Image
    {
        get => _image;
        set
        {
            if (ReferenceEquals(_image, value)) return;
            ReleaseOwnedImage();
            _image = value;
            ResetView();
            ImageChanged?.Invoke(this, new EnterpriseImageChangedEventArgs(_image));
            Invalidate();
        }
    }

    public EnterpriseImageZoomMode ZoomMode
    {
        get => _zoomMode;
        set
        {
            if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
            if (_zoomMode == value) return;
            _zoomMode = value;
            if (value != EnterpriseImageZoomMode.Custom) _panOffset = PointF.Empty;
            OnZoomChanged();
        }
    }

    public float ZoomFactor
    {
        get => _zoomFactor;
        set
        {
            if (!float.IsFinite(value) || value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
            var next = Math.Clamp(value, _minimumZoom, _maximumZoom);
            if (Math.Abs(_zoomFactor - next) < 0.0001f && _zoomMode == EnterpriseImageZoomMode.Custom) return;
            _zoomFactor = next;
            _zoomMode = EnterpriseImageZoomMode.Custom;
            OnZoomChanged();
        }
    }

    public PointF PanOffset
    {
        get => _panOffset;
        set { _panOffset = value; Invalidate(); }
    }

    public bool ShowMiniToolbar
    {
        get => _showMiniToolbar;
        set
        {
            if (_showMiniToolbar == value) return;
            _showMiniToolbar = value;
            Invalidate();
        }
    }

    public float MinimumZoom
    {
        get => _minimumZoom;
        set
        {
            if (!float.IsFinite(value) || value <= 0 || value > _maximumZoom) throw new ArgumentOutOfRangeException(nameof(value));
            _minimumZoom = value;
            if (_zoomMode == EnterpriseImageZoomMode.Custom && _zoomFactor < value) ZoomFactor = value;
        }
    }

    public float MaximumZoom
    {
        get => _maximumZoom;
        set
        {
            if (!float.IsFinite(value) || value < _minimumZoom) throw new ArgumentOutOfRangeException(nameof(value));
            _maximumZoom = value;
            if (_zoomMode == EnterpriseImageZoomMode.Custom && _zoomFactor > value) ZoomFactor = value;
        }
    }
    public event EventHandler<EnterpriseImageChangedEventArgs>? ImageChanged;
    public event EventHandler? ZoomChanged;

    public void LoadImage(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var source = Image.FromFile(path);
        var bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppPArgb);
        using (var graphics = Graphics.FromImage(bitmap)) graphics.DrawImageUnscaled(source, 0, 0);
        SetOwnedImage(bitmap);
    }

    public void ClearImage() => Image = null;

    public void FitToView()
    {
        _zoomMode = EnterpriseImageZoomMode.Fit;
        _panOffset = PointF.Empty;
        OnZoomChanged();
    }

    public void ShowActualSize()
    {
        _zoomMode = EnterpriseImageZoomMode.ActualSize;
        _panOffset = PointF.Empty;
        OnZoomChanged();
    }

    public void ZoomIn() => ZoomAt(1.25f, ViewportCenter);
    public void ZoomOut() => ZoomAt(0.8f, ViewportCenter);

    public void ZoomAt(float factor, Point location)
    {
        if (_image is null || !float.IsFinite(factor) || factor <= 0) return;
        var oldScale = GetScale();
        var newScale = Math.Clamp(oldScale * factor, _minimumZoom, _maximumZoom);
        if (Math.Abs(newScale - oldScale) < 0.0001f) return;

        var area = ImageViewport;
        var oldSize = new SizeF(_image.Width * oldScale, _image.Height * oldScale);
        var oldOrigin = new PointF(area.Left + (area.Width - oldSize.Width) / 2 + _panOffset.X,
            area.Top + (area.Height - oldSize.Height) / 2 + _panOffset.Y);
        var ratio = newScale / oldScale;
        var newSize = new SizeF(_image.Width * newScale, _image.Height * newScale);
        var newOrigin = new PointF(location.X - (location.X - oldOrigin.X) * ratio,
            location.Y - (location.Y - oldOrigin.Y) * ratio);
        _panOffset = new PointF(newOrigin.X - (area.Left + (area.Width - newSize.Width) / 2),
            newOrigin.Y - (area.Top + (area.Height - newSize.Height) / 2));
        _zoomFactor = newScale;
        _zoomMode = EnterpriseImageZoomMode.Custom;
        OnZoomChanged();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var graphics = e.Graphics;
        graphics.Clear(BackColor);
        DrawCheckerboard(graphics, ImageViewport);
        if (_image is null)
        {
            TextRenderer.DrawText(graphics, "No image loaded", Font, ClientRectangle, Color.FromArgb(190, ForeColor),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
        else
        {
            var destination = GetImageBounds();
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.DrawImage(_image, destination);
        }
        if (ShowMiniToolbar) DrawToolbar(graphics);
        using var border = new Pen(Color.FromArgb(90, 255, 255, 255));
        graphics.DrawRectangle(border, 0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        ZoomAt(e.Delta > 0 ? 1.15f : 1 / 1.15f, e.Location);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        if (e.Button != MouseButtons.Left) return;
        if (ShowMiniToolbar && HandleToolbarClick(e.Location)) return;
        if (_image is null) return;
        _panning = true;
        _lastMousePoint = e.Location;
        Cursor = Cursors.Hand;
        Capture = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_panning) return;
        _panOffset = new PointF(_panOffset.X + e.X - _lastMousePoint.X, _panOffset.Y + e.Y - _lastMousePoint.Y);
        _lastMousePoint = e.Location;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left) return;
        _panning = false;
        Capture = false;
        Cursor = Cursors.Default;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Home) FitToView();
        else if (e.KeyCode is Keys.Add or Keys.Oemplus) ZoomIn();
        else if (e.KeyCode is Keys.Subtract or Keys.OemMinus) ZoomOut();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) ReleaseOwnedImage();
        base.Dispose(disposing);
    }

    private Rectangle ImageViewport => new(0, ShowMiniToolbar ? 44 : 0, ClientSize.Width, Math.Max(0, ClientSize.Height - (ShowMiniToolbar ? 44 : 0)));
    private Point ViewportCenter => new(ImageViewport.Width / 2, ImageViewport.Top + ImageViewport.Height / 2);

    private float GetScale()
    {
        if (_image is null) return 1;
        if (_zoomMode == EnterpriseImageZoomMode.ActualSize) return 1;
        if (_zoomMode == EnterpriseImageZoomMode.Custom) return _zoomFactor;
        var area = ImageViewport;
        return Math.Max(0.001f, Math.Min((float)area.Width / _image.Width, (float)area.Height / _image.Height));
    }

    private RectangleF GetImageBounds()
    {
        if (_image is null) return RectangleF.Empty;
        var area = ImageViewport;
        var width = _image.Width * GetScale();
        var height = _image.Height * GetScale();
        return new RectangleF(area.Left + (area.Width - width) / 2 + _panOffset.X,
            area.Top + (area.Height - height) / 2 + _panOffset.Y, width, height);
    }

    private void DrawToolbar(Graphics graphics)
    {
        var toolbar = new Rectangle(8, 8, 174, 28);
        using var background = new SolidBrush(Color.FromArgb(225, 22, 27, 30));
        using var border = new Pen(Color.FromArgb(90, 255, 255, 255));
        graphics.FillRectangle(background, toolbar);
        graphics.DrawRectangle(border, toolbar);
        var labels = new[] { "-", "+", "1:1", "Fit" };
        var widths = new[] { 34, 34, 50, 50 };
        var x = toolbar.Left;
        for (var index = 0; index < labels.Length; index++)
        {
            var button = new Rectangle(x, toolbar.Top, widths[index], toolbar.Height);
            TextRenderer.DrawText(graphics, labels[index], Font, button, ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            x += widths[index];
        }
    }

    private bool HandleToolbarClick(Point point)
    {
        var toolbar = new Rectangle(8, 8, 174, 28);
        if (!toolbar.Contains(point)) return false;
        var x = point.X - toolbar.Left;
        if (x < 34) ZoomOut();
        else if (x < 68) ZoomIn();
        else if (x < 118) ShowActualSize();
        else FitToView();
        return true;
    }

    private void DrawCheckerboard(Graphics graphics, Rectangle bounds)
    {
        const int tileSize = 14;
        using var first = new SolidBrush(Color.FromArgb(45, 50, 53));
        using var second = new SolidBrush(Color.FromArgb(55, 60, 63));
        for (var y = bounds.Top; y < bounds.Bottom; y += tileSize)
        for (var x = bounds.Left; x < bounds.Right; x += tileSize)
        {
            var tile = new Rectangle(x, y, Math.Min(tileSize, bounds.Right - x), Math.Min(tileSize, bounds.Bottom - y));
            graphics.FillRectangle(((x / tileSize + y / tileSize) & 1) == 0 ? first : second, tile);
        }
    }

    private void SetOwnedImage(Image image)
    {
        ReleaseOwnedImage();
        _ownedImage = image;
        _image = image;
        ResetView();
        ImageChanged?.Invoke(this, new EnterpriseImageChangedEventArgs(_image));
        Invalidate();
    }

    private void ResetView()
    {
        _zoomMode = EnterpriseImageZoomMode.Fit;
        _zoomFactor = 1;
        _panOffset = PointF.Empty;
        ZoomChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnZoomChanged()
    {
        ZoomChanged?.Invoke(this, EventArgs.Empty);
        Invalidate();
    }

    private void ReleaseOwnedImage()
    {
        if (_ownedImage is null) return;
        if (ReferenceEquals(_image, _ownedImage)) _image = null;
        _ownedImage.Dispose();
        _ownedImage = null;
    }
}