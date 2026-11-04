using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using EnterpriseUI.Abstractions.Theming;
using EnterpriseUI.Scheduling;
using EnterpriseUI.Theming;
using EnterpriseUI.WinForms;

namespace EnterpriseUI.WinFormsDemo.Controls;

public class WinFormsSchedulerControl : Control, IThemeableControl
{
    private readonly Scheduler _scheduler = new();
    private ITheme _theme = ThemeManager.Instance.CurrentTheme;
    public string? ThemeOverride { get; set; }

    public DateTime SelectedDate
    {
        get => _scheduler.SelectedDate;
        set { _scheduler.SelectedDate = value; Invalidate(); }
    }

    public SchedulerViewType ActiveView
    {
        get => _scheduler.ActiveView;
        set { _scheduler.ActiveView = value; Invalidate(); }
    }

    public List<Appointment> Appointments => _scheduler.Appointments;
    public List<SchedulerResource> Resources => _scheduler.Resources;

    public TimeSpan DayStartTime
    {
        get => _scheduler.DayStartTime;
        set { _scheduler.DayStartTime = value; Invalidate(); }
    }

    public TimeSpan DayEndTime
    {
        get => _scheduler.DayEndTime;
        set { _scheduler.DayEndTime = value; Invalidate(); }
    }

    public int TimeSlotMinutes
    {
        get => _scheduler.TimeSlotMinutes;
        set { _scheduler.TimeSlotMinutes = value; Invalidate(); }
    }

    public string? AriaLabel
    {
        get => _scheduler.AriaLabel;
        set => _scheduler.AriaLabel = value;
    }

    public WinFormsSchedulerControl()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw,
            true);

        DoubleBuffered = true;
        Size = new Size(680, 420);
        Font = new Font("Segoe UI", 9f);

        _scheduler.SelectedDate = DateTime.Today;
        _scheduler.ActiveView = SchedulerViewType.Week;

        // Default appointments
        _scheduler.Appointments.Add(new Appointment
        {
            Subject = "Executive Quarterly Review",
            Start = DateTime.Today.AddHours(9),
            End = DateTime.Today.AddHours(10.5),
            Color = "#0078d4",
            Location = "Boardroom A"
        });
        _scheduler.Appointments.Add(new Appointment
        {
            Subject = "Architecture Alignment & Design",
            Start = DateTime.Today.AddHours(11),
            End = DateTime.Today.AddHours(12.5),
            Color = "#107c10",
            Location = "Virtual Teams"
        });
        _scheduler.Appointments.Add(new Appointment
        {
            Subject = "Release Verification Sprint",
            Start = DateTime.Today.AddDays(1).AddHours(14),
            End = DateTime.Today.AddDays(1).AddHours(16),
            Color = "#d13438",
            Location = "QA Lab 3"
        });

        ThemeManager.Instance.ThemeChanged += (_, newTheme) => OnThemeChanged(newTheme);
    }

    public void OnThemeChanged(ITheme newTheme)
    {
        _theme = newTheme;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        var bg = WinFormsThemeEngine.GetColor(_theme, DesignTokens.Surface, Color.White);
        var fg = WinFormsThemeEngine.GetColor(_theme, DesignTokens.TextColor, Color.Black);
        var border = WinFormsThemeEngine.GetColor(_theme, DesignTokens.BorderColor, Color.FromArgb(220, 220, 220));
        var surfaceAlt = WinFormsThemeEngine.GetColor(_theme, DesignTokens.SurfaceAlt, Color.FromArgb(245, 245, 245));
        var primary = WinFormsThemeEngine.GetColor(_theme, DesignTokens.Primary, Color.FromArgb(0, 120, 215));

        g.Clear(bg);

        var timeColWidth = 60;
        var headerHeight = 36;
        var daysCount = _scheduler.ActiveView == SchedulerViewType.Day ? 1 : 5;
        var dayColWidth = (Width - timeColWidth) / (float)daysCount;
        var totalHours = 10; // 8 AM to 6 PM
        var rowHeight = (Height - headerHeight) / (float)totalHours;

        // Draw Header
        using (var headerBrush = new SolidBrush(surfaceAlt))
        using (var borderPen = new Pen(border))
        using (var textBrush = new SolidBrush(fg))
        using (var boldFont = new Font(Font, FontStyle.Bold))
        {
            g.FillRectangle(headerBrush, 0, 0, Width, headerHeight);
            g.DrawLine(borderPen, 0, headerHeight, Width, headerHeight);

            // Time column header
            g.DrawString("Time", boldFont, textBrush, 12, 10);
            g.DrawLine(borderPen, timeColWidth, 0, timeColWidth, Height);

            // Day headers
            var monday = _scheduler.SelectedDate.AddDays(-(int)_scheduler.SelectedDate.DayOfWeek + 1);
            for (int d = 0; d < daysCount; d++)
            {
                var curDay = monday.AddDays(d);
                var x = timeColWidth + (d * dayColWidth);
                g.DrawLine(borderPen, x, 0, x, Height);
                var title = $"{curDay:ddd, MMM d}";
                g.DrawString(title, boldFont, textBrush, x + 8, 10);
            }
        }

        // Draw Time Slots
        using (var gridPen = new Pen(Color.FromArgb(60, border)))
        using (var textBrush = new SolidBrush(Color.FromArgb(120, fg)))
        {
            for (int h = 0; h < totalHours; h++)
            {
                var y = headerHeight + (h * rowHeight);
                g.DrawLine(gridPen, 0, y, Width, y);

                var timeStr = $"{8 + h}:00";
                g.DrawString(timeStr, Font, textBrush, 8, y + 4);
            }
        }

        // Draw Appointments
        var mondayWeek = _scheduler.SelectedDate.AddDays(-(int)_scheduler.SelectedDate.DayOfWeek + 1);
        foreach (var appt in _scheduler.Appointments)
        {
            var dayOffset = (appt.Start.Date - mondayWeek.Date).Days;
            if (dayOffset >= 0 && dayOffset < daysCount)
            {
                var startHour = appt.Start.Hour + (appt.Start.Minute / 60.0);
                var endHour = appt.End.Hour + (appt.End.Minute / 60.0);

                if (startHour >= 8 && endHour <= 18)
                {
                    var x = timeColWidth + (dayOffset * dayColWidth) + 4;
                    var y = headerHeight + (float)((startHour - 8) * rowHeight) + 2;
                    var w = dayColWidth - 8;
                    var h = (float)((endHour - startHour) * rowHeight) - 4;

                    var apptColor = string.IsNullOrEmpty(appt.Color) ? primary : ColorTranslator.FromHtml(appt.Color);
                    var apptRect = new RectangleF(x, y, w, h);

                    using var fillBrush = new SolidBrush(Color.FromArgb(40, apptColor));
                    using var barBrush = new SolidBrush(apptColor);
                    using var borderPen = new Pen(apptColor, 1f);
                    using var titleBrush = new SolidBrush(Color.FromArgb(30, 30, 30));
                    using var titleFont = new Font(Font, FontStyle.Bold);
                    using var locFont = new Font(Font.FontFamily, 8f);

                    g.FillRoundedRectangle(fillBrush, apptRect, 4);
                    g.DrawRoundedRectangle(borderPen, apptRect, 4);
                    // Left color accent bar
                    g.FillRectangle(barBrush, x, y, 4, h);

                    g.DrawString(appt.Subject, titleFont, titleBrush, x + 8, y + 4);
                    if (!string.IsNullOrEmpty(appt.Location))
                    {
                        g.DrawString(appt.Location, locFont, titleBrush, x + 8, y + 20);
                    }
                }
            }
        }
    }
}

