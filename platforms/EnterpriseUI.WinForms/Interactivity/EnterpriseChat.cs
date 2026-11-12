using System.ComponentModel;
using System.Drawing.Drawing2D;
using EnterpriseUI.Editors.Interactivity;

namespace EnterpriseUI.WinForms;

public sealed class ChatSubmittingEventArgs : CancelEventArgs
{
    public ChatSubmittingEventArgs(string text, IReadOnlyList<ChatAttachment> attachments)
    {
        Text = text;
        Attachments = attachments;
    }

    public string Text { get; }
    public IReadOnlyList<ChatAttachment> Attachments { get; }
}

/// <summary>Conversation view: grouped message bubbles with avatars, timestamps and delivery state, attachments, quick-reply chips, a typing indicator and a composer.</summary>
public class EnterpriseChatControl : EnterpriseControlBase
{
    private enum HitKind { Chip, Attach, Send, RemoveAttachment, File, Retry, Link, ScrollDown }

    private sealed record Hit(Rectangle Bounds, HitKind Kind, object? Payload = null, int Index = -1);

    private sealed class Box : IDisposable
    {
        public ChatMessage Message = null!;
        public ChatMessageLayoutInfo Info;
        public int Top, Height;
        public bool Local;
        public string? DateText;
        public Rectangle Bubble, Avatar, Name, Footer;
        public Point TextOrigin;
        public MarkupLayout? Text;
        public List<Rectangle> Files = new();

        public void Dispose() => Text?.Dispose();
    }

    private static readonly Color[] AvatarPalette =
    {
        Color.FromArgb(0, 120, 212), Color.FromArgb(16, 137, 62), Color.FromArgb(202, 80, 16), Color.FromArgb(136, 23, 152),
        Color.FromArgb(0, 130, 135), Color.FromArgb(194, 57, 179), Color.FromArgb(180, 120, 0), Color.FromArgb(92, 92, 190)
    };

    private readonly EnterpriseScrollBar _scroll = new(Orientation.Vertical) { Visible = false };
    private readonly TextBox _input = new() { Multiline = true, BorderStyle = BorderStyle.None, AcceptsReturn = false, ScrollBars = ScrollBars.None, WordWrap = true };
    private readonly Label _placeholder = new() { AutoSize = false, Cursor = Cursors.IBeam };
    private readonly List<Box> _boxes = new();
    private readonly List<Hit> _hits = new();
    private readonly List<ChatAttachment> _pending = new();
    private readonly FrameClock _typingClock = new() { Interval = 90 };
    private ChatConversation _conversation;
    private ControlColors _localColors;
    private int _layoutWidth = -1;
    private bool _stickToBottom = true;
    private bool _updatingScroll;
    private bool _unseen;
    private bool _quickRepliesVisible = true;
    private bool _showAvatars = true;
    private bool _showNames = true;
    private bool _showTimestamps = true;
    private bool _allowAttachments = true;
    private bool _readOnly;
    private bool _sendOnEnter = true;
    private LabelMarkupMode _markup = LabelMarkupMode.Markdown;
    private int _composerHeight;

    public EnterpriseChatControl()
    {
        TabStop = false;
        AccessibleRole = AccessibleRole.Grouping;
        _localColors = Colors;
        _conversation = new ChatConversation(new ChatAuthor("me", "You", true));
        Attach(_conversation);
        Size = new Size(420, 520);

        _input.MaxLength = 4000;
        _input.AccessibleName = "Message";
        _input.TextChanged += (_, _) => OnInputChanged();
        _input.KeyDown += OnInputKeyDown;
        _input.GotFocus += (_, _) => Invalidate();
        _input.LostFocus += (_, _) => Invalidate();
        _placeholder.Text = "Type a message";
        _placeholder.Click += (_, _) => _input.Focus();
        _scroll.ValueChanged += (_, _) =>
        {
            if (_updatingScroll) return;
            _stickToBottom = _scroll.Value >= _scroll.MaxValue - S(24);
            if (_stickToBottom) _unseen = false;
            Invalidate();
        };
        _typingClock.Frame += (_, _) => Invalidate();
        Controls.Add(_scroll);
        Controls.Add(_input);
        Controls.Add(_placeholder);
        OnThemeApplied();
        LayoutChildren();
    }

    [Browsable(false)]
    public ChatConversation Conversation
    {
        get => _conversation;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            Detach(_conversation);
            _conversation = value;
            Attach(value);
            ResetLayout();
        }
    }

    [DefaultValue("Type a message")]
    public string PlaceholderText { get => _placeholder.Text; set { _placeholder.Text = value; Invalidate(); } }

    [DefaultValue(true)]
    public bool ShowAvatars { get => _showAvatars; set { _showAvatars = value; ResetLayout(); } }

    [DefaultValue(true)]
    public bool ShowAuthorNames { get => _showNames; set { _showNames = value; ResetLayout(); } }

    [DefaultValue(true)]
    public bool ShowTimestamps { get => _showTimestamps; set { _showTimestamps = value; ResetLayout(); } }

    [DefaultValue(true)]
    public bool AllowAttachments { get => _allowAttachments; set { _allowAttachments = value; LayoutChildren(); Invalidate(); } }

    [DefaultValue(false)]
    public bool ReadOnly
    {
        get => _readOnly;
        set { _readOnly = value; _input.ReadOnly = value; _input.Enabled = !value; LayoutChildren(); Invalidate(); }
    }

    /// <summary>Enter sends and Shift+Enter inserts a new line; when false Ctrl+Enter sends.</summary>
    [DefaultValue(true)]
    public bool SendOnEnter { get => _sendOnEnter; set => _sendOnEnter = value; }

    [DefaultValue(LabelMarkupMode.Markdown)]
    public LabelMarkupMode MessageMarkup { get => _markup; set { _markup = value; ResetLayout(); } }

    [DefaultValue(4000)]
    public int MaxInputLength { get => _input.MaxLength; set => _input.MaxLength = Math.Max(1, value); }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string InputText { get => _input.Text; set => _input.Text = value; }

    [Browsable(false)]
    public IReadOnlyList<ChatAttachment> PendingAttachments => _pending;

    public event EventHandler<ChatSubmittingEventArgs>? MessageSubmitting;
    public event EventHandler<ChatMessageEventArgs>? MessageSubmitted;
    public event EventHandler<string>? QuickReplyClicked;
    public event EventHandler<ChatAttachment>? AttachmentClicked;
    public event EventHandler<ChatMessageEventArgs>? RetryRequested;
    public event EventHandler<LinkClickedEventArgs>? LinkClicked;

    private void Attach(ChatConversation c)
    {
        c.MessageAdded += OnMessageAdded;
        c.MessageChanged += OnMessageChanged;
        c.TypingChanged += OnTypingChanged;
        c.Cleared += OnCleared;
    }

    private void Detach(ChatConversation c)
    {
        c.MessageAdded -= OnMessageAdded;
        c.MessageChanged -= OnMessageChanged;
        c.TypingChanged -= OnTypingChanged;
        c.Cleared -= OnCleared;
    }

    private void OnMessageAdded(object? sender, ChatMessageEventArgs e)
    {
        if (InvokeRequired) { BeginInvoke(new Action(() => OnMessageAdded(sender, e))); return; }
        if (_layoutWidth > 0 && _boxes.Count + 1 == _conversation.Messages.Count) RebuildFrom(Math.Max(0, _boxes.Count - 1));
        else if (_layoutWidth > 0) ResetLayout();
        else Invalidate();
        _quickRepliesVisible = true;
        AfterContentChanged(e.Message.Author.IsLocal);
        AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
    }

    private void OnMessageChanged(object? sender, ChatMessageEventArgs e)
    {
        if (InvokeRequired) { BeginInvoke(new Action(() => OnMessageChanged(sender, e))); return; }
        var index = _boxes.FindIndex(b => b.Message == e.Message);
        if (index >= 0) RebuildFrom(index);
        Invalidate();
    }

    private void OnTypingChanged(object? sender, EventArgs e)
    {
        if (InvokeRequired) { BeginInvoke(new Action(() => OnTypingChanged(sender, e))); return; }
        _typingClock.Enabled = IsHandleCreated && _conversation.TypingAuthors.Count > 0;
        AfterContentChanged(false);
    }

    private void OnCleared(object? sender, EventArgs e)
    {
        if (InvokeRequired) { BeginInvoke(new Action(() => OnCleared(sender, e))); return; }
        ResetLayout();
    }

    private void AfterContentChanged(bool force)
    {
        UpdateScrollBar();
        if (_stickToBottom || force) ScrollToBottom();
        else
        {
            _unseen = true;
            Invalidate();
        }
    }

    public void ScrollToBottom()
    {
        _stickToBottom = true;
        _unseen = false;
        UpdateScrollBar();
        if (_scroll.Visible) _scroll.Value = _scroll.MaxValue;
        Invalidate();
    }

    /// <summary>Sends text as the local author as if the user had typed it.</summary>
    public bool SendText(string text, IEnumerable<ChatAttachment>? attachments = null)
    {
        var files = attachments?.ToList() ?? new List<ChatAttachment>();
        text = text.Trim();
        if (text.Length == 0 && files.Count == 0) return false;
        var args = new ChatSubmittingEventArgs(text, files);
        MessageSubmitting?.Invoke(this, args);
        if (args.Cancel) return false;
        var message = _conversation.Send(text, files);
        MessageSubmitted?.Invoke(this, new ChatMessageEventArgs(message));
        return true;
    }

    public void AddPendingAttachment(string path)
    {
        if (!File.Exists(path)) return;
        var info = new FileInfo(path);
        _pending.Add(new ChatAttachment { FileName = info.Name, Path = path, SizeBytes = info.Length });
        LayoutChildren();
        Invalidate();
    }

    private void Submit()
    {
        if (_readOnly) return;
        if (!SendText(_input.Text, _pending.ToArray())) return;
        _input.Clear();
        _pending.Clear();
        LayoutChildren();
        _input.Focus();
    }

    // ---- composer ----

    private void OnInputChanged()
    {
        _placeholder.Visible = _input.Text.Length == 0;
        LayoutChildren();
        Invalidate();
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            var wantsSend = _sendOnEnter ? !e.Shift : e.Control;
            e.SuppressKeyPress = true;
            if (wantsSend) Submit();
            else _input.SelectedText = Environment.NewLine;
        }
        else if (e.KeyCode is Keys.PageUp or Keys.PageDown && _scroll.Visible)
        {
            _scroll.Value += (e.KeyCode == Keys.PageUp ? -1 : 1) * _scroll.LargeChange;
            e.Handled = true;
        }
        else if (e.Control && e.KeyCode is Keys.End) { ScrollToBottom(); e.Handled = true; }
        else if (e.KeyCode == Keys.Escape && _pending.Count > 0) { _pending.Clear(); LayoutChildren(); Invalidate(); e.Handled = true; }
    }

    private int InputLineHeight => _input.Font.Height;

    private int InputHeight
    {
        get
        {
            var width = Math.Max(10, Width - S(2 + 44 + 52));
            var size = TextRenderer.MeasureText(_input.Text.Length == 0 ? " " : _input.Text + "x", _input.Font, new Size(width, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.TextBoxControl);
            var lines = Math.Clamp((int)Math.Ceiling(size.Height / (double)InputLineHeight), 1, 5);
            return lines * InputLineHeight;
        }
    }

    private IReadOnlyList<string> VisibleChips => _quickRepliesVisible && _conversation.Messages.Count > 0 && !_readOnly ? _conversation.Messages[^1].QuickReplies : Array.Empty<string>();

    private List<(Rectangle Rect, int Row)> ChipRects(out int height)
    {
        var result = new List<(Rectangle, int)>();
        var x = S(12);
        var row = 0;
        var chipHeight = S(28);
        var limit = Width - S(12);
        foreach (var text in VisibleChips)
        {
            var w = Chrome.MeasureText(text, Font) + S(24);
            if (x + w > limit && x > S(12)) { x = S(12); row++; }
            result.Add((new Rectangle(x, row * (chipHeight + S(6)), w, chipHeight), row));
            x += w + S(8);
        }

        height = VisibleChips.Count == 0 ? 0 : (row + 1) * (chipHeight + S(6));
        return result;
    }

    private void LayoutChildren()
    {
        if (_input == null || _scroll == null) return;
        ChipRects(out var chipsHeight);
        var attachRow = _pending.Count > 0 ? S(34) : 0;
        var inputH = InputHeight;
        var frameH = inputH + S(16);
        _composerHeight = S(10) + chipsHeight + attachRow + frameH + S(8);
        var frameTop = Height - 1 - _composerHeight + S(10) + chipsHeight + attachRow;
        var left = S(12) + (_allowAttachments ? S(40) : S(8));
        var right = S(12) + S(44);
        _input.SetBounds(left, frameTop + S(8), Math.Max(10, Width - left - right), inputH);
        _placeholder.SetBounds(left, frameTop + S(8), Math.Max(10, Width - left - right), inputH);
        _placeholder.Visible = _input.Text.Length == 0 && !_readOnly;
        UpdateScrollBar();
    }

    private Rectangle TranscriptBounds => new(1, 1, Math.Max(0, Width - 2), Math.Max(0, Height - 2 - _composerHeight));

    protected override void OnThemeApplied()
    {
        base.OnThemeApplied();
        _localColors = ControlColors.FromTheme(EnterpriseUI.Theming.ThemeManager.Instance.CurrentTheme);
        _localColors.Primary = _localColors.PrimaryText;
        _input.BackColor = Colors.Surface;
        _input.ForeColor = Colors.Text;
        _input.Font = Font;
        _placeholder.BackColor = Colors.Surface;
        _placeholder.ForeColor = Colors.Muted;
        _placeholder.Font = Font;
        _scroll.ApplyColors(Colors);
        ResetLayout();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        if (_input == null) return;
        _input.Font = Font;
        _placeholder.Font = Font;
        ResetLayout();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (_scroll == null) return;
        LayoutChildren();
        ResetLayout();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        _typingClock.Enabled = _conversation.TypingAuthors.Count > 0;
        LayoutChildren();
        ScrollToBottom();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        _typingClock.Enabled = false;
        base.OnHandleDestroyed(e);
    }

    // ---- layout ----

    private int BubbleMax => Math.Max(S(160), (int)((TranscriptBounds.Width - (_scroll.Visible ? S(12) : 0)) * 0.74) - (_showAvatars ? S(40) : 0));

    private void ResetLayout()
    {
        foreach (var b in _boxes) b.Dispose();
        _boxes.Clear();
        _layoutWidth = -1;
        if (Width > 0 && IsHandleCreated) RebuildFrom(0);
        UpdateScrollBar();
        if (_stickToBottom && _scroll.Visible) _scroll.Value = _scroll.MaxValue;
        Invalidate();
    }

    private void EnsureLayout()
    {
        if (_layoutWidth == TranscriptBounds.Width && _boxes.Count == _conversation.Messages.Count) return;
        foreach (var b in _boxes) b.Dispose();
        _boxes.Clear();
        RebuildFrom(0);
    }

    private void RebuildFrom(int start)
    {
        var width = TranscriptBounds.Width;
        _layoutWidth = width;
        var messages = _conversation.Messages;
        while (_boxes.Count > start)
        {
            _boxes[^1].Dispose();
            _boxes.RemoveAt(_boxes.Count - 1);
        }

        var top = start > 0 && _boxes.Count > 0 ? _boxes[^1].Top + _boxes[^1].Height : S(10);
        for (var i = _boxes.Count; i < messages.Count; i++)
        {
            var box = LayoutBox(messages, i, top);
            _boxes.Add(box);
            top += box.Height;
        }

        UpdateScrollBar();
    }

    private Box LayoutBox(IReadOnlyList<ChatMessage> messages, int index, int top)
    {
        var message = messages[index];
        var info = ChatGrouping.Describe(messages, index);
        var box = new Box { Message = message, Info = info, Top = top, Local = message.Author.IsLocal };
        var c = Colors;
        var y = 0;
        var width = TranscriptBounds.Width - (_scroll.Visible ? S(12) : 0);
        var pad = S(12);

        if (info.ShowDateSeparator)
        {
            box.DateText = ChatGrouping.DateLabel(message.Timestamp, DateTimeOffset.Now);
            y += S(34);
        }

        if (message.IsSystem)
        {
            box.Text = MarkupLayout.Create(LabelMarkup.Parse(message.Text, LabelMarkupMode.Plain), Font, c, Math.Max(S(80), width - pad * 4), true, HorizontalAlignment.Center, 0, true, c.Muted);
            box.TextOrigin = new Point((width - box.Text.Size.Width) / 2, top + y + S(2));
            box.Height = y + box.Text.Size.Height + S(10);
            return box;
        }

        y += info.StartsGroup ? S(8) : S(2);
        var avatarCol = _showAvatars && !box.Local ? S(32) + S(8) : 0;
        var showName = _showNames && !box.Local && info.StartsGroup;
        if (showName)
        {
            box.Name = new Rectangle(pad + avatarCol + S(4), top + y, Math.Max(10, BubbleMax), S(16));
            y += S(18);
        }

        var padX = S(12);
        var padY = S(8);
        var available = BubbleMax - padX * 2;
        var colors = box.Local ? _localColors : c;
        var textColor = box.Local ? c.PrimaryText : c.Text;
        MarkupLayout? text = null;
        if (message.Text.Length > 0) text = MarkupLayout.Create(LabelMarkup.Parse(message.Text, _markup), Font, colors, available, true, HorizontalAlignment.Left, 0, true, textColor);
        box.Text = text;

        var contentW = Math.Max(text?.Size.Width ?? 0, message.Attachments.Count > 0 ? Math.Min(available, S(240)) : 0);
        var bubbleW = Math.Max(S(56), contentW + padX * 2);
        var textH = text?.Size.Height ?? 0;
        var filesH = message.Attachments.Count * (S(40) + S(4));
        var bubbleH = padY * 2 + textH + (textH > 0 && filesH > 0 ? S(6) : 0) + filesH - (filesH > 0 ? S(4) : 0);
        bubbleH = Math.Max(bubbleH, S(34));
        var bubbleX = box.Local ? width - pad - bubbleW : pad + avatarCol;
        box.Bubble = new Rectangle(bubbleX, top + y, bubbleW, bubbleH);
        box.TextOrigin = new Point(bubbleX + padX, top + y + padY);
        var fileY = top + y + padY + textH + (textH > 0 ? S(6) : 0);
        for (var f = 0; f < message.Attachments.Count; f++) box.Files.Add(new Rectangle(bubbleX + padX, fileY + f * (S(40) + S(4)), bubbleW - padX * 2, S(40)));
        if (_showAvatars && !box.Local && info.EndsGroup) box.Avatar = new Rectangle(pad, top + y + bubbleH - S(32), S(32), S(32));
        y += bubbleH;

        if (info.EndsGroup && (_showTimestamps || box.Local))
        {
            box.Footer = new Rectangle(box.Local ? bubbleX : bubbleX + S(4), top + y + S(2), Math.Max(bubbleW, S(120)), S(16));
            y += S(20);
        }

        box.Height = y;
        return box;
    }

    private int ContentHeight => (_boxes.Count == 0 ? S(10) : _boxes[^1].Top + _boxes[^1].Height) + (_conversation.TypingAuthors.Count > 0 ? S(48) : 0) + S(8);

    private void UpdateScrollBar()
    {
        if (_scroll == null) return;
        var view = TranscriptBounds;
        var content = ContentHeight;
        var needed = content > view.Height;
        _updatingScroll = true;
        try
        {
            _scroll.Visible = needed;
            _scroll.SetBounds(view.Right - S(12), view.Y, S(12), view.Height);
            if (needed)
            {
                _scroll.Minimum = 0;
                _scroll.Maximum = content - 1;
                _scroll.LargeChange = Math.Max(1, view.Height);
                _scroll.SmallChange = S(40);
                if (_stickToBottom) _scroll.Value = _scroll.MaxValue;
            }
            else _scroll.Value = 0;
        }
        finally
        {
            _updatingScroll = false;
        }
    }

    private int ScrollY => _scroll.Visible ? _scroll.Value : 0;

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (!_scroll.Visible) return;
        _scroll.Value -= e.Delta / 120 * S(48);
    }

    // ---- painting ----

    private static Color AvatarColor(ChatAuthor author)
    {
        if (author.AccentArgb is { } argb) return Color.FromArgb(argb);
        var hash = 17;
        foreach (var ch in author.Id) hash = hash * 31 + ch;
        return AvatarPalette[(hash & 0x7fffffff) % AvatarPalette.Length];
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Colors.Surface);
        _hits.Clear();
        EnsureLayout();
        var c = Colors;
        var view = TranscriptBounds;

        var state = g.Save();
        g.SetClip(view);
        g.TranslateTransform(0, -ScrollY);
        var from = ScrollY;
        var to = ScrollY + view.Height;
        var first = FindFirst(from);
        for (var i = first; i < _boxes.Count && _boxes[i].Top <= to; i++) PaintBox(g, c, _boxes[i], i);
        if (_conversation.TypingAuthors.Count > 0) PaintTyping(g, c);
        g.Restore(state);

        if (_boxes.Count == 0 && _conversation.TypingAuthors.Count == 0)
            Chrome.DrawText(g, "No messages yet", Font, view, c.Muted, ContentAlignment.MiddleCenter);

        if (!_stickToBottom && _scroll.Visible) PaintScrollDown(g, c, view);
        PaintComposer(g, c);
        Chrome.DrawRoundedBorder(g, new Rectangle(0, 0, Width, Height), c.Border, Chrome.CornerRadius);
    }

    private int FindFirst(int y)
    {
        var lo = 0;
        var hi = _boxes.Count - 1;
        while (lo < hi)
        {
            var mid = (lo + hi) / 2;
            if (_boxes[mid].Top + _boxes[mid].Height < y) lo = mid + 1; else hi = mid;
        }

        return Math.Max(0, lo);
    }

    private void Register(Rectangle contentRect, HitKind kind, object? payload = null, int index = -1) => _hits.Add(new Hit(new Rectangle(contentRect.X, contentRect.Y - ScrollY, contentRect.Width, contentRect.Height), kind, payload, index));

    private Rectangle V(Rectangle r) => new(r.X, r.Y - ScrollY, r.Width, r.Height);

    private Point V(Point p) => new(p.X, p.Y - ScrollY);

    private void PaintBox(Graphics g, ControlColors c, Box box, int index)
    {
        var width = TranscriptBounds.Width - (_scroll.Visible ? S(12) : 0);
        if (box.DateText != null)
        {
            var textW = Chrome.MeasureText(box.DateText, Font) + S(20);
            var pill = new Rectangle((width - textW) / 2, box.Top + S(6), textW, S(22));
            Chrome.FillRounded(g, pill, Chrome.Blend(c.Surface, c.Muted, 0.14), pill.Height / 2);
            Chrome.DrawText(g, box.DateText, Font, V(pill), c.Muted, ContentAlignment.MiddleCenter);
        }

        if (box.Message.IsSystem)
        {
            box.Text?.Draw(g, V(box.TextOrigin));
            return;
        }

        if (!box.Name.IsEmpty) Chrome.DrawText(g, box.Message.Author.Name, BoldFont, V(box.Name), c.Muted);
        if (!box.Avatar.IsEmpty)
        {
            using var brush = new SolidBrush(AvatarColor(box.Message.Author));
            g.FillEllipse(brush, box.Avatar);
            Chrome.DrawText(g, box.Message.Author.Initials, BoldFont, V(box.Avatar), Color.White, ContentAlignment.MiddleCenter);
        }

        var back = box.Local ? (box.Message.Status == ChatMessageStatus.Failed ? Chrome.Blend(c.Primary, c.Danger, 0.5) : c.Primary) : Chrome.Blend(c.Surface, c.Muted, c.HighContrast ? 0 : 0.16);
        var fore = box.Local ? c.PrimaryText : c.Text;
        Chrome.FillRounded(g, box.Bubble, back, S(12));
        if (c.HighContrast) Chrome.DrawRoundedBorder(g, box.Bubble, c.Text, S(12));
        box.Text?.Draw(g, V(box.TextOrigin));
        if (box.Text != null)
            foreach (var piece in box.Text.Pieces.Where(p => p.Href != null)) Register(new Rectangle(box.TextOrigin.X + piece.Bounds.X, box.TextOrigin.Y + piece.Bounds.Y, piece.Bounds.Width, piece.Bounds.Height), HitKind.Link, piece.Href);

        for (var f = 0; f < box.Files.Count; f++)
        {
            var r = box.Files[f];
            var attachment = box.Message.Attachments[f];
            Chrome.FillRounded(g, r, box.Local ? Chrome.Blend(c.Primary, Color.White, 0.18) : c.Surface, S(8));
            TextRenderer.DrawText(g, attachment.IsImage ? "🖼" : "📄", GlyphFonts.Get(Font.SizeInPoints + 3), V(new Rectangle(r.X + S(6), r.Y, S(28), r.Height)), fore, TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
            Chrome.DrawText(g, attachment.FileName, BoldFont, V(new Rectangle(r.X + S(38), r.Y + S(4), r.Width - S(44), S(16))), fore);
            Chrome.DrawText(g, attachment.SizeText, Font, V(new Rectangle(r.X + S(38), r.Y + S(21), r.Width - S(44), S(16))), box.Local ? Color.FromArgb(200, fore) : c.Muted);
            Register(r, HitKind.File, attachment);
        }

        if (box.Footer.IsEmpty) return;
        var stamp = box.Message.Timestamp.ToLocalTime().ToString("t");
        var status = box.Local ? StatusText(box.Message.Status) : string.Empty;
        var failed = box.Message.Status == ChatMessageStatus.Failed;
        var footerText = box.Local ? (status.Length > 0 ? $"{stamp}  {status}" : stamp) : stamp;
        var color = failed ? c.Danger : box.Message.Status == ChatMessageStatus.Read && box.Local ? c.Primary : c.Muted;
        var footer = box.Footer;
        if (box.Local) footer = new Rectangle(box.Bubble.Right - footer.Width, footer.Y, footer.Width, footer.Height);
        Chrome.DrawText(g, footerText + (failed ? "  Retry" : string.Empty), Font, V(footer), color, box.Local ? ContentAlignment.MiddleRight : ContentAlignment.MiddleLeft);
        if (failed) Register(footer, HitKind.Retry, box.Message);
    }

    private static string StatusText(ChatMessageStatus status) => status switch
    {
        ChatMessageStatus.Sending => "Sending…",
        ChatMessageStatus.Sent => "✓",
        ChatMessageStatus.Delivered => "✓✓",
        ChatMessageStatus.Read => "✓✓",
        _ => "Not sent"
    };

    private void PaintTyping(Graphics g, ControlColors c)
    {
        var top = (_boxes.Count == 0 ? S(10) : _boxes[^1].Top + _boxes[^1].Height) + S(6);
        var pad = S(12);
        var bubble = new Rectangle(pad + (_showAvatars ? S(40) : 0), top, S(56), S(30));
        Chrome.FillRounded(g, bubble, Chrome.Blend(c.Surface, c.Muted, 0.16), S(12));
        var t = _typingClock.Seconds;
        for (var i = 0; i < 3; i++)
        {
            var wave = Math.Max(0, Math.Sin(t * 5 - i * 0.9));
            var radius = S(3) + (int)(wave * S(1.5));
            using var brush = new SolidBrush(Chrome.Blend(c.Muted, c.Text, wave * 0.6));
            g.FillEllipse(brush, bubble.X + S(14) + i * S(13) - radius, bubble.Y + bubble.Height / 2 - radius - (int)(wave * S(3)), radius * 2, radius * 2);
        }

        var names = string.Join(", ", _conversation.TypingAuthors.Select(a => a.Name));
        Chrome.DrawText(g, names + (_conversation.TypingAuthors.Count == 1 ? " is typing" : " are typing"), Font, V(new Rectangle(bubble.Right + S(8), top, Math.Max(10, TranscriptBounds.Width - bubble.Right - S(20)), bubble.Height)), c.Muted);
    }

    private void PaintScrollDown(Graphics g, ControlColors c, Rectangle view)
    {
        var text = _unseen ? "New messages ↓" : "↓";
        var w = Chrome.MeasureText(text, BoldFont) + S(24);
        var r = new Rectangle(view.Right - w - S(24), view.Bottom - S(36), w, S(28));
        Chrome.FillRounded(g, r, c.Primary, r.Height / 2);
        Chrome.DrawText(g, text, BoldFont, r, c.PrimaryText, ContentAlignment.MiddleCenter);
        _hits.Add(new Hit(r, HitKind.ScrollDown));
    }

    private void PaintComposer(Graphics g, ControlColors c)
    {
        var top = Height - 1 - _composerHeight;
        using (var pen = new Pen(c.Border)) g.DrawLine(pen, 1, top, Width - 2, top);
        var y = top + S(10);

        var chips = ChipRects(out var chipsHeight);
        for (var i = 0; i < chips.Count; i++)
        {
            var r = new Rectangle(chips[i].Rect.X, y + chips[i].Rect.Y, chips[i].Rect.Width, chips[i].Rect.Height);
            Chrome.FillRounded(g, r, c.Surface, r.Height / 2);
            Chrome.DrawRoundedBorder(g, r, c.Primary, r.Height / 2);
            Chrome.DrawText(g, VisibleChips[i], Font, r, c.Primary, ContentAlignment.MiddleCenter);
            _hits.Add(new Hit(r, HitKind.Chip, VisibleChips[i], i));
        }

        y += chipsHeight;

        if (_pending.Count > 0)
        {
            var x = S(12);
            for (var i = 0; i < _pending.Count; i++)
            {
                var label = _pending[i].FileName + "  " + _pending[i].SizeText;
                var w = Math.Min(S(240), Chrome.MeasureText(label, Font) + S(52));
                var r = new Rectangle(x, y, w, S(28));
                Chrome.FillRounded(g, r, Chrome.Blend(c.Surface, c.Muted, 0.14), S(8));
                Chrome.DrawText(g, "📎 " + label, Font, new Rectangle(r.X + S(8), r.Y, r.Width - S(30), r.Height), c.Text);
                var close = new Rectangle(r.Right - S(24), r.Y + S(4), S(20), S(20));
                Chrome.DrawText(g, "✕", Font, close, c.Muted, ContentAlignment.MiddleCenter);
                _hits.Add(new Hit(close, HitKind.RemoveAttachment, null, i));
                x += w + S(8);
            }

            y += S(34);
        }

        var frame = new Rectangle(S(12), y, Width - S(24), InputHeight + S(16));
        Chrome.DrawFrame(g, frame, c, _input.Focused, false, S(12), !_readOnly);
        if (_allowAttachments)
        {
            var attach = new Rectangle(frame.X + S(4), frame.Bottom - S(36), S(32), S(32));
            TextRenderer.DrawText(g, "📎", GlyphFonts.Get(Font.SizeInPoints + 2), attach, _readOnly ? c.Disabled : c.Muted, TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);
            _hits.Add(new Hit(attach, HitKind.Attach));
        }

        var canSend = !_readOnly && (_input.Text.Trim().Length > 0 || _pending.Count > 0);
        var send = new Rectangle(frame.Right - S(38), frame.Bottom - S(36), S(32), S(32));
        using (var brush = new SolidBrush(canSend ? c.Primary : Chrome.Blend(c.Surface, c.Border, 0.6))) g.FillEllipse(brush, send);
        var arrow = canSend ? c.PrimaryText : c.Disabled;
        using (var pen = new Pen(arrow, S(2)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
        {
            var cx = send.X + send.Width / 2f;
            var cy = send.Y + send.Height / 2f;
            g.DrawLine(pen, cx - S(6), cy + S(1), cx + S(6), cy + S(1));
            g.DrawLines(pen, new[] { new PointF(cx + S(1), cy - S(5)), new PointF(cx + S(6), cy + S(1)), new PointF(cx + S(1), cy + S(7)) });
        }

        if (canSend) _hits.Add(new Hit(send, HitKind.Send));
    }

    // ---- mouse ----

    private Hit? HitAt(Point p) => _hits.LastOrDefault(h => h.Bounds.Contains(p) && (h.Kind is HitKind.Chip or HitKind.Attach or HitKind.Send or HitKind.RemoveAttachment || TranscriptBounds.Contains(p)));

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Cursor = HitAt(e.Location) != null ? Cursors.Hand : Cursors.Default;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || !Enabled) return;
        if (HitAt(e.Location) is not { } hit)
        {
            _input.Focus();
            return;
        }

        switch (hit.Kind)
        {
            case HitKind.Chip:
                var text = (string)hit.Payload!;
                QuickReplyClicked?.Invoke(this, text);
                _quickRepliesVisible = false;
                SendText(text);
                LayoutChildren();
                break;
            case HitKind.Attach: PickFiles(); break;
            case HitKind.Send: Submit(); break;
            case HitKind.RemoveAttachment:
                _pending.RemoveAt(hit.Index);
                LayoutChildren();
                Invalidate();
                break;
            case HitKind.File: AttachmentClicked?.Invoke(this, (ChatAttachment)hit.Payload!); break;
            case HitKind.Retry:
                RetryRequested?.Invoke(this, new ChatMessageEventArgs((ChatMessage)hit.Payload!));
                break;
            case HitKind.Link: LinkClicked?.Invoke(this, new LinkClickedEventArgs((string)hit.Payload!)); break;
            case HitKind.ScrollDown: ScrollToBottom(); break;
        }
    }

    private void PickFiles()
    {
        if (_readOnly) return;
        using var dialog = new OpenFileDialog { Multiselect = true, Title = "Attach files" };
        if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
        foreach (var path in dialog.FileNames) AddPendingAttachment(path);
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new ValueAccessibleObject(this, () =>
    {
        var last = _conversation.Messages.Count == 0 ? null : _conversation.Messages[^1];
        return last == null ? "No messages" : $"{_conversation.Messages.Count} messages. Latest from {last.Author.Name}: {last.Text}";
    });

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Detach(_conversation);
            _typingClock.Dispose();
            foreach (var b in _boxes) b.Dispose();
        }

        base.Dispose(disposing);
    }
}
