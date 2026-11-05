using System.Reflection;
using System.Runtime.ExceptionServices;
using EnterpriseUI.Grids;
using EnterpriseUI.WinForms;

namespace EnterpriseUI.Tests.Support;

public sealed class Row
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public bool Active { get; set; }
    public DateTime Date { get; set; }
    public string Priority { get; set; } = "Normal";
}

/// <summary>Exposes the protected input handlers so tests can drive the control like a user without a message loop.</summary>
public sealed class TestGrid<T> : EnterpriseDataGrid<T>
{
    public void Key(Keys keys) => OnKeyDown(new KeyEventArgs(keys));

    public void Type(char ch) => OnKeyPress(new KeyPressEventArgs(ch));

    public void Down(int x, int y, MouseButtons button = MouseButtons.Left) => OnMouseDown(new MouseEventArgs(button, 1, x, y, 0));

    public void Up(int x, int y, MouseButtons button = MouseButtons.Left) => OnMouseUp(new MouseEventArgs(button, 1, x, y, 0));

    public void Move(int x, int y) => OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, x, y, 0));

    public void Click(int x, int y)
    {
        Down(x, y);
        Up(x, y);
    }

    public void DoubleClick(int x, int y)
    {
        Down(x, y);
        Up(x, y);
        OnMouseDoubleClick(new MouseEventArgs(MouseButtons.Left, 2, x, y, 0));
    }

    public Bitmap Render()
    {
        var bitmap = new Bitmap(Width, Height);
        DrawToBitmap(bitmap, new Rectangle(0, 0, Width, Height));
        return bitmap;
    }

    public float Scale => DeviceDpi / 96f;
    public int S(double logical) => (int)Math.Round(logical * Scale);
}

public static class WinFormsTestSupport
{
    public static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    public static void Pump(int milliseconds = 60)
    {
        var until = DateTime.UtcNow.AddMilliseconds(milliseconds);
        while (DateTime.UtcNow < until)
        {
            Application.DoEvents();
            Thread.Sleep(5);
        }
    }

    public static void PumpUntil(Func<bool> condition, int timeoutMs = 3000)
    {
        var until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition())
        {
            if (DateTime.UtcNow > until) throw new TimeoutException("Condition was not met in time.");
            Application.DoEvents();
            Thread.Sleep(5);
        }
    }

    /// <summary>Waits for a task while pumping messages; blocking the UI thread would deadlock the engine's UI-context continuations.</summary>
    public static void Wait(Task task, int timeoutMs = 60000)
    {
        PumpUntil(() => task.IsCompleted, timeoutMs);
        task.GetAwaiter().GetResult();
    }

    public static List<Row> Rows(int count)
    {
        var regions = new[] { "EMEA", "APAC", "AMER" };
        return Enumerable.Range(1, count).Select(i => new Row
        {
            Id = i,
            Name = $"Name {i:0000}",
            Region = regions[i % regions.Length],
            Amount = 10m * (i % 97) + i % 7,
            Active = i % 3 != 0,
            Date = new DateTime(2024, 1, 1).AddDays(i % 365),
            Priority = i % 10 == 0 ? "Critical" : "Normal"
        }).ToList();
    }

    /// <summary>A loaded, sized grid with standard columns, no group panel and fixed row height so tests can compute hit points.</summary>
    public static TestGrid<Row> CreateGrid(int rows = 50, Action<TestGrid<Row>>? configure = null, bool attachToForm = true)
    {
        var grid = new TestGrid<Row>
        {
            Width = 900,
            Height = 500,
            ShowGroupPanel = false,
            ItemHeight = 30,
            ShowSummaryFooter = false,
            AriaLabel = "Test rows"
        };
        grid.Columns.Add(new GridColumn { Field = "Id", Title = "Id", Width = 80, ReadOnly = true });
        grid.Columns.Add(new GridColumn { Field = "Name", Title = "Name", Width = 160 });
        grid.Columns.Add(new GridColumn { Field = "Region", Title = "Region", Width = 100 });
        grid.Columns.Add(new GridColumn { Field = "Amount", Title = "Amount", Width = 100, FormatString = "N2", Alignment = ColumnTextAlignment.Right });
        grid.Columns.Add(new GridColumn { Field = "Active", Title = "Active", Width = 80 });
        grid.Columns.Add(new GridColumn { Field = "Date", Title = "Date", Width = 110, FormatString = "d" });
        grid.Columns.Add(new GridColumn { Field = "Priority", Title = "Priority", Width = 90 });
        configure?.Invoke(grid);
        if (attachToForm)
        {
            var form = new Form { ClientSize = new Size(900, 500), ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-4000, 0) };
            grid.Dock = DockStyle.Fill;
            form.Controls.Add(grid);
            form.Show();
        }
        grid.DataSource = Rows(rows);
        Wait(grid.RefreshDataAsync());
        Pump(80);
        return grid;
    }

    /// <summary>Calls a non-public method such as an editor's KeyDown handler.</summary>
    public static void Invoke(object target, string method, params object?[] args) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!.Invoke(target, args);

    public static TextBox? OpenEditor(Control grid) => grid.Controls.OfType<TextBox>().FirstOrDefault(t => t.Visible);

    public static void PressInEditor(Control editor, Keys keys)
    {
        var args = new KeyEventArgs(keys);
        Invoke(editor, "OnKeyDown", args);
    }

    public static Color Pixel(Bitmap bitmap, int x, int y) => bitmap.GetPixel(Math.Clamp(x, 0, bitmap.Width - 1), Math.Clamp(y, 0, bitmap.Height - 1));

    public static Control Host(Control grid) => grid.FindForm() ?? grid;
}
