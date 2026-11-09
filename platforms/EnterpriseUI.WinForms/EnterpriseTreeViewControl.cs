using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace EnterpriseUI.WinForms;

public sealed class EnterpriseTreeNodeEventArgs : EventArgs
{
    public EnterpriseTreeNodeEventArgs(EnterpriseTreeNode node) => Node = node;
    public EnterpriseTreeNode Node { get; }
}

public sealed class EnterpriseTreeNodeCancelEventArgs : EventArgs
{
    public EnterpriseTreeNodeCancelEventArgs(EnterpriseTreeNode node) => Node = node;
    public EnterpriseTreeNode Node { get; }
    public bool Cancel { get; set; }
}

public sealed class EnterpriseTreeNodeCollection : IReadOnlyList<EnterpriseTreeNode>
{
    private readonly List<EnterpriseTreeNode> _nodes = new();
    private readonly EnterpriseTreeNode? _owner;
    private readonly EnterpriseTreeViewControl? _tree;

    internal EnterpriseTreeNodeCollection(EnterpriseTreeNode owner) => _owner = owner;
    internal EnterpriseTreeNodeCollection(EnterpriseTreeViewControl tree) => _tree = tree;

    public int Count => _nodes.Count;
    public EnterpriseTreeNode this[int index] => _nodes[index];

    public EnterpriseTreeNode Add(string text) => Add(new EnterpriseTreeNode(text));

    public EnterpriseTreeNode Add(EnterpriseTreeNode node)
    {
        Attach(node);
        _nodes.Add(node);
        Changed();
        return node;
    }

    public void AddRange(IEnumerable<EnterpriseTreeNode> nodes)
    {
        foreach (var node in nodes)
        {
            Attach(node);
            _nodes.Add(node);
        }

        Changed();
    }

    public void Insert(int index, EnterpriseTreeNode node)
    {
        Attach(node);
        _nodes.Insert(index, node);
        Changed();
    }

    public bool Remove(EnterpriseTreeNode node)
    {
        if (!_nodes.Remove(node)) return false;
        node.Detach();
        Changed();
        return true;
    }

    public void Clear()
    {
        foreach (var node in _nodes) node.Detach();
        _nodes.Clear();
        Changed();
    }

    public int IndexOf(EnterpriseTreeNode node) => _nodes.IndexOf(node);
    public IEnumerator<EnterpriseTreeNode> GetEnumerator() => _nodes.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private void Attach(EnterpriseTreeNode node)
    {
        if (node.Parent is not null || node.RootTree is not null) throw new InvalidOperationException("The node already belongs to a tree.");
        node.Parent = _owner;
        node.RootTree = _tree;
    }

    private void Changed()
    {
        if (_owner is not null && _nodes.Count > 0) _owner.HasChildrenHint = false;
        (_tree ?? _owner?.Tree)?.NodesChanged();
        if (_owner is not null && _owner.Tree is { } tree && tree.CascadeChecks) tree.RecomputeFrom(_owner);
    }
}

public sealed class EnterpriseTreeNode
{
    private CheckState _check;
    private bool _expanded;
    private string _text;

    public EnterpriseTreeNode(string text)
    {
        _text = text;
        Nodes = new EnterpriseTreeNodeCollection(this);
    }

    public string Text
    {
        get => _text;
        set { _text = value; Tree?.Redraw(); }
    }

    public object? Tag { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public Color? ForeColor { get; set; }
    public bool Bold { get; set; }
    public EnterpriseTreeNodeCollection Nodes { get; }
    public EnterpriseTreeNode? Parent { get; internal set; }
    internal EnterpriseTreeViewControl? RootTree { get; set; }
    public EnterpriseTreeViewControl? Tree => Parent is null ? RootTree : Parent.Tree;

    /// <summary>Shows the expander although there are no children yet, so they can be loaded in <c>NodeExpanding</c>.</summary>
    public bool HasChildrenHint { get; set; }

    public bool HasChildren => Nodes.Count > 0 || HasChildrenHint;

    public int Level
    {
        get
        {
            var level = 0;
            for (var p = Parent; p is not null; p = p.Parent) level++;
            return level;
        }
    }

    public string FullPath(string separator = " / ") => Parent is null ? Text : Parent.FullPath(separator) + separator + Text;

    public bool Expanded
    {
        get => _expanded;
        set
        {
            if (Tree is { } tree) tree.SetExpanded(this, value);
            else _expanded = value;
        }
    }

    internal void SetExpandedCore(bool value) => _expanded = value;

    public CheckState CheckState
    {
        get => _check;
        set
        {
            if (Tree is { } tree) tree.SetCheck(this, value);
            else _check = value;
        }
    }

    public bool Checked
    {
        get => _check == CheckState.Checked;
        set => CheckState = value ? CheckState.Checked : CheckState.Unchecked;
    }

    internal void SetCheckCore(CheckState value) => _check = value;

    internal void Detach()
    {
        Parent = null;
        RootTree = null;
    }

    public IEnumerable<EnterpriseTreeNode> Descendants()
    {
        foreach (var child in Nodes)
        {
            yield return child;
            foreach (var deeper in child.Descendants()) yield return deeper;
        }
    }

    public override string ToString() => Text;
}

/// <summary>Tree with chevron expanders, optional tri-state check boxes, lazy loading, guides and full keyboard navigation.</summary>
public class EnterpriseTreeViewControl : EnterpriseScrollableControl
{
    private readonly List<EnterpriseTreeNode> _visible = new();
    private bool _dirty = true;
    private int _updating;
    private bool _checkBoxes;
    private int _itemHeight = 28;
    private int _indent = 20;
    private EnterpriseTreeNode? _selected;
    private EnterpriseTreeNode? _hot;

    public EnterpriseTreeViewControl()
    {
        Nodes = new EnterpriseTreeNodeCollection(this);
        Size = new Size(260, 300);
        AccessibleRole = AccessibleRole.Outline;
    }

    public EnterpriseTreeNodeCollection Nodes { get; }

    public event EventHandler<EnterpriseTreeNodeEventArgs>? AfterSelect;
    public event EventHandler<EnterpriseTreeNodeCancelEventArgs>? NodeExpanding;
    public event EventHandler<EnterpriseTreeNodeEventArgs>? AfterExpand;
    public event EventHandler<EnterpriseTreeNodeEventArgs>? AfterCollapse;
    public event EventHandler<EnterpriseTreeNodeCancelEventArgs>? BeforeCheck;
    public event EventHandler<EnterpriseTreeNodeEventArgs>? AfterCheck;
    public event EventHandler<EnterpriseTreeNodeEventArgs>? NodeActivate;

    public bool CheckBoxes
    {
        get => _checkBoxes;
        set { _checkBoxes = value; Invalidate(); }
    }

    /// <summary>Checking a node checks its descendants; parents show checked, unchecked or indeterminate.</summary>
    public bool CascadeChecks { get; set; } = true;

    public bool ShowGuides { get; set; } = true;

    public int ItemHeight
    {
        get => _itemHeight;
        set { _itemHeight = Math.Max(16, value); NodesChanged(); }
    }

    public int Indent
    {
        get => _indent;
        set { _indent = Math.Max(8, value); Invalidate(); }
    }

    public EnterpriseTreeNode? SelectedNode
    {
        get => _selected;
        set
        {
            if (ReferenceEquals(_selected, value)) return;
            _selected = value;
            if (value is not null)
            {
                for (var p = value.Parent; p is not null; p = p.Parent) p.SetExpandedCore(true);
                _dirty = true;
                EnsureNodeVisible(value);
            }

            Invalidate();
            if (value is not null) AfterSelect?.Invoke(this, new EnterpriseTreeNodeEventArgs(value));
        }
    }

    protected override int LineHeight => S(_itemHeight);
    protected override Size ContentSize { get { Rebuild(); return new Size(0, _visible.Count * S(_itemHeight)); } }

    public IEnumerable<EnterpriseTreeNode> AllNodes()
    {
        foreach (var node in Nodes)
        {
            yield return node;
            foreach (var d in node.Descendants()) yield return d;
        }
    }

    public IEnumerable<EnterpriseTreeNode> CheckedNodes() => AllNodes().Where(n => n.CheckState == CheckState.Checked);

    public EnterpriseTreeNode? FindNode(Func<EnterpriseTreeNode, bool> predicate) => AllNodes().FirstOrDefault(predicate);

    public void BeginUpdate() => _updating++;

    public void EndUpdate()
    {
        if (_updating > 0) _updating--;
        if (_updating == 0) NodesChanged();
    }

    public void ExpandAll()
    {
        BeginUpdate();
        foreach (var node in AllNodes().Where(n => n.HasChildren)) SetExpanded(node, true);
        EndUpdate();
    }

    public void CollapseAll()
    {
        BeginUpdate();
        foreach (var node in AllNodes()) SetExpanded(node, false);
        EndUpdate();
    }

    /// <summary>Builds the tree from a hierarchy of objects.</summary>
    public void LoadHierarchy<T>(IEnumerable<T> roots, Func<T, string> textOf, Func<T, IEnumerable<T>?> childrenOf)
    {
        BeginUpdate();
        Nodes.Clear();
        void Add(EnterpriseTreeNodeCollection target, T item)
        {
            var node = target.Add(textOf(item));
            node.Tag = item;
            if (childrenOf(item) is { } children)
            {
                foreach (var child in children) Add(node.Nodes, child);
            }
        }

        foreach (var root in roots) Add(Nodes, root);
        EndUpdate();
    }

    internal void NodesChanged()
    {
        _dirty = true;
        if (_updating > 0 || IsDisposed) return;
        if (_selected is not null && !IsAttached(_selected)) _selected = null;
        UpdateScrollBars();
    }

    internal void Redraw()
    {
        if (_updating == 0) Invalidate();
    }

    private bool IsAttached(EnterpriseTreeNode node)
    {
        var top = node;
        while (top.Parent is not null) top = top.Parent;
        return ReferenceEquals(top.RootTree, this);
    }

    private void Rebuild()
    {
        if (!_dirty) return;
        _visible.Clear();
        var stack = new Stack<IEnumerator<EnterpriseTreeNode>>();
        stack.Push(Nodes.GetEnumerator());
        while (stack.Count > 0)
        {
            var e = stack.Peek();
            if (!e.MoveNext())
            {
                stack.Pop();
                continue;
            }

            _visible.Add(e.Current);
            if (e.Current.Expanded && e.Current.Nodes.Count > 0) stack.Push(e.Current.Nodes.GetEnumerator());
        }

        _dirty = false;
    }

    internal void SetExpanded(EnterpriseTreeNode node, bool expand)
    {
        if (node.Expanded == expand) return;
        if (expand)
        {
            var args = new EnterpriseTreeNodeCancelEventArgs(node);
            NodeExpanding?.Invoke(this, args);
            if (args.Cancel) return;
            if (node.Nodes.Count == 0)
            {
                node.HasChildrenHint = false;
                return;
            }

            node.SetExpandedCore(true);
            NodesChanged();
            AfterExpand?.Invoke(this, new EnterpriseTreeNodeEventArgs(node));
        }
        else
        {
            node.SetExpandedCore(false);
            if (_selected is not null && _selected != node && IsDescendantOf(_selected, node))
            {
                _selected = node;
                AfterSelect?.Invoke(this, new EnterpriseTreeNodeEventArgs(node));
            }

            NodesChanged();
            AfterCollapse?.Invoke(this, new EnterpriseTreeNodeEventArgs(node));
        }
    }

    private static bool IsDescendantOf(EnterpriseTreeNode node, EnterpriseTreeNode ancestor)
    {
        for (var p = node.Parent; p is not null; p = p.Parent)
        {
            if (ReferenceEquals(p, ancestor)) return true;
        }

        return false;
    }

    internal void SetCheck(EnterpriseTreeNode node, CheckState state)
    {
        if (node.CheckState == state) return;
        var args = new EnterpriseTreeNodeCancelEventArgs(node);
        BeforeCheck?.Invoke(this, args);
        if (args.Cancel) return;

        node.SetCheckCore(state);
        if (CascadeChecks && state != CheckState.Indeterminate)
        {
            foreach (var d in node.Descendants()) d.SetCheckCore(state);
            RecomputeFrom(node.Parent);
        }

        Invalidate();
        AfterCheck?.Invoke(this, new EnterpriseTreeNodeEventArgs(node));
    }

    internal void RecomputeFrom(EnterpriseTreeNode? node)
    {
        for (var p = node; p is not null; p = p.Parent)
        {
            if (p.Nodes.Count == 0) continue;
            var all = p.Nodes.All(n => n.CheckState == CheckState.Checked);
            var none = p.Nodes.All(n => n.CheckState == CheckState.Unchecked);
            p.SetCheckCore(all ? CheckState.Checked : none ? CheckState.Unchecked : CheckState.Indeterminate);
        }
    }

    public void EnsureNodeVisible(EnterpriseTreeNode node)
    {
        for (var p = node.Parent; p is not null; p = p.Parent) SetExpanded(p, true);
        Rebuild();
        var pos = _visible.IndexOf(node);
        if (pos >= 0) EnsureVisible(new Rectangle(0, pos * S(_itemHeight), 1, S(_itemHeight)));
    }

    // ---- geometry -----------------------------------------------------------------------

    private Rectangle RowRect(int pos)
    {
        var view = ViewPort;
        return new Rectangle(view.X, view.Y + pos * S(_itemHeight) - ScrollY, view.Width, S(_itemHeight));
    }

    private int ExpanderX(EnterpriseTreeNode node, Rectangle row) => row.X + S(6) + node.Level * S(_indent);

    private Rectangle ExpanderRect(EnterpriseTreeNode node, Rectangle row) => new(ExpanderX(node, row), row.Y, S(18), row.Height);

    private Rectangle CheckRect(EnterpriseTreeNode node, Rectangle row)
    {
        var size = S(16);
        return new Rectangle(ExpanderX(node, row) + S(22), row.Y + (row.Height - size) / 2, size, size);
    }

    private int TextX(EnterpriseTreeNode node, Rectangle row) => ExpanderX(node, row) + S(22) + (_checkBoxes ? S(24) : 0);

    private int PositionAt(Point location)
    {
        Rebuild();
        var view = ViewPort;
        if (!view.Contains(location)) return -1;
        var pos = (location.Y - view.Y + ScrollY) / S(_itemHeight);
        return pos >= 0 && pos < _visible.Count ? pos : -1;
    }

    public EnterpriseTreeNode? NodeAt(Point location)
    {
        var pos = PositionAt(location);
        return pos < 0 ? null : _visible[pos];
    }

    // ---- painting -----------------------------------------------------------------------

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        PaintFrame(g);
        var view = ViewPort;
        if (view.Width <= 0 || view.Height <= 0) return;
        Rebuild();
        g.SetClip(view);
        var h = S(_itemHeight);
        var first = Math.Max(0, ScrollY / h);
        var last = Math.Min(_visible.Count - 1, (ScrollY + view.Height) / h);
        for (var pos = first; pos <= last; pos++) PaintNode(g, _visible[pos], RowRect(pos));
    }

    private void PaintNode(Graphics g, EnterpriseTreeNode node, Rectangle row)
    {
        var selected = ReferenceEquals(node, _selected);
        var hovered = ReferenceEquals(node, _hot) && Enabled;
        var cell = Rectangle.Inflate(row, -S(3), -S(1));

        if (ShowGuides && node.Level > 0)
        {
            using var pen = new Pen(Chrome.Blend(Colors.Surface, Colors.Border, 0.7f));
            for (var level = 0; level < node.Level; level++)
            {
                var x = row.X + S(6) + level * S(_indent) + S(9);
                g.DrawLine(pen, x, row.Y, x, row.Bottom);
            }
        }

        if (selected || hovered) Chrome.FillRounded(g, cell, Chrome.ItemBack(Colors, selected, hovered), S(3));
        if (selected) Chrome.DrawSelectionBar(g, cell, Colors, S(3));
        if (selected && Focused)
        {
            using var path = Chrome.RoundedRect(Rectangle.Inflate(cell, -1, -1), S(3));
            using var pen = new Pen(Colors.FocusRing, 1f) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot };
            g.DrawPath(pen, path);
        }

        if (node.HasChildren)
        {
            var color = selected ? Colors.SelectionText : Colors.Muted;
            Chrome.DrawChevron(g, ExpanderRect(node, row), node.Expanded ? ChevronDirection.Down : ChevronDirection.Right, color);
        }

        if (_checkBoxes) Chrome.DrawCheckBox(g, Colors, CheckRect(node, row), node.CheckState, Enabled && node.Enabled, hovered);

        var textX = TextX(node, row);
        var bounds = new Rectangle(textX, row.Y, Math.Max(0, row.Right - textX - S(6)), row.Height);
        var fore = !Enabled || !node.Enabled ? Colors.Disabled : node.ForeColor ?? (selected ? Colors.SelectionText : Colors.Text);
        Chrome.DrawText(g, node.Text, node.Bold ? BoldFont : Font, bounds, fore);
    }

    // ---- mouse and keyboard ---------------------------------------------------------------

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var pos = PositionAt(e.Location);
        var node = pos < 0 ? null : _visible[pos];
        if (!ReferenceEquals(node, _hot))
        {
            _hot = node;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hot is not null)
        {
            _hot = null;
            Invalidate();
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        var pos = PositionAt(e.Location);
        if (pos < 0 || !Enabled) return;
        var node = _visible[pos];
        var row = RowRect(pos);
        if (e.Button == MouseButtons.Left && node.HasChildren && ExpanderRect(node, row).Contains(e.Location))
        {
            SetExpanded(node, !node.Expanded);
            return;
        }

        if (e.Button == MouseButtons.Left && _checkBoxes && node.Enabled && Rectangle.Inflate(CheckRect(node, row), S(3), S(3)).Contains(e.Location))
        {
            node.CheckState = node.CheckState == CheckState.Checked ? CheckState.Unchecked : CheckState.Checked;
            SelectedNode = node;
            return;
        }

        SelectedNode = node;
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        var node = NodeAt(e.Location);
        if (node is null || e.Button != MouseButtons.Left) return;
        if (node.HasChildren) SetExpanded(node, !node.Expanded);
        NodeActivate?.Invoke(this, new EnterpriseTreeNodeEventArgs(node));
    }

    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.PageUp or Keys.PageDown or Keys.Home or Keys.End or Keys.Enter or Keys.Space
        || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        Rebuild();
        if (_visible.Count == 0) return;
        var pos = _selected is null ? -1 : _visible.IndexOf(_selected);
        var page = Math.Max(1, ViewPort.Height / S(_itemHeight) - 1);

        switch (e.KeyCode)
        {
            case Keys.Down: SelectedNode = _visible[Math.Min(_visible.Count - 1, pos + 1)]; break;
            case Keys.Up: SelectedNode = _visible[Math.Max(0, pos < 0 ? 0 : pos - 1)]; break;
            case Keys.PageDown: SelectedNode = _visible[Math.Min(_visible.Count - 1, Math.Max(0, pos) + page)]; break;
            case Keys.PageUp: SelectedNode = _visible[Math.Max(0, Math.Max(0, pos) - page)]; break;
            case Keys.Home: SelectedNode = _visible[0]; break;
            case Keys.End: SelectedNode = _visible[^1]; break;
            case Keys.Right when _selected is { } right:
                if (right.HasChildren && !right.Expanded) SetExpanded(right, true);
                else if (right.Expanded && right.Nodes.Count > 0) SelectedNode = right.Nodes[0];
                break;
            case Keys.Left when _selected is { } left:
                if (left.Expanded) SetExpanded(left, false);
                else if (left.Parent is not null) SelectedNode = left.Parent;
                break;
            case Keys.Add when _selected is { } plus:
                SetExpanded(plus, true);
                break;
            case Keys.Subtract when _selected is { } minus:
                SetExpanded(minus, false);
                break;
            case Keys.Multiply when _selected is { } star:
                BeginUpdate();
                SetExpanded(star, true);
                foreach (var d in star.Descendants().Where(n => n.HasChildren)) SetExpanded(d, true);
                EndUpdate();
                break;
            case Keys.Space when _selected is { } space && _checkBoxes && space.Enabled:
                space.CheckState = space.CheckState == CheckState.Checked ? CheckState.Unchecked : CheckState.Checked;
                break;
            case Keys.Enter when _selected is { } enter:
                NodeActivate?.Invoke(this, new EnterpriseTreeNodeEventArgs(enter));
                break;
            default:
                return;
        }

        e.Handled = true;
    }
}
