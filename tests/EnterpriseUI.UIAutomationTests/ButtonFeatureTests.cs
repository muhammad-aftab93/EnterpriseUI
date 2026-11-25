using System.ComponentModel;
using System.ComponentModel.Design.Serialization;
using System.Reflection;
using EnterpriseUI.Core.Mvvm;
using EnterpriseUI.Icons;
using EnterpriseUI.Tests.Support;
using EnterpriseUI.WinForms;
using EnterpriseUI.WinForms.Design;
using Xunit;
using static EnterpriseUI.Tests.Support.WinFormsTestSupport;

namespace EnterpriseUI.Tests.UI;

/// <summary>Vector images and the pure layout of the button content; no window needed.</summary>
public class ButtonContentTests
{
    private static int Painted(Bitmap bitmap)
    {
        var count = 0;
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).A > 0) count++;
            }
        }

        return count;
    }

    // ---- SVG path data ----

    [Fact]
    public void FilledPathPaintsTheRequestedColorAtTheRequestedSize()
    {
        var image = new EnterpriseSvgImage("M0 0H10V10H0Z", "0 0 10 10");
        using var bitmap = image.ToBitmap(20, Color.Red);
        Assert.Equal(Color.Red.ToArgb(), bitmap.GetPixel(10, 10).ToArgb());
        Assert.Equal(Color.Red.ToArgb(), bitmap.GetPixel(2, 2).ToArgb());
    }

    [Fact]
    public void RelativeCommandsAndArcsDrawACircle()
    {
        var image = new EnterpriseSvgImage("M12 2a10 10 0 1 0 0 20a10 10 0 1 0 0-20z");
        using var bitmap = image.ToBitmap(24, Color.Blue);
        Assert.Equal(Color.Blue.ToArgb(), bitmap.GetPixel(12, 12).ToArgb());
        Assert.Equal(Color.Blue.ToArgb(), bitmap.GetPixel(4, 12).ToArgb());
        Assert.Equal(Color.Blue.ToArgb(), bitmap.GetPixel(19, 12).ToArgb());
        Assert.Equal(0, bitmap.GetPixel(0, 0).A);
        Assert.Equal(0, bitmap.GetPixel(23, 23).A);
    }

    [Fact]
    public void ArcFlagsCanBeWrittenWithoutSeparators()
    {
        var image = new EnterpriseSvgImage("M2 12a10 10 0 01 20 0z");
        using var bitmap = image.ToBitmap(24, Color.Black);
        Assert.True(Painted(bitmap) > 20);
    }

    [Fact]
    public void CubicQuadraticAndSmoothCommandsAreSupported()
    {
        var image = new EnterpriseSvgImage("M2 12C2 4 10 4 12 12S22 20 22 12V22H2ZM2 2Q12 8 22 2T22 6Z");
        using var bitmap = image.ToBitmap(24, Color.Black);
        Assert.True(Painted(bitmap) > 50);
    }

    [Fact]
    public void ImplicitLineToAfterMoveToAndNumbersWithoutSpacesAreParsed()
    {
        var image = new EnterpriseSvgImage("M0,0 10,0 10,10 0,10z M2-2", "0 0 10 10");
        using var bitmap = image.ToBitmap(10, Color.Black);
        Assert.Equal(Color.Black.ToArgb(), bitmap.GetPixel(5, 5).ToArgb());
        var compact = new EnterpriseSvgImage("M0 0h10v10h-10zm.5.5h1v1z", "0 0 10 10");
        using var compactBitmap = compact.ToBitmap(20, Color.Black);
        Assert.True(Painted(compactBitmap) > 100);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("M0 0 L")]
    [InlineData("L1 2")]
    [InlineData("M0 0 X 1 2")]
    [InlineData("M0 0 a1 1 0 2 0 5 5")]
    public void MalformedPathDataIsRejectedWithAnArgumentException(string data) =>
        Assert.Throws<ArgumentException>(() => new EnterpriseSvgImage(data));

    [Theory]
    [InlineData("0 0 0 10")]
    [InlineData("0 0 10")]
    [InlineData("a b c d")]
    public void InvalidViewBoxIsRejected(string viewBox) => Assert.Throws<ArgumentException>(() => new EnterpriseSvgImage("M0 0h1v1z", viewBox));

    [Fact]
    public void EveryRegisteredIconRenders()
    {
        var icons = new[]
        {
            FluentIconPack.Add, FluentIconPack.Edit, FluentIconPack.Delete, FluentIconPack.Save, FluentIconPack.Search, FluentIconPack.Filter,
            FluentIconPack.Refresh, FluentIconPack.Grid, FluentIconPack.EnterpriseChart, FluentIconPack.Calendar, MaterialIconPack.Check,
            MaterialIconPack.Close, MaterialIconPack.Menu, MaterialIconPack.Settings, MaterialIconPack.Download, MaterialIconPack.Upload,
            MaterialIconPack.Print, MaterialIconPack.MoreVert
        };
        foreach (var icon in icons)
        {
            using var bitmap = EnterpriseSvgImage.FromIcon(icon).ToBitmap(48, Color.Black);
            Assert.True(Painted(bitmap) > 30, icon.Name);
        }
    }

    // ---- SVG markup ----

    [Fact]
    public void MarkupShapesAreFilledByDefault()
    {
        var image = EnterpriseSvgImage.FromMarkup("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 10 10\"><rect width=\"10\" height=\"10\"/></svg>");
        using var bitmap = image.ToBitmap(20, Color.Green);
        Assert.Equal(Color.Green.ToArgb(), bitmap.GetPixel(10, 10).ToArgb());
    }

    [Fact]
    public void StrokeOnlyShapesLeaveTheInsideEmpty()
    {
        var image = EnterpriseSvgImage.FromMarkup("<svg viewBox=\"0 0 10 10\"><rect x=\"1\" y=\"1\" width=\"8\" height=\"8\" fill=\"none\" stroke=\"black\" stroke-width=\"2\"/></svg>");
        using var bitmap = image.ToBitmap(20, Color.Black);
        Assert.Equal(0, bitmap.GetPixel(10, 10).A);
        Assert.True(bitmap.GetPixel(2, 10).A > 200);
    }

    [Fact]
    public void FillAndStrokeAreInheritedFromGroupsAndStyleAttributes()
    {
        var grouped = EnterpriseSvgImage.FromMarkup("<svg viewBox=\"0 0 10 10\"><g fill=\"none\" stroke=\"black\"><line x1=\"0\" y1=\"5\" x2=\"10\" y2=\"5\"/></g></svg>");
        using (var bitmap = grouped.ToBitmap(20, Color.Black))
        {
            Assert.True(bitmap.GetPixel(10, 10).A > 200);
            Assert.Equal(0, bitmap.GetPixel(10, 2).A);
        }

        var styled = EnterpriseSvgImage.FromMarkup("<svg viewBox=\"0 0 10 10\"><circle cx=\"5\" cy=\"5\" r=\"4\" style=\"fill:none;stroke:red;stroke-width:1\"/></svg>");
        using (var bitmap = styled.ToBitmap(20, Color.Black))
        {
            Assert.Equal(0, bitmap.GetPixel(10, 10).A);
            Assert.True(bitmap.GetPixel(2, 10).A > 100);
        }
    }

    [Fact]
    public void EvenOddFillRuleCutsOutTheInnerShape()
    {
        const string ring = "M0 0H10V10H0ZM3 3H7V7H3Z";
        using var winding = EnterpriseSvgImage.FromMarkup($"<svg viewBox=\"0 0 10 10\"><path d=\"{ring}\"/></svg>").ToBitmap(20, Color.Black);
        using var evenOdd = EnterpriseSvgImage.FromMarkup($"<svg viewBox=\"0 0 10 10\"><path fill-rule=\"evenodd\" d=\"{ring}\"/></svg>").ToBitmap(20, Color.Black);
        Assert.Equal(0, evenOdd.GetPixel(10, 10).A);
        Assert.True(evenOdd.GetPixel(2, 2).A > 200);
        Assert.True(winding.GetPixel(2, 2).A > 200);
    }

    [Fact]
    public void PolygonsEllipsesAndRoundedRectanglesAreDrawn()
    {
        var image = EnterpriseSvgImage.FromMarkup("<svg viewBox=\"0 0 30 10\"><polygon points=\"0,0 10,0 5,10\"/><ellipse cx=\"15\" cy=\"5\" rx=\"4\" ry=\"3\"/><rect x=\"22\" width=\"8\" height=\"10\" rx=\"2\"/></svg>");
        using var bitmap = image.ToBitmap(60, Color.Black);
        Assert.True(Painted(bitmap) > 300);
    }

    [Fact]
    public void ShapesInsideDefinitionsAreNotDrawn()
    {
        Assert.Throws<ArgumentException>(() => EnterpriseSvgImage.FromMarkup("<svg viewBox=\"0 0 10 10\"><defs><rect width=\"10\" height=\"10\"/></defs></svg>"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not xml")]
    [InlineData("<html/>")]
    [InlineData("<svg viewBox=\"0 0 10 10\"/>")]
    [InlineData("<svg viewBox=\"0 0 10 10\"><path d=\"M0 0 L\"/></svg>")]
    public void InvalidMarkupIsRejected(string markup) => Assert.Throws<ArgumentException>(() => EnterpriseSvgImage.FromMarkup(markup));

    [Fact]
    public void MarkupWithADoctypeIsRejectedSoEntitiesCannotBeExpanded()
    {
        const string xxe = "<!DOCTYPE svg [<!ENTITY a \"b\">]><svg viewBox=\"0 0 10 10\"><rect width=\"10\" height=\"10\"/></svg>";
        Assert.Throws<ArgumentException>(() => EnterpriseSvgImage.FromMarkup(xxe));
    }

    [Fact]
    public void OversizedMarkupIsRejected() =>
        Assert.Throws<ArgumentException>(() => EnterpriseSvgImage.FromMarkup("<svg viewBox=\"0 0 1 1\"><rect width=\"1\" height=\"1\"/>" + new string(' ', 300000) + "</svg>"));

    [Fact]
    public void ConverterSerializesPathAndMarkupImagesForTheDesigner()
    {
        var converter = TypeDescriptor.GetConverter(typeof(EnterpriseSvgImage));
        Assert.True(converter.CanConvertTo(typeof(InstanceDescriptor)));

        var path = new EnterpriseSvgImage("M0 0h5v5z", "0 0 5 5");
        var rebuilt = (EnterpriseSvgImage)((InstanceDescriptor)converter.ConvertTo(path, typeof(InstanceDescriptor))!).Invoke()!;
        Assert.Equal(path.PathData, rebuilt.PathData);
        Assert.Equal(path.ViewBox, rebuilt.ViewBox);

        var markup = EnterpriseSvgImage.FromMarkup("<svg viewBox=\"0 0 4 4\"><rect width=\"4\" height=\"4\"/></svg>");
        var rebuiltMarkup = (EnterpriseSvgImage)((InstanceDescriptor)converter.ConvertTo(markup, typeof(InstanceDescriptor))!).Invoke()!;
        Assert.Equal(markup.Markup, rebuiltMarkup.Markup);

        Assert.Equal("M0 0h5v5z", converter.ConvertToInvariantString(path));
        Assert.Equal("M0 0h5v5z", ((EnterpriseSvgImage)converter.ConvertFromInvariantString("M0 0h5v5z")!).PathData);
        Assert.NotNull(((EnterpriseSvgImage)converter.ConvertFromInvariantString("<svg viewBox=\"0 0 4 4\"><rect width=\"4\" height=\"4\"/></svg>")!).Markup);
        Assert.Null(converter.ConvertFromInvariantString(""));
    }

    // ---- access keys ----

    [Theory]
    [InlineData("&Save", "Save", 0)]
    [InlineData("Sa&ve", "Save", 2)]
    [InlineData("Fish && &Chips", "Fish & Chips", 7)]
    [InlineData("A&&B", "A&B", -1)]
    [InlineData("Trailing &", "Trailing &", -1)]
    [InlineData("", "", -1)]
    public void AccessKeysAreSplitFromTheText(string raw, string text, int index)
    {
        var parsed = Mnemonic.Parse(raw);
        Assert.Equal(text, parsed.Text);
        Assert.Equal(index, parsed.Index);
    }

    // ---- layout engine ----

    private static ButtonLayoutEngine.Result Arrange(ButtonContentLayout relation, ContentAlignment text = ContentAlignment.MiddleCenter, ContentAlignment image = ContentAlignment.MiddleCenter, bool rtl = false, Size? icon = null, Func<int, Size>? measure = null) =>
        ButtonLayoutEngine.Arrange(new Rectangle(0, 0, 200, 40), icon ?? new Size(18, 18), measure ?? (_ => new Size(60, 16)), 6, relation, text, image, rtl);

    [Fact]
    public void ImageBeforeTextCentersTheBlockAndAlignsBothVertically()
    {
        var r = Arrange(ButtonContentLayout.ImageBeforeText);
        Assert.Equal(new Rectangle(58, 11, 18, 18), r.Icon);
        Assert.Equal(new Rectangle(82, 12, 60, 16), r.Text);
        Assert.Equal(new Size(84, 18), r.Content);
    }

    [Fact]
    public void ImageAfterTextPutsTheTextFirst()
    {
        var r = Arrange(ButtonContentLayout.ImageAfterText);
        Assert.Equal(58, r.Text.X);
        Assert.Equal(124, r.Icon.X);
    }

    [Fact]
    public void ImageAboveAndTextAboveStackTheParts()
    {
        var above = Arrange(ButtonContentLayout.ImageAboveText);
        Assert.Equal(new Rectangle(91, 0, 18, 18), above.Icon);
        Assert.Equal(new Rectangle(70, 24, 60, 16), above.Text);

        var below = Arrange(ButtonContentLayout.TextAboveImage);
        Assert.Equal(0, below.Text.Y);
        Assert.Equal(22, below.Icon.Y);
    }

    [Fact]
    public void OverlayPlacesImageAndTextIndependently()
    {
        var r = Arrange(ButtonContentLayout.Overlay, ContentAlignment.BottomRight, ContentAlignment.TopLeft);
        Assert.Equal(new Rectangle(0, 0, 18, 18), r.Icon);
        Assert.Equal(new Rectangle(140, 24, 60, 16), r.Text);
    }

    [Theory]
    [InlineData(ContentAlignment.TopLeft, 0, 0)]
    [InlineData(ContentAlignment.TopRight, 116, 0)]
    [InlineData(ContentAlignment.BottomLeft, 0, 22)]
    [InlineData(ContentAlignment.BottomRight, 116, 22)]
    [InlineData(ContentAlignment.MiddleCenter, 58, 11)]
    public void TextAlignmentPositionsTheWholeBlock(ContentAlignment alignment, int x, int y)
    {
        // The block is 84 x 18 in a 200 x 40 area; the icon is the first item of the block.
        var r = Arrange(ButtonContentLayout.ImageBeforeText, alignment);
        Assert.Equal(x, r.Icon.X);
        Assert.Equal(y, r.Icon.Y);
    }

    [Fact]
    public void RightToLeftMirrorsTheArrangement()
    {
        var ltr = Arrange(ButtonContentLayout.ImageBeforeText);
        var rtl = Arrange(ButtonContentLayout.ImageBeforeText, rtl: true);
        Assert.True(rtl.Icon.X > rtl.Text.X);
        Assert.Equal(200 - ltr.Icon.Right, rtl.Icon.X);
        Assert.Equal(200 - ltr.Text.Right, rtl.Text.X);

        var left = Arrange(ButtonContentLayout.ImageBeforeText, ContentAlignment.MiddleLeft, rtl: true);
        Assert.Equal(200, left.Icon.Right);
        Assert.Equal(176, left.Text.Right);
    }

    [Fact]
    public void TextIsClippedToTheSpaceNextToTheImage()
    {
        var r = ButtonLayoutEngine.Arrange(new Rectangle(0, 0, 100, 40), new Size(18, 18), _ => new Size(300, 16), 6, ButtonContentLayout.ImageBeforeText, ContentAlignment.MiddleCenter, ContentAlignment.MiddleCenter, false);
        Assert.Equal(76, r.Text.Width);
        Assert.True(r.Text.Right <= 100);
    }

    [Fact]
    public void WrappingTextIsMeasuredWithTheWidthThatIsLeft()
    {
        var widths = new List<int>();
        ButtonLayoutEngine.Arrange(new Rectangle(0, 0, 200, 40), new Size(18, 18), w => { widths.Add(w); return new Size(50, 32); }, 6, ButtonContentLayout.ImageBeforeText, ContentAlignment.MiddleCenter, ContentAlignment.MiddleCenter, false);
        ButtonLayoutEngine.Arrange(new Rectangle(0, 0, 200, 40), new Size(18, 18), w => { widths.Add(w); return new Size(50, 32); }, 6, ButtonContentLayout.ImageAboveText, ContentAlignment.MiddleCenter, ContentAlignment.MiddleCenter, false);
        Assert.Equal(new[] { 176, 200 }, widths);
    }

    [Fact]
    public void ContentWithoutImageOrWithoutTextHasNoGap()
    {
        var textOnly = ButtonLayoutEngine.Arrange(new Rectangle(0, 0, 200, 40), Size.Empty, _ => new Size(60, 16), 6, ButtonContentLayout.ImageBeforeText, ContentAlignment.MiddleCenter, ContentAlignment.MiddleCenter, false);
        Assert.True(textOnly.Icon.IsEmpty);
        Assert.Equal(new Rectangle(70, 12, 60, 16), textOnly.Text);

        var imageOnly = ButtonLayoutEngine.Arrange(new Rectangle(0, 0, 200, 40), new Size(18, 18), null, 6, ButtonContentLayout.ImageBeforeText, ContentAlignment.MiddleCenter, ContentAlignment.MiddleCenter, false);
        Assert.True(imageOnly.Text.IsEmpty);
        Assert.Equal(new Rectangle(91, 11, 18, 18), imageOnly.Icon);

        Assert.Equal(default, ButtonLayoutEngine.Arrange(new Rectangle(0, 0, 200, 40), Size.Empty, null, 6, ButtonContentLayout.ImageBeforeText, ContentAlignment.MiddleCenter, ContentAlignment.MiddleCenter, false));
    }

    [Theory]
    [InlineData(ContentAlignment.TopLeft, false, ContentAlignment.TopLeft)]
    [InlineData(ContentAlignment.MiddleRight, false, ContentAlignment.TopRight)]
    [InlineData(ContentAlignment.BottomCenter, false, ContentAlignment.TopCenter)]
    [InlineData(ContentAlignment.TopLeft, true, ContentAlignment.TopRight)]
    [InlineData(ContentAlignment.MiddleCenter, true, ContentAlignment.TopCenter)]
    public void TextLinesFollowOnlyTheHorizontalAlignment(ContentAlignment alignment, bool rtl, ContentAlignment expected) =>
        Assert.Equal(expected, ButtonLayoutEngine.HorizontalOnly(alignment, rtl));
}

/// <summary>Runtime theme switching; serialized with the other tests that change the global theme.</summary>
[Collection("Theme")]
public class ButtonThemeTests
{
    [Fact]
    public void ButtonsRenderInEveryThemeAndFollowRuntimeThemeSwitching()
    {
        RunSta(() =>
        {
            var manager = EnterpriseUI.Theming.ThemeManager.Instance;
            var original = manager.CurrentTheme;
            var host = new Form { ClientSize = new Size(300, 200), ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-4000, 0) };
            var primary = new EnterpriseButtonControl { Text = "Primary", Glyph = "★", Location = new Point(10, 10) };
            var outline = new EnterpriseButtonControl { Text = "Outline", Variant = EnterpriseButtonVariant.Outline, Location = new Point(10, 60) };
            var disabled = new EnterpriseButtonControl { Text = "Disabled", Enabled = false, Location = new Point(10, 110) };
            host.Controls.AddRange(new Control[] { primary, outline, disabled });
            host.Show();
            Pump(40);
            var faces = new HashSet<int>();
            try
            {
                foreach (var theme in manager.AvailableThemes.ToList())
                {
                    manager.ApplyTheme(theme.Name);
                    Pump(30);
                    foreach (Control c in new Control[] { primary, outline, disabled })
                    {
                        using var bitmap = Ui.Render(c);
                        Assert.True(Ui.DistinctColors(bitmap) >= 3, $"{theme.Name}: {c.Text}");
                    }

                    using var face = Ui.Render(primary);
                    faces.Add(Pixel(face, 12, primary.Height / 2).ToArgb());
                }
            }
            finally
            {
                manager.ApplyTheme(original.Name);
                host.Dispose();
            }

            Assert.True(faces.Count > 1, "the face color did not change with the theme");
        });
    }
}

/// <summary>Drives the button family like a user and checks state, events, keyboard handling, accessibility, commands and painting.</summary>
public class ButtonFeatureTests
{
    private static Form NewHost(int width = 600, int height = 400) =>
        new() { ClientSize = new Size(width, height), ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-4000, 0) };

    private static T Show<T>(Form host, T control, int x = 10, int y = 10) where T : Control
    {
        control.Location = new Point(x, y);
        host.Controls.Add(control);
        if (!host.Visible)
        {
            host.Show();
            Pump(40);
        }

        return control;
    }

    private static MouseEventArgs Left(int x = 20, int y = 10) => new(MouseButtons.Left, 1, x, y, 0);

    private static Color Face(Control control)
    {
        using var bitmap = Ui.Render(control);
        return Pixel(bitmap, 12, control.Height / 2);
    }

    private static Color Center(Control control)
    {
        using var bitmap = Ui.Render(control);
        return Pixel(bitmap, control.Width / 2, control.Height / 2);
    }

    private static bool SamePixels(Bitmap a, Bitmap b)
    {
        if (a.Size != b.Size) return false;
        for (var y = 0; y < a.Height; y++)
        {
            for (var x = 0; x < a.Width; x++)
            {
                if (a.GetPixel(x, y) != b.GetPixel(x, y)) return false;
            }
        }

        return true;
    }

    private static void SendKeyMessage(Control control, Keys key, bool autoRepeat)
    {
        var message = Message.Create(control.Handle, 0x0100, (IntPtr)(int)key, (IntPtr)(autoRepeat ? 0x40000001 : 1));
        typeof(Control).GetMethod("WndProc", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(control, new object[] { message });
    }

    // ---- content, sizing and layout ----

    [Fact]
    public void PreferredSizeFollowsTextImageAndLayout()
    {
        RunSta(() =>
        {
            var host = NewHost();
            var button = Show(host, new EnterpriseButtonControl { Text = "OK" });
            var small = button.GetPreferredSize(Size.Empty);
            button.Text = "A much longer caption";
            var wide = button.GetPreferredSize(Size.Empty);
            Assert.True(wide.Width > small.Width);

            button.Glyph = "★";
            var withGlyph = button.GetPreferredSize(Size.Empty);
            Assert.True(withGlyph.Width > wide.Width);

            button.ContentLayout = ButtonContentLayout.ImageAboveText;
            Assert.True(button.GetPreferredSize(Size.Empty).Height > withGlyph.Height);

            button.ContentLayout = ButtonContentLayout.ImageBeforeText;
            button.DisplayStyle = ButtonDisplayStyle.Image;
            Assert.True(button.GetPreferredSize(Size.Empty).Width < withGlyph.Width);
            button.DisplayStyle = ButtonDisplayStyle.Text;
            Assert.True(button.GetPreferredSize(Size.Empty).Width < withGlyph.Width);
            host.Dispose();
        });
    }

    [Fact]
    public void AutoSizeGrowsAndOptionallyShrinksWithTheContent()
    {
        RunSta(() =>
        {
            var host = NewHost();
            var button = Show(host, new EnterpriseButtonControl { Text = "Hi", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink });
            Assert.Equal(button.GetPreferredSize(Size.Empty), button.Size);

            var before = button.Width;
            button.Text = "A rather long caption for this button";
            Assert.True(button.Width > before);
            Assert.Equal(button.GetPreferredSize(Size.Empty), button.Size);

            button.Text = "Hi";
            Assert.Equal(before, button.Width);

            var grow = Show(host, new EnterpriseButtonControl { Text = "Hi", AutoSize = true }, 10, 80);
            var start = grow.Width;
            grow.Text = "A rather long caption for this button";
            var grown = grow.Width;
            Assert.True(grown > start);
            grow.Text = "Hi";
            Assert.Equal(grown, grow.Width);
            host.Dispose();
        });
    }

    [Fact]
    public void WrappedTextNeedsMoreHeightWhenTheWidthIsLimited()
    {
        RunSta(() =>
        {
            var host = NewHost();
            var button = Show(host, new EnterpriseButtonControl { Text = "Several words that do not fit on a single line", TextWrap = true });
            var single = button.GetPreferredSize(Size.Empty);
            var wrapped = button.GetPreferredSize(new Size(120, 0));
            Assert.True(wrapped.Height > single.Height);
            Assert.True(wrapped.Width <= 120 + 2);
            host.Dispose();
        });
    }

    [Fact]
    public void SplitAndDropDownButtonsReserveRoomForTheArrow()
    {
        RunSta(() =>
        {
            var host = NewHost();
            var plain = Show(host, new EnterpriseButtonControl { Text = "Export" });
            var split = Show(host, new EnterpriseSplitButtonControl { Text = "Export" }, 10, 60);
            var drop = Show(host, new EnterpriseDropDownButtonControl { Text = "Export", AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink }, 10, 110);
            var arrow = split.GetPreferredSize(Size.Empty).Width - plain.GetPreferredSize(Size.Empty).Width;
            Assert.True(arrow >= Scale(plain, 20));
            Assert.Equal(split.GetPreferredSize(Size.Empty).Width, drop.Width);
            host.Dispose();
        });
    }

    [Fact]
    public void ImageListImagesAreResolvedByIndexAndKey()
    {
        RunSta(() =>
        {
            var host = NewHost();
            using var list = new ImageList { ImageSize = new Size(16, 16), ColorDepth = ColorDepth.Depth32Bit };
            using var red = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(red)) g.Clear(Color.Red);
            list.Images.Add("red", red);
            var button = Show(host, new EnterpriseButtonControl { Text = "Img" });
            var without = button.GetPreferredSize(Size.Empty).Width;

            button.ImageList = list;
            button.ImageIndex = 0;
            Assert.True(button.GetPreferredSize(Size.Empty).Width > without);
            button.ImageIndex = -1;
            Assert.Equal(without, button.GetPreferredSize(Size.Empty).Width);
            button.ImageKey = "red";
            Assert.True(button.GetPreferredSize(Size.Empty).Width > without);
            Assert.Equal(-1, button.ImageIndex);
            button.ImageList = null;
            Assert.Equal(without, button.GetPreferredSize(Size.Empty).Width);
            Assert.Throws<ArgumentOutOfRangeException>(() => button.ImageIndex = -2);
            host.Dispose();
        });
    }

    [Fact]
    public void TextImageRelationMapsToContentLayoutBothWays()
    {
        var button = new EnterpriseButtonControl();
        foreach (var relation in Enum.GetValues<TextImageRelation>())
        {
            button.TextImageRelation = relation;
            Assert.Equal(relation, button.TextImageRelation);
        }

        button.TextImageRelation = TextImageRelation.TextBeforeImage;
        Assert.Equal(ButtonContentLayout.ImageAfterText, button.ContentLayout);
        button.ContentLayout = ButtonContentLayout.TextAboveImage;
        Assert.Equal(TextImageRelation.TextAboveImage, button.TextImageRelation);
        button.Dispose();
    }

    // ---- painting and styling ----

    [Fact]
    public void SvgImageIsPaintedInTheTextColorAndDimmedWhenDisabled()
    {
        RunSta(() =>
        {
            var host = NewHost();
            var button = Show(host, new EnterpriseButtonControl
            {
                Variant = EnterpriseButtonVariant.Outline,
                TextColor = Color.Lime,
                DisplayStyle = ButtonDisplayStyle.Image,
                SvgImage = new EnterpriseSvgImage("M0 0H10V10H0Z", "0 0 10 10"),
                Size = new Size(48, 34)
            });
            Assert.Equal(Color.Lime.ToArgb(), Center(button).ToArgb());

            button.Enabled = false;
            Assert.NotEqual(Color.Lime.ToArgb(), Center(button).ToArgb());
            button.Enabled = true;

            button.DisplayStyle = ButtonDisplayStyle.Text;
            Assert.NotEqual(Color.Lime.ToArgb(), Center(button).ToArgb());
            button.DisplayStyle = ButtonDisplayStyle.None;
            button.DisplayStyle = ButtonDisplayStyle.ImageAndText;
            Assert.Equal(Color.Lime.ToArgb(), Center(button).ToArgb());
            host.Dispose();
        });
    }

    [Fact]
    public void SvgImageWinsOverGlyphAndImage()
    {
        RunSta(() =>
        {
            var host = NewHost();
            using var bitmap = new Bitmap(16, 16);
            var button = Show(host, new EnterpriseButtonControl
            {
                Variant = EnterpriseButtonVariant.Outline,
                TextColor = Color.Lime,
                Glyph = "★",
                Image = bitmap,
                SvgImage = new EnterpriseSvgImage("M0 0H10V10H0Z", "0 0 10 10"),
                DisplayStyle = ButtonDisplayStyle.Image,
                Size = new Size(48, 34)
            });
            Assert.Equal(Color.Lime.ToArgb(), Center(button).ToArgb());
            host.Dispose();
        });
    }

    [Fact]
    public void CustomColorsReplaceTheThemeColorsAndResetToTheTheme()
    {
        RunSta(() =>
        {
            var host = NewHost();
            var button = Show(host, new EnterpriseButtonControl { Text = "OK" });
            var themed = Face(button);
            button.FillColor = Color.FromArgb(200, 30, 30);
            Assert.Equal(Color.FromArgb(200, 30, 30).ToArgb(), Face(button).ToArgb());
            button.FillColor = Color.Empty;
            Assert.Equal(themed.ToArgb(), Face(button).ToArgb());

            var props = TypeDescriptor.GetProperties(button);
            Assert.False(props[nameof(EnterpriseButtonControl.FillColor)]!.ShouldSerializeValue(button));
            button.FillColor = Color.Red;
            Assert.True(props[nameof(EnterpriseButtonControl.FillColor)]!.ShouldSerializeValue(button));
            host.Dispose();
        });
    }

    [Fact]
    public void FacePaintingCanReplaceTheFaceAndReportsTheState()
    {
        RunSta(() =>
        {
            var host = NewHost();
            var button = Show(host, new EnterpriseButtonControl { Text = "OK" });
            ButtonVisualState? seen = null;
            var bounds = Rectangle.Empty;
            button.FacePainting += (_, e) =>
            {
                seen = e.State;
                bounds = e.Bounds;
                using var brush = new SolidBrush(Color.Magenta);
                e.Graphics.FillRectangle(brush, e.Bounds);
                e.Handled = true;
            };
            Assert.Equal(Color.Magenta.ToArgb(), Face(button).ToArgb());
            Assert.Equal(ButtonVisualState.Normal, seen);
            Assert.Equal(button.Width - 2 * Scale(button, 2), bounds.Width);

            button.Enabled = false;
            Face(button);
            Assert.Equal(ButtonVisualState.Disabled, seen);
            host.Dispose();
        });
    }

    private static int Scale(Control control, int value) => (int)Math.Round(value * control.DeviceDpi / 96.0);

    [Fact]
    public void ContentPaintingCanReplaceTheContent()
    {
        RunSta(() =>
        {
            var host = NewHost();
            var button = Show(host, new EnterpriseButtonControl { Text = "OK", Size = new Size(100, 34) });
            var calls = 0;
            Color fore = Color.Empty;
            button.ContentPainting += (_, e) =>
            {
                calls++;
                fore = e.ForeColor;
                using var brush = new SolidBrush(Color.Lime);
                e.Graphics.FillRectangle(brush, e.Bounds.X + e.Bounds.Width / 2 - 4, e.Bounds.Y + e.Bounds.Height / 2 - 4, 8, 8);
                e.Handled = true;
            };
            Assert.Equal(Color.Lime.ToArgb(), Center(button).ToArgb());
            Assert.True(calls > 0);
            Assert.False(fore.IsEmpty);
            host.Dispose();
        });
    }

    [Fact]
    public void DisabledButtonIgnoresClicksAndKeys()
    {
        RunSta(() =>
        {
            var host = NewHost();
            var button = Show(host, new EnterpriseButtonControl { Text = "No", Enabled = false });
            var clicks = 0;
            button.Click += (_, _) => clicks++;
            Ui.Click(button, 20, 10);
            Ui.Key(button, Keys.Space);
            button.PerformClick();
            Assert.Equal(0, clicks);
            Assert.Equal(Cursors.Default, button.Cursor);
            host.Dispose();
        });
    }

    // ---- states ----

    [Fact]
    public void VisualStateFollowsTheMouseAndEnabledState()
    {
        RunSta(() =>
        {
            var host = NewHost();
            var button = Show(host, new EnterpriseButtonControl { Text = "State" });
            var states = new List<ButtonVisualState>();
            button.VisualStateChanged += (_, _) => states.Add(button.VisualState);
            Assert.Equal(ButtonVisualState.Normal, button.VisualState);

            Invoke(button, "OnMouseEnter", EventArgs.Empty);
            Invoke(button, "OnMouseDown", Left());
            Assert.True(button.IsPressed);
            Invoke(button, "OnMouseUp", Left());
            Assert.False(button.IsPressed);
            Invoke(button, "OnMouseLeave", EventArgs.Empty);
            button.Enabled = false;

            Assert.Equal(new[] { ButtonVisualState.Hot, ButtonVisualState.Pressed, ButtonVisualState.Hot, ButtonVisualState.Normal, ButtonVisualState.Disabled }, states);
            host.Dispose();
        });
    }

    [Fact]
    public void AnimationsCanBeTurnedOffPerButton()
    {
        RunSta(() =>
        {
            var host = NewHost();
            var button = Show(host, new EnterpriseButtonControl { Text = "Fast", AnimateStates = false });
            Invoke(button, "OnMouseEnter", EventArgs.Empty);
            var hover = (double)typeof(EnterpriseButtonBase).GetProperty("HoverAmount", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(button)!;
            Assert.Equal(1.0, hover);
            host.Dispose();
        });
    }

    // ---- click handling ----

    [Fact]
    public void ClickModeSelectsWhenTheClickIsRaised()
    {
        RunSta(() =>
        {
            var host = NewHost();
            var release = Show(host, new EnterpriseButtonControl { Text = "R" });
            var press = Show(host, new EnterpriseButtonControl { Text = "P", ClickMode = ButtonClickMode.Press }, 170, 10);
            var hover = Show(host, new EnterpriseButtonControl { Text = "H", ClickMode = ButtonClickMode.Hover }, 330, 10);
            int releaseClicks = 0, pressClicks = 0, hoverClicks = 0;
            release.Click += (_, _) => releaseClicks++;
            press.Click += (_, _) => pressClicks++;
            hover.Click += (_, _) => hoverClicks++;

            Invoke(release, "OnMouseDown", Left());
            Assert.Equal(0, releaseClicks);
            Invoke(release, "OnMouseUp", Left());
            Assert.Equal(1, releaseClicks);

            Invoke(release, "OnMouseDown", Left());
            Invoke(release, "OnMouseUp", new MouseEventArgs(MouseButtons.Left, 1, -30, -30, 0));
            Assert.Equal(1, releaseClicks);

            Invoke(press, "OnMouseDown", Left());
            Assert.Equal(1, pressClicks);
            Invoke(press, "OnMouseUp", Left());
            Assert.Equal(1, pressClicks);

            Invoke(hover, "OnMouseEnter", EventArgs.Empty);
            Assert.Equal(1, hoverClicks);
            Invoke(hover, "OnMouseDown", Left());
            Invoke(hover, "OnMouseUp", Left());
            Assert.Equal(1, hoverClicks);
            host.Dispose();
        });
    }

    [Fact]
    public void HoldingTheKeyDoesNotRepeatTheClick()
    {
        RunSta(() =>
        {
            var host = NewHost();
            var button = Show(host, new EnterpriseButtonControl { Text = "Go" });
            button.Focus();
            var clicks = 0;
            button.Click += (_, _) => clicks++;

            SendKeyMessage(button, Keys.Space, autoRepeat: false);
            Assert.Equal(1, clicks);
            SendKeyMessage(button, Keys.Space, autoRepeat: true);
            SendKeyMessage(button, Keys.Space, autoRepeat: true);
            Assert.Equal(1, clicks);
            SendKeyMessage(button, Keys.Enter, autoRepeat: false);
            Assert.Equal(2, clicks);
            host.Dispose();
        });
    }

    [Fact]
    public void AccessKeyIsIgnoredWhenMnemonicsAreOff()
    {
        RunSta(() =>
        {
            var host = NewHost();
            var button = Show(host, new EnterpriseButtonControl { Text = "&Save" });
            var clicks = 0;
            button.Click += (_, _) => clicks++;
            var process = typeof(Control).GetMethod("ProcessMnemonic", BindingFlags.Instance | BindingFlags.NonPublic)!;

            Assert.True((bool)process.Invoke(button, new object[] { 's' })!);
            Assert.Equal(1, clicks);
            Assert.False((bool)process.Invoke(button, new object[] { 'x' })!);

            button.UseMnemonic = false;
            Assert.False((bool)process.Invoke(button, new object[] { 's' })!);
            Assert.Equal(1, clicks);
            host.Dispose();
        });
    }

    // ---- toggle ----

    [Fact]
    public void ToggleChangeCanBeCanceledBeforeItHappens()
    {
        RunSta(() =>
        {
            var host = NewHost();
            var toggle = Show(host, new EnterpriseToggleButtonControl { Text = "T" });
            var cancel = true;
            var changes = new List<(CheckState Old, CheckState New)>();
            var changed = 0;
            toggle.CheckStateChanging += (_, e) =>
            {
                changes.Add((e.OldState, e.NewState));
                e.Cancel = cancel;
            };
            toggle.CheckedChanged += (_, _) => changed++;

            Ui.Click(toggle, 20, 10);
            Assert.False(toggle.Checked);
            Assert.Equal(0, changed);

            cancel = false;
            Ui.Click(toggle, 20, 10);
            Assert.True(toggle.Checked);
            Assert.Equal(1, changed);
            Assert.Equal(new[] { (CheckState.Unchecked, CheckState.Checked), (CheckState.Unchecked, CheckState.Checked) }, changes);
            host.Dispose();
        });
    }

    [Fact]
    public void ExclusiveGroupUnchecksTheOthersEvenWhenTheyWouldCancel()
    {
        RunSta(() =>
        {
            var host = NewHost();
            var a = Show(host, new EnterpriseToggleButtonControl { Text = "A", GroupName = "g", Checked = true });
            var b = Show(host, new EnterpriseToggleButtonControl { Text = "B", GroupName = "g" }, 170, 10);
            a.CheckStateChanging += (_, e) => e.Cancel = true;
            b.Checked = true;
            Assert.True(b.Checked);
            Assert.False(a.Checked);
            host.Dispose();
        });
    }

    // ---- commands ----

    [Fact]
    public void CommandDrivesEnabledStateAndRunsAfterClick()
    {
        RunSta(() =>
        {
            var host = NewHost();
            var allowed = false;
            var order = new List<string>();
            var command = new RelayCommand(p => order.Add("command:" + p), p => allowed && (string?)p == "x");
            var button = Show(host, new EnterpriseButtonControl { Text = "Cmd", CommandParameter = "x" });
            button.Click += (_, _) => order.Add("click");

            button.Command = command;
            Assert.False(button.Enabled);
            allowed = true;
            command.RaiseCanExecuteChanged();
            Assert.True(button.Enabled);

            Ui.Click(button, 20, 10);
            Assert.Equal(new[] { "click", "command:x" }, order);

            allowed = false;
            command.RaiseCanExecuteChanged();
            Assert.False(button.Enabled);
            button.CommandParameter = "y";
            button.Command = null;
            Assert.True(button.Enabled);
            host.Dispose();
        });
    }

    [Fact]
    public void CommandStateChangesFromOtherThreadsAreMarshaled()
    {
        RunSta(() =>
        {
            var host = NewHost();
            var allowed = false;
            var command = new RelayCommand(_ => { }, _ => allowed);
            var button = Show(host, new EnterpriseButtonControl { Text = "Async", Command = command });
            Assert.False(button.Enabled);
            allowed = true;
            Task.Run(command.RaiseCanExecuteChanged).Wait();
            PumpUntil(() => button.Enabled);
            host.Dispose();
        });
    }

    [Fact]
    public void RestoresAnAlreadyDisabledButtonWhenTheCommandIsRemoved()
    {
        RunSta(() =>
        {
            var button = new EnterpriseButtonControl { Enabled = false, Command = new RelayCommand(_ => { }) };
            Assert.True(button.Enabled);
            button.Command = null;
            Assert.False(button.Enabled);
            button.Dispose();
        });
    }

    [Fact]
    public void DisposedButtonStopsListeningToItsCommand()
    {
        RunSta(() =>
        {
            var command = new RelayCommand(_ => { }, _ => true);
            var button = new EnterpriseButtonControl { Command = command };
            button.Dispose();
            command.RaiseCanExecuteChanged();
        });
    }

    // ---- tooltips ----

    [Fact]
    public void ToolTipHandlerWinsOverTheTextAndImageOnlyButtonsFallBackToTheirText()
    {
        RunSta(() =>
        {
            var button = new EnterpriseButtonControl { ToolTipText = "Static" };
            Assert.Equal("Static", button.ResolveToolTipText());
            button.ToolTipTextNeeded += (_, e) => e.ToolTipText = e.ToolTipText + " / dynamic";
            Assert.Equal("Static / dynamic", button.ResolveToolTipText());

            var iconOnly = new EnterpriseButtonControl { Text = "&Print", Glyph = "P", DisplayStyle = ButtonDisplayStyle.Image };
            Assert.Equal("Print", iconOnly.ResolveToolTipText());
            iconOnly.DisplayStyle = ButtonDisplayStyle.ImageAndText;
            Assert.Null(iconOnly.ResolveToolTipText());
            button.Dispose();
            iconOnly.Dispose();
        });
    }

    // ---- accessibility ----

    [Fact]
    public void AccessibleObjectExposesNameShortcutValueActionAndState()
    {
        RunSta(() =>
        {
            var host = NewHost();
            var button = Show(host, new EnterpriseButtonControl { Text = "&Save", BadgeCount = 3 });
            var accessible = button.AccessibilityObject;
            Assert.Equal("Save", accessible.Name);
            Assert.Equal("Alt+S", accessible.KeyboardShortcut);
            Assert.Equal("Press", accessible.DefaultAction);
            Assert.Equal(AccessibleRole.PushButton, accessible.Role);
            Assert.Equal("3", accessible.Value);

            var clicks = 0;
            button.Click += (_, _) => clicks++;
            accessible.DoDefaultAction();
            Assert.Equal(1, clicks);

            button.NotifyDefault(true);
            Assert.True(accessible.State.HasFlag(AccessibleStates.Default));

            button.AccessibleName = "Save document";
            Assert.Equal("Save document", accessible.Name);

            button.UseMnemonic = false;
            button.AccessibleName = null;
            Assert.Equal("&Save", accessible.Name);
            Assert.Null(accessible.KeyboardShortcut);
            host.Dispose();
        });
    }

    [Fact]
    public void IconOnlyButtonsGetTheirNameFromTheToolTip()
    {
        RunSta(() =>
        {
            var host = NewHost();
            var button = Show(host, new EnterpriseButtonControl { Text = string.Empty, Glyph = "✕", ToolTipText = "Close", Size = new Size(34, 34) });
            Assert.Equal("Close", button.AccessibilityObject.Name);
            host.Dispose();
        });
    }

    [Fact]
    public void ToggleAndSplitButtonsExposeTheirOwnRolesAndActions()
    {
        RunSta(() =>
        {
            var host = NewHost();
            var toggle = Show(host, new EnterpriseToggleButtonControl { Text = "Bold", Checked = true });
            Assert.Equal("Toggle", toggle.AccessibilityObject.DefaultAction);
            Assert.Equal(AccessibleRole.CheckButton, toggle.AccessibilityObject.Role);
            Assert.True(toggle.AccessibilityObject.State.HasFlag(AccessibleStates.Checked));

            var split = Show(host, new EnterpriseSplitButtonControl { Text = "Export" }, 10, 60);
            Assert.Equal(AccessibleRole.SplitButton, split.AccessibilityObject.Role);
            Assert.Equal("Export", split.AccessibilityObject.Name);
            host.Dispose();
        });
    }

    [Fact]
    public void FocusRingIsPaintedForKeyboardFocusOnlyAndCanBeSwitchedOff()
    {
        RunSta(() =>
        {
            var host = NewHost();
            var button = Show(host, new EnterpriseButtonControl { Text = "Focus", Variant = EnterpriseButtonVariant.Outline, Size = new Size(120, 34) });
            button.Focus();
            if (!button.Focused) return; // another window owns the foreground (parallel tests); nothing to verify then
            using var keyboard = Ui.Render(button);

            button.ShowFocusRing = false;
            using var switchedOff = Ui.Render(button);
            Assert.False(SamePixels(keyboard, switchedOff), "the ring was not drawn for keyboard focus");

            button.ShowFocusRing = true;
            Invoke(button, "OnMouseDown", Left(60, 17));
            Invoke(button, "OnMouseUp", Left(60, 17));
            Pump(300);
            using var mouse = Ui.Render(button);
            Assert.True(SamePixels(mouse, switchedOff), "mouse focus must not draw the ring");

            Invoke(button, "OnKeyDown", new KeyEventArgs(Keys.Tab));
            using var again = Ui.Render(button);
            Assert.True(SamePixels(again, keyboard), "the ring must return with the next key press");
            host.Dispose();
        });
    }

    // ---- design time ----

    [Fact]
    public void DesignerSupportDescribesTheButtonFamily()
    {
        var attributes = TypeDescriptor.GetAttributes(typeof(EnterpriseSplitButtonControl));
        Assert.Contains(attributes.OfType<DesignerAttribute>(), a => a.DesignerTypeName.Contains("ButtonActions"));
        Assert.Equal(nameof(Control.Click), ((DefaultEventAttribute)attributes[typeof(DefaultEventAttribute)]!).Name);
        Assert.Equal(nameof(Control.Text), ((DefaultPropertyAttribute)attributes[typeof(DefaultPropertyAttribute)]!).Name);

        using var button = new EnterpriseButtonControl();
        var props = TypeDescriptor.GetProperties(button);
        Assert.Equal("Appearance", props[nameof(button.Variant)]!.Category);
        Assert.Equal("Behavior", props[nameof(button.ClickMode)]!.Category);
        Assert.False(string.IsNullOrEmpty(props[nameof(button.SvgImage)]!.Description));
        Assert.False(props[nameof(button.Command)]!.IsBrowsable);
        Assert.False(props[nameof(button.CommandParameter)]!.IsBrowsable);
        Assert.True(props[nameof(button.AutoSize)]!.IsBrowsable);
        Assert.True(props[nameof(button.SvgImage)]!.Converter.CanConvertTo(typeof(InstanceDescriptor)));
        Assert.False(props[nameof(button.ClickMode)]!.ShouldSerializeValue(button));
        button.ClickMode = ButtonClickMode.Press;
        Assert.True(props[nameof(button.ClickMode)]!.ShouldSerializeValue(button));
        Assert.False(props[nameof(button.ImageSize)]!.ShouldSerializeValue(button));
        button.ImageSize = new Size(24, 24);
        Assert.True(props[nameof(button.ImageSize)]!.ShouldSerializeValue(button));
    }

    [Fact]
    public void SmartTagChangesPropertiesThroughTheDescriptors()
    {
        using var button = new EnterpriseButtonControl { Text = "Before" };
        var actions = new ButtonActions(button);
        Assert.Equal(9, actions.GetSortedActionItems().Count);

        actions.Text = "After";
        actions.Variant = EnterpriseButtonVariant.Danger;
        actions.Glyph = "!";
        actions.DisplayStyle = ButtonDisplayStyle.Text;
        actions.ContentLayout = ButtonContentLayout.Overlay;
        actions.CornerRadius = 9;
        actions.ClickMode = ButtonClickMode.Press;
        actions.ToolTipText = "Careful";
        actions.AutoSize = true;

        Assert.Equal("After", button.Text);
        Assert.Equal(EnterpriseButtonVariant.Danger, button.Variant);
        Assert.Equal("!", button.Glyph);
        Assert.Equal(ButtonDisplayStyle.Text, button.DisplayStyle);
        Assert.Equal(ButtonContentLayout.Overlay, button.ContentLayout);
        Assert.Equal(9, button.CornerRadius);
        Assert.Equal(ButtonClickMode.Press, button.ClickMode);
        Assert.Equal("Careful", button.ToolTipText);
        Assert.True(button.AutoSize);
        Assert.Equal("After", actions.Text);
    }

    // ---- right to left and high DPI ----

    [Fact]
    public void RightToLeftButtonsStillRender()
    {
        RunSta(() =>
        {
            var host = NewHost();
            var button = Show(host, new EnterpriseButtonControl { Text = "&Save", Glyph = "✔", RightToLeft = RightToLeft.Yes });
            using var bitmap = Ui.Render(button);
            Assert.True(Ui.DistinctColors(bitmap) >= 3);
            host.Dispose();
        });
    }

    [Fact]
    public void PreferredSizeScalesWithDeviceDpi()
    {
        RunSta(() =>
        {
            var host = NewHost();
            var button = Show(host, new EnterpriseButtonControl { Text = "Scale", Glyph = "★" });
            var preferred = button.GetPreferredSize(Size.Empty);
            Assert.True(preferred.Height >= (int)Math.Round(28 * button.DeviceDpi / 96.0));
            Assert.True(preferred.Width >= (int)Math.Round(30 * button.DeviceDpi / 96.0));
            host.Dispose();
        });
    }
}
