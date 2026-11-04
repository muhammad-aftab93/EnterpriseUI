using System;
using System.Drawing;
using System.Windows.Forms;
using EnterpriseUI.Map;
using EnterpriseUI.WinFormsDemo.Controls;
using EnterpriseUI.WinFormsDemo.Infrastructure;

namespace EnterpriseUI.WinFormsDemo.Modules;

public class MapsDemoView : UserControl
{
    private readonly BaseDemoView _baseView;
    private readonly WinFormsMapControl _mapControl;

    public MapsDemoView(DemoItem metadata)
    {
        Dock = DockStyle.Fill;
        Font = new Font("Segoe UI", 9f);

        _baseView = new BaseDemoView { Dock = DockStyle.Fill };
        _baseView.BindDemoMetadata(metadata);
        Controls.Add(_baseView);

        var toolbar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 44,
            Padding = new Padding(6)
        };

        var btnZoomIn = new Button
        {
            Text = "Zoom In (+)",
            Location = new Point(8, 8),
            Size = new Size(110, 28),
            FlatStyle = FlatStyle.Flat
        };
        btnZoomIn.Click += (_, _) =>
        {
            _mapControl.ZoomLevel++;
            _mapControl.Invalidate();
        };

        var btnZoomOut = new Button
        {
            Text = "Zoom Out (-)",
            Location = new Point(126, 8),
            Size = new Size(110, 28),
            FlatStyle = FlatStyle.Flat
        };
        btnZoomOut.Click += (_, _) =>
        {
            _mapControl.ZoomLevel--;
            _mapControl.Invalidate();
        };

        var btnFleet = new Button
        {
            Text = "Simulate Fleet Movement",
            Location = new Point(244, 8),
            Size = new Size(180, 28),
            FlatStyle = FlatStyle.Flat
        };
        btnFleet.Click += (_, _) =>
        {
            var rnd = new Random();
            foreach (var m in _mapControl.Markers)
            {
                m.Location = new GeoLocation(
                    m.Location.Latitude + (rnd.NextDouble() * 0.4 - 0.2),
                    m.Location.Longitude + (rnd.NextDouble() * 0.4 - 0.2));
            }
            _mapControl.Invalidate();
        };

        toolbar.Controls.AddRange(new Control[] { btnZoomIn, btnZoomOut, btnFleet });

        _mapControl = new WinFormsMapControl { Dock = DockStyle.Fill };
        _mapControl.Center = new GeoLocation(37.7749, -122.4194);
        _mapControl.ZoomLevel = 8;

        _mapControl.Markers.Add(new GeoMarker
        {
            Title = "HQ Data Center",
            Location = new GeoLocation(37.7749, -122.4194),
            Description = "Primary Cloud Region (Tier IV)",
            Color = "#0078d4"
        });
        _mapControl.Markers.Add(new GeoMarker
        {
            Title = "Logistics Hub Alpha",
            Location = new GeoLocation(37.8044, -122.2712),
            Description = "Oakland Port Facility (42 trucks dispatched)",
            Color = "#107c10"
        });
        _mapControl.Markers.Add(new GeoMarker
        {
            Title = "Asset Fleet #108",
            Location = new GeoLocation(37.6213, -122.3790),
            Description = "Autonomous Freight in Transit (Speed: 58 mph)",
            Color = "#d13438"
        });

        _baseView.DemoContainer.Controls.Add(_mapControl);
        _baseView.DemoContainer.Controls.Add(toolbar);
    }
}

