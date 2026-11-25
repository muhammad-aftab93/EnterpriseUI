using System.ComponentModel;

namespace EnterpriseUI.WinForms;

/// <summary>Which parts of the content a button shows.</summary>
public enum ButtonDisplayStyle
{
    /// <summary>Image or glyph and text.</summary>
    ImageAndText,

    /// <summary>Text only; the image is hidden.</summary>
    Text,

    /// <summary>Image or glyph only; the text is hidden but still used as the accessible name.</summary>
    Image,

    /// <summary>Nothing is drawn on the face.</summary>
    None
}

/// <summary>When a button raises its click.</summary>
public enum ButtonClickMode
{
    /// <summary>When the left mouse button is released over the button (the standard behavior).</summary>
    Release,

    /// <summary>As soon as the left mouse button is pressed.</summary>
    Press,

    /// <summary>When the pointer enters the button.</summary>
    Hover
}

/// <summary>The interaction state a button is painted in.</summary>
public enum ButtonVisualState
{
    Normal,
    Hot,
    Pressed,
    Disabled
}

/// <summary>Data of <see cref="EnterpriseButtonBase.FacePainting"/> and <see cref="EnterpriseButtonBase.ContentPainting"/>.</summary>
public sealed class EnterpriseButtonPaintEventArgs : EventArgs
{
    public EnterpriseButtonPaintEventArgs(Graphics graphics, Rectangle bounds, ButtonVisualState state, Color fillColor, Color foreColor)
    {
        Graphics = graphics;
        Bounds = bounds;
        State = state;
        FillColor = fillColor;
        ForeColor = foreColor;
    }

    /// <summary>The surface to draw on; anti-aliasing is already enabled.</summary>
    public Graphics Graphics { get; }

    /// <summary>The face rectangle for <c>FacePainting</c>, or the content rectangle for <c>ContentPainting</c>.</summary>
    public Rectangle Bounds { get; }

    public ButtonVisualState State { get; }

    /// <summary>The fill the default painting would use for the variant and current theme.</summary>
    public Color FillColor { get; }

    /// <summary>The text color the default painting would use.</summary>
    public Color ForeColor { get; }

    /// <summary>Set to true to skip the default painting of this part.</summary>
    public bool Handled { get; set; }
}

/// <summary>Data of <see cref="EnterpriseButtonBase.ToolTipTextNeeded"/>; the text set here wins over <see cref="EnterpriseButtonBase.ToolTipText"/>.</summary>
public sealed class ButtonToolTipTextNeededEventArgs : EventArgs
{
    public string? ToolTipText { get; set; }
}

/// <summary>Data of <see cref="EnterpriseToggleButtonControl.CheckStateChanging"/>.</summary>
public sealed class ToggleStateChangingEventArgs : CancelEventArgs
{
    public ToggleStateChangingEventArgs(CheckState oldState, CheckState newState)
    {
        OldState = oldState;
        NewState = newState;
    }

    public CheckState OldState { get; }
    public CheckState NewState { get; }
}

/// <summary>Pure geometry of the image and text of a button, shared by painting and size calculation.</summary>
internal static class ButtonLayoutEngine
{
    internal readonly struct Result
    {
        public Result(Rectangle icon, Rectangle text, Size content)
        {
            Icon = icon;
            Text = text;
            Content = content;
        }

        public Rectangle Icon { get; }
        public Rectangle Text { get; }

        /// <summary>Size of the combined image and text block.</summary>
        public Size Content { get; }
    }

    /// <param name="area">Where the content may be drawn.</param>
    /// <param name="icon">Size of the image or glyph; empty when there is none.</param>
    /// <param name="measureText">Measures the text for a maximum width; null when there is no text.</param>
    /// <param name="gap">Space between image and text.</param>
    /// <param name="relation">How image and text are arranged.</param>
    /// <param name="textAlignment">Where the image and text block (or just the text for <see cref="ButtonContentLayout.Overlay"/>) sits in the area.</param>
    /// <param name="imageAlignment">Where the image sits in the area for <see cref="ButtonContentLayout.Overlay"/>.</param>
    /// <param name="rightToLeft">Mirrors the result horizontally.</param>
    public static Result Arrange(Rectangle area, Size icon, Func<int, Size>? measureText, int gap, ButtonContentLayout relation, ContentAlignment textAlignment, ContentAlignment imageAlignment, bool rightToLeft)
    {
        var hasIcon = icon.Width > 0 && icon.Height > 0;
        var hasText = measureText is not null;
        if (!hasIcon && !hasText) return default;
        if (!hasIcon) icon = Size.Empty;

        var overlay = relation == ButtonContentLayout.Overlay;
        var vertical = relation is ButtonContentLayout.ImageAboveText or ButtonContentLayout.TextAboveImage;
        var spacing = hasIcon && hasText && !overlay ? gap : 0;
        var text = Size.Empty;
        if (hasText)
        {
            var available = Math.Max(0, overlay || vertical ? area.Width : area.Width - icon.Width - spacing);
            text = measureText!(available);
            text.Width = Math.Min(text.Width, available);
        }

        Rectangle iconRect, textRect;
        if (overlay)
        {
            iconRect = Align(area, icon, imageAlignment);
            textRect = Align(area, text, textAlignment);
        }
        else
        {
            var iconFirst = relation is ButtonContentLayout.ImageBeforeText or ButtonContentLayout.ImageAboveText;
            var first = iconFirst ? icon : text;
            var second = iconFirst ? text : icon;
            var group = vertical
                ? new Size(Math.Max(first.Width, second.Width), first.Height + spacing + second.Height)
                : new Size(first.Width + spacing + second.Width, Math.Max(first.Height, second.Height));
            var block = Align(area, group, textAlignment);
            Rectangle firstRect, secondRect;
            if (vertical)
            {
                firstRect = new Rectangle(block.X + (block.Width - first.Width) / 2, block.Y, first.Width, first.Height);
                secondRect = new Rectangle(block.X + (block.Width - second.Width) / 2, block.Y + first.Height + spacing, second.Width, second.Height);
            }
            else
            {
                firstRect = new Rectangle(block.X, block.Y + (block.Height - first.Height) / 2, first.Width, first.Height);
                secondRect = new Rectangle(block.X + first.Width + spacing, block.Y + (block.Height - second.Height) / 2, second.Width, second.Height);
            }

            iconRect = iconFirst ? firstRect : secondRect;
            textRect = iconFirst ? secondRect : firstRect;
        }

        if (!hasIcon) iconRect = Rectangle.Empty;
        if (!hasText) textRect = Rectangle.Empty;
        var content = hasIcon && hasText ? Rectangle.Union(iconRect, textRect).Size : hasIcon ? iconRect.Size : textRect.Size;
        if (rightToLeft)
        {
            iconRect = Mirror(iconRect, area);
            textRect = Mirror(textRect, area);
        }

        return new Result(iconRect, textRect, content);
    }

    /// <summary>The horizontal part of an alignment as a top-aligned one, for drawing the lines of a text block.</summary>
    public static ContentAlignment HorizontalOnly(ContentAlignment alignment, bool rightToLeft)
    {
        var left = alignment is ContentAlignment.TopLeft or ContentAlignment.MiddleLeft or ContentAlignment.BottomLeft;
        var right = alignment is ContentAlignment.TopRight or ContentAlignment.MiddleRight or ContentAlignment.BottomRight;
        if (rightToLeft && (left || right))
        {
            (left, right) = (right, left);
        }

        return left ? ContentAlignment.TopLeft : right ? ContentAlignment.TopRight : ContentAlignment.TopCenter;
    }

    internal static Rectangle Align(Rectangle area, Size size, ContentAlignment alignment)
    {
        var horizontal = alignment switch
        {
            ContentAlignment.TopLeft or ContentAlignment.MiddleLeft or ContentAlignment.BottomLeft => 0,
            ContentAlignment.TopRight or ContentAlignment.MiddleRight or ContentAlignment.BottomRight => 2,
            _ => 1
        };
        var vertical = alignment switch
        {
            ContentAlignment.TopLeft or ContentAlignment.TopCenter or ContentAlignment.TopRight => 0,
            ContentAlignment.BottomLeft or ContentAlignment.BottomCenter or ContentAlignment.BottomRight => 2,
            _ => 1
        };
        var freeX = Math.Max(0, area.Width - size.Width);
        var freeY = Math.Max(0, area.Height - size.Height);
        return new Rectangle(area.X + (horizontal == 0 ? 0 : horizontal == 1 ? freeX / 2 : freeX), area.Y + (vertical == 0 ? 0 : vertical == 1 ? freeY / 2 : freeY), size.Width, size.Height);
    }

    private static Rectangle Mirror(Rectangle rect, Rectangle area) => new(area.Left + area.Right - rect.Right, rect.Y, rect.Width, rect.Height);
}

/// <summary>Accessible object of the button family: name from the text without its access key, the access key as keyboard shortcut, badge as value, and a working default action.</summary>
internal sealed class ButtonAccessibleObject : Control.ControlAccessibleObject
{
    private readonly EnterpriseButtonBase _button;

    public ButtonAccessibleObject(EnterpriseButtonBase owner) : base(owner) => _button = owner;

    public override string? Name
    {
        get => _button.AccessibleName ?? _button.AccessibleLabel;
        set => base.Name = value;
    }

    public override string? Value
    {
        get => _button.AccessibleBadge;
        set { }
    }

    public override string? DefaultAction => _button.AccessibleDefaultAction;

    public override string? KeyboardShortcut => _button.AccessibleShortcut;

    public override AccessibleStates State => base.State | _button.AccessibleStateFlags();

    public override void DoDefaultAction()
    {
        if (!_button.IsHandleCreated) return;
        if (_button.InvokeRequired) _button.BeginInvoke(new Action(_button.PerformClick));
        else _button.PerformClick();
    }
}
