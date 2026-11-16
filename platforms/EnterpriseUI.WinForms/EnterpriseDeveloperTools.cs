using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing.Drawing2D;
using System.Text.Json;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Theming;
using EnterpriseUI.Theming.Themes;

namespace EnterpriseUI.WinForms;

public abstract class EnterpriseHierarchyToolForm : Form
{
    private readonly TreeView _hierarchy = new() { Dock = DockStyle.Fill, HideSelection = false };
    private readonly PropertyGrid _properties = new() { Dock = DockStyle.Fill, HelpVisible = true, ToolbarVisible = true };
    private readonly SplitContainer _split = new() { Dock = DockStyle.Fill, SplitterDistance = 270 };
    private Control? _root;

    protected EnterpriseHierarchyToolForm(string title)
    {
        Text = title;
        Width = 900;
        Height = 620;
        _split.Panel1.Controls.Add(_hierarchy);
        _split.Panel2.Controls.Add(_properties);
        Controls.Add(_split);
        _hierarchy.AfterSelect += (_, args) =>
        {
            if (args.Node?.Tag is Control selected)
            {
                _properties.SelectedObject = selected;
                OnControlSelected(selected);
            }
        };
    }

    public Control? RootControl => _root;
    public Control? SelectedControl => _properties.SelectedObject as Control;
    public event EventHandler<Control>? ControlSelected;

    public void Inspect(Control root)
    {
        ArgumentNullException.ThrowIfNull(root);
        _root = root;
        RefreshHierarchy();
    }

    public void RefreshHierarchy()
    {
        _hierarchy.BeginUpdate();
        try
        {
            _hierarchy.Nodes.Clear();
            if (_root is null) return;
            var rootNode = CreateNode(_root);
            _hierarchy.Nodes.Add(rootNode);
            rootNode.Expand();
            _hierarchy.SelectedNode = rootNode;
            _properties.SelectedObject = _root;
        }
        finally
        {
            _hierarchy.EndUpdate();
        }
    }

    protected virtual void OnControlSelected(Control selected) => ControlSelected?.Invoke(this, selected);

    private static TreeNode CreateNode(Control control)
    {
        var node = new TreeNode($"{control.Name} ({control.GetType().Name})") { Tag = control };
        foreach (Control child in control.Controls) node.Nodes.Add(CreateNode(child));
        return node;
    }
}

[ToolboxItem(false)]
public sealed class EnterpriseControlSpy : EnterpriseHierarchyToolForm
{
    public EnterpriseControlSpy() : base("Control Spy") { }
}

[ToolboxItem(false)]
public sealed class EnterpriseElementHierarchyEditor : EnterpriseHierarchyToolForm
{
    public EnterpriseElementHierarchyEditor() : base("Element Hierarchy Editor") { }
}

public sealed class EnterpriseFormConvertedEventArgs : EventArgs
{
    public Form Source { get; }
    public Form Result { get; }

    public EnterpriseFormConvertedEventArgs(Form source, Form result)
    {
        Source = source;
        Result = result;
    }
}

[ToolboxItem(false)]
public sealed class EnterpriseFormConverter : Component
{
    public event EventHandler<EnterpriseFormConvertedEventArgs>? Converted;

    public TForm ConvertTo<TForm>(Form source) where TForm : Form, new() => (TForm)ConvertTo(source, typeof(TForm));

    public Form ConvertTo(Form source, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type targetFormType)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(targetFormType);
        if (!typeof(Form).IsAssignableFrom(targetFormType) || targetFormType.IsAbstract
            || targetFormType.GetConstructor(Type.EmptyTypes) is null)
            throw new ArgumentException("The target type must be a non-abstract form with a public parameterless constructor.", nameof(targetFormType));
        if (source.GetType() == targetFormType) throw new ArgumentException("The source already uses the requested form type.", nameof(targetFormType));

        var target = (Form)Activator.CreateInstance(targetFormType)!;
        try
        {
            target.Text = source.Text;
            target.Name = source.Name;
            target.Bounds = source.Bounds;
            target.MinimumSize = source.MinimumSize;
            target.MaximumSize = source.MaximumSize;
            target.StartPosition = source.StartPosition;
            target.FormBorderStyle = source.FormBorderStyle;
            target.WindowState = source.WindowState;
            target.TopMost = source.TopMost;
            target.ShowInTaskbar = source.ShowInTaskbar;
            target.Icon = source.Icon;

            var children = source.Controls.Cast<Control>().ToArray();
            source.Controls.Clear();
            target.Controls.AddRange(children);
            target.ActiveControl = source.ActiveControl;
            Converted?.Invoke(this, new EnterpriseFormConvertedEventArgs(source, target));
            return target;
        }
        catch
        {
            target.Dispose();
            throw;
        }
    }
}

[ToolboxItem(false)]
public sealed class EnterpriseShapeEditor : Control
{
    private readonly List<PointF> _points = new();
    private readonly HashSet<int> _curvedSegments = new();
    private int _selectedPoint = -1;
    private bool _closed = true;

    public EnterpriseShapeEditor()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        BackColor = Color.White;
        ForeColor = Color.FromArgb(39, 93, 115);
        Size = new Size(320, 220);
        TabStop = true;
    }

    [Browsable(false)]
    public IReadOnlyList<PointF> Points => _points.AsReadOnly();

    public bool Closed
    {
        get => _closed;
        set { _closed = value; Invalidate(); ShapeChanged?.Invoke(this, EventArgs.Empty); }
    }

    [Browsable(false)]
    public IReadOnlyCollection<int> CurvedSegments => _curvedSegments.Order().ToArray();

    public int SelectedPointIndex => _selectedPoint;
    public event EventHandler? ShapeChanged;

    public void SetPoints(IEnumerable<PointF> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        _points.Clear();
        _points.AddRange(points.Select(point => new PointF(Math.Clamp(point.X, 0, 1), Math.Clamp(point.Y, 0, 1))));
        _curvedSegments.Clear();
        _selectedPoint = -1;
        Invalidate();
        ShapeChanged?.Invoke(this, EventArgs.Empty);
    }

    public void AddPoint(PointF point)
    {
        _points.Add(new PointF(Math.Clamp(point.X, 0, 1), Math.Clamp(point.Y, 0, 1)));
        _selectedPoint = _points.Count - 1;
        Invalidate();
        ShapeChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool RemovePoint(int index)
    {
        if (index < 0 || index >= _points.Count) return false;
        _points.RemoveAt(index);
        _selectedPoint = -1;
        Invalidate();
        ShapeChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public bool IsSegmentCurved(int segmentIndex) => _curvedSegments.Contains(segmentIndex);

    public void SetSegmentCurved(int segmentIndex, bool curved)
    {
        var segmentCount = Closed ? _points.Count : Math.Max(0, _points.Count - 1);
        if (segmentIndex < 0 || segmentIndex >= segmentCount) throw new ArgumentOutOfRangeException(nameof(segmentIndex));
        if (curved) _curvedSegments.Add(segmentIndex);
        else _curvedSegments.Remove(segmentIndex);
        Invalidate();
        ShapeChanged?.Invoke(this, EventArgs.Empty);
    }

    public GraphicsPath CreatePath(RectangleF bounds)
    {
        var path = new GraphicsPath();
        if (_points.Count < 2) return path;
        var absolute = _points.Select(point => new PointF(bounds.Left + point.X * bounds.Width,
            bounds.Top + point.Y * bounds.Height)).ToArray();
        path.StartFigure();
        var segmentCount = Closed ? absolute.Length : absolute.Length - 1;
        for (var index = 0; index < segmentCount; index++)
        {
            var nextIndex = (index + 1) % absolute.Length;
            var start = absolute[index];
            var end = absolute[nextIndex];
            if (_curvedSegments.Contains(index))
            {
                var previous = absolute[(index - 1 + absolute.Length) % absolute.Length];
                var after = absolute[(nextIndex + 1) % absolute.Length];
                var firstControl = new PointF(start.X + (end.X - previous.X) / 6, start.Y + (end.Y - previous.Y) / 6);
                var secondControl = new PointF(end.X - (after.X - start.X) / 6, end.Y - (after.Y - start.Y) / 6);
                path.AddBezier(start, firstControl, secondControl, end);
            }
            else
            {
                path.AddLine(start, end);
            }
        }
        if (Closed) path.CloseFigure();
        return path;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = RectangleF.Inflate(ClientRectangle, -18, -18);
        using var gridPen = new Pen(Color.FromArgb(225, 230, 232));
        e.Graphics.DrawRectangle(gridPen, bounds.X, bounds.Y, bounds.Width, bounds.Height);
        using var path = CreatePath(bounds);
        if (_points.Count >= 3 && Closed)
        {
            using var fill = new SolidBrush(Color.FromArgb(55, ForeColor));
            e.Graphics.FillPath(fill, path);
        }
        using var pen = new Pen(ForeColor, 2);
        e.Graphics.DrawPath(pen, path);
        for (var index = 0; index < _points.Count; index++)
        {
            var point = ToClient(_points[index], bounds);
            var marker = new RectangleF(point.X - 5, point.Y - 5, 10, 10);
            using var brush = new SolidBrush(index == _selectedPoint ? Color.FromArgb(207, 87, 54) : ForeColor);
            e.Graphics.FillEllipse(brush, marker);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        var hit = FindPoint(e.Location);
        if (e.Button == MouseButtons.Right)
        {
            if ((ModifierKeys & Keys.Shift) != 0)
            {
                var segment = FindSegment(e.Location);
                if (segment >= 0) SetSegmentCurved(segment, !IsSegmentCurved(segment));
            }
            else if (hit >= 0) RemovePoint(hit);
            return;
        }
        if (e.Button != MouseButtons.Left) return;
        if (hit >= 0) _selectedPoint = hit;
        else AddPoint(ToNormalized(e.Location));
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (e.Button != MouseButtons.Left || _selectedPoint < 0) return;
        _points[_selectedPoint] = ToNormalized(e.Location);
        Invalidate();
        ShapeChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Left && _selectedPoint >= 0) ShapeChanged?.Invoke(this, EventArgs.Empty);
    }

    private int FindPoint(Point point)
    {
        var bounds = RectangleF.Inflate(ClientRectangle, -18, -18);
        for (var index = _points.Count - 1; index >= 0; index--)
        {
            var candidate = ToClient(_points[index], bounds);
            if (Math.Abs(candidate.X - point.X) <= 8 && Math.Abs(candidate.Y - point.Y) <= 8) return index;
        }
        return -1;
    }

    private int FindSegment(Point point)
    {
        if (_points.Count < 2) return -1;
        var bounds = RectangleF.Inflate(ClientRectangle, -18, -18);
        var count = Closed ? _points.Count : _points.Count - 1;
        for (var index = 0; index < count; index++)
        {
            var start = ToClient(_points[index], bounds);
            var end = ToClient(_points[(index + 1) % _points.Count], bounds);
            var dx = end.X - start.X;
            var dy = end.Y - start.Y;
            var lengthSquared = dx * dx + dy * dy;
            var projection = lengthSquared == 0 ? 0 : Math.Clamp(((point.X - start.X) * dx + (point.Y - start.Y) * dy) / lengthSquared, 0, 1);
            var distanceX = point.X - (start.X + projection * dx);
            var distanceY = point.Y - (start.Y + projection * dy);
            if (distanceX * distanceX + distanceY * distanceY <= 64) return index;
        }
        return -1;
    }

    private PointF ToNormalized(Point point)
    {
        var bounds = RectangleF.Inflate(ClientRectangle, -18, -18);
        return new PointF(Math.Clamp((point.X - bounds.Left) / Math.Max(1, bounds.Width), 0, 1),
            Math.Clamp((point.Y - bounds.Top) / Math.Max(1, bounds.Height), 0, 1));
    }

    private static PointF ToClient(PointF point, RectangleF bounds) =>
        new(bounds.Left + point.X * bounds.Width, bounds.Top + point.Y * bounds.Height);
}

[ToolboxItem(false)]
public sealed class EnterpriseVisualStyleBuilder : Form
{
    private readonly TextBox _name = new() { Dock = DockStyle.Fill };
    private readonly ComboBox _type = new() { DropDownStyle = ComboBoxStyle.DropDownList, DataSource = Enum.GetValues<ThemeType>() };
    private readonly ComboBox _scheme = new() { DropDownStyle = ComboBoxStyle.DropDownList, DataSource = Enum.GetValues<ColorScheme>() };
    private readonly DataGridView _tokens = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false, AllowUserToAddRows = true };
    private readonly Panel _preview = new() { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle };
    private readonly ThemeDocument _document;

    public EnterpriseVisualStyleBuilder() : this(BuiltInThemes.CreateFluent(ColorScheme.Light)) { }

    public EnterpriseVisualStyleBuilder(Theme initialTheme)
    {
        ArgumentNullException.ThrowIfNull(initialTheme);
        Text = "Visual Style Builder";
        Width = 880;
        Height = 620;
        _document = ThemeDocument.FromTheme(initialTheme);
        _name.Text = initialTheme.Name;
        _type.SelectedItem = initialTheme.Type;
        _scheme.SelectedItem = initialTheme.Scheme;

        _tokens.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Token", Name = "Token", Width = 220 });
        _tokens.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Value", Name = "Value", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        foreach (var token in initialTheme.DesignTokens.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            _tokens.Rows.Add(token.Key, token.Value);
        _tokens.CellValueChanged += (_, _) => UpdatePreview();
        _tokens.RowsRemoved += (_, _) => UpdatePreview();

        var metadata = new TableLayoutPanel { Dock = DockStyle.Top, Height = 70, ColumnCount = 4, Padding = new Padding(8) };
        metadata.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 115));
        metadata.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        metadata.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));
        metadata.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        metadata.Controls.Add(new Label { Text = "Theme name", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        metadata.Controls.Add(_name, 1, 0);
        metadata.Controls.Add(new Label { Text = "Base style", AutoSize = true, Anchor = AnchorStyles.Left }, 2, 0);
        metadata.Controls.Add(_type, 3, 0);
        metadata.Controls.Add(new Label { Text = "Color scheme", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        metadata.Controls.Add(_scheme, 1, 1);

        var previewLabel = new Label { Text = "Preview", Dock = DockStyle.Top, Height = 32, TextAlign = ContentAlignment.MiddleLeft };
        _preview.Controls.Add(new Button { Text = "Sample action", AutoSize = true, Location = new Point(16, 16) });
        var previewPanel = new Panel { Dock = DockStyle.Right, Width = 220, Padding = new Padding(12) };
        previewPanel.Controls.Add(_preview);
        previewPanel.Controls.Add(previewLabel);
        Controls.Add(_tokens);
        Controls.Add(previewPanel);
        Controls.Add(metadata);
        UpdatePreview();
    }

    public string BaseThemeName
    {
        get => _document.BaseTheme ?? string.Empty;
        set => _document.BaseTheme = string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public void SetToken(string name, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);
        for (var rowIndex = 0; rowIndex < _tokens.Rows.Count; rowIndex++)
        {
            var row = _tokens.Rows[rowIndex];
            if (row.IsNewRow || !string.Equals(Convert.ToString(row.Cells[0].Value), name, StringComparison.OrdinalIgnoreCase)) continue;
            row.Cells[1].Value = value;
            UpdatePreview();
            return;
        }
        _tokens.Rows.Add(name, value);
        UpdatePreview();
    }

    public Theme BuildTheme(Func<string, ITheme?>? resolveBase = null)
    {
        _tokens.EndEdit();
        _document.Name = _name.Text;
        _document.Type = (ThemeType)(_type.SelectedItem ?? ThemeType.Default);
        _document.Scheme = (ColorScheme)(_scheme.SelectedItem ?? ColorScheme.Light);
        _document.Tokens.Clear();
        foreach (DataGridViewRow row in _tokens.Rows)
        {
            if (row.IsNewRow) continue;
            var key = Convert.ToString(row.Cells[0].Value)?.Trim();
            var value = Convert.ToString(row.Cells[1].Value)?.Trim();
            if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value)) _document.Tokens[key] = value;
        }
        return _document.Build(resolveBase);
    }

    public string ExportJson(Func<string, ITheme?>? resolveBase = null)
    {
        BuildTheme(resolveBase);
        return _document.ToJson();
    }

    public void SaveTheme(string path, Func<string, ITheme?>? resolveBase = null) =>
        File.WriteAllText(path, ExportJson(resolveBase));

    public void LoadTheme(string json)
    {
        var document = ThemeDocument.FromJson(json);
        _document.Name = document.Name;
        _document.Type = document.Type;
        _document.Scheme = document.Scheme;
        _document.BaseTheme = document.BaseTheme;
        _document.Version = document.Version;
        _document.Author = document.Author;
        _document.Brand = document.Brand;
        _document.License = document.License;
        _document.Tokens = new Dictionary<string, string>(document.Tokens, StringComparer.OrdinalIgnoreCase);
        _document.Variables = new Dictionary<string, string>(document.Variables, StringComparer.OrdinalIgnoreCase);
        _name.Text = document.Name;
        _type.SelectedItem = document.Type;
        _scheme.SelectedItem = document.Scheme;
        _tokens.Rows.Clear();
        foreach (var token in document.Tokens.OrderBy(pair => pair.Key, StringComparer.Ordinal)) _tokens.Rows.Add(token.Key, token.Value);
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        var primary = _tokens.Rows.Cast<DataGridViewRow>()
            .FirstOrDefault(row => !row.IsNewRow && string.Equals(Convert.ToString(row.Cells[0].Value), DesignTokens.Primary, StringComparison.OrdinalIgnoreCase));
        if (ColorTranslator.FromHtml(Convert.ToString(primary?.Cells[1].Value) ?? "#315F72") is var color)
        {
            _preview.BackColor = color;
            if (_preview.Controls.Count > 0) _preview.Controls[0].ForeColor = color.GetBrightness() < 0.55f ? Color.White : Color.Black;
        }
    }
}