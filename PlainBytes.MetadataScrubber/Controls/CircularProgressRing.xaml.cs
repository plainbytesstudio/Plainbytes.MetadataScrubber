// SPDX-FileCopyrightText: 2026 PlainBytes Studio
// SPDX-License-Identifier: GPL-3.0-only

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PlainBytes.MetadataScrubber.Controls;

/// <summary>
/// Circular progress indicator that draws a stroked arc from 0–100%.
/// </summary>
public partial class CircularProgressRing : UserControl
{
    /// <summary>Identifies <see cref="Progress"/>.</summary>
    public static readonly DependencyProperty ProgressProperty =
        DependencyProperty.Register(
            nameof(Progress),
            typeof(double),
            typeof(CircularProgressRing),
            new PropertyMetadata(0d, (d, _) => ((CircularProgressRing)d).UpdateArc()));

    public CircularProgressRing()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateArc();
        SizeChanged += (_, _) => UpdateArc();
    }

    /// <summary>Progress percentage in the range 0–100.</summary>
    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    /// <summary>
    /// Rebuilds <see cref="ProgressArc"/> geometry from the current size and <see cref="Progress"/>.
    /// Starts at 12 o'clock and sweeps clockwise.
    /// </summary>
    private void UpdateArc()
    {
        if (ProgressArc == null)
            return;

        var pct = Math.Clamp(Progress, 0, 100);
        var size = Math.Min(ActualWidth > 0 ? ActualWidth : Width, ActualHeight > 0 ? ActualHeight : Height);
        const double stroke = 3;
        var radius = (size - stroke) / 2;
        var center = size / 2;
        var angle = pct / 100d * 360d;

        if (angle <= 0.01)
        {
            ProgressArc.Data = Geometry.Empty;
            return;
        }

        if (angle >= 359.9)
        {
            ProgressArc.Data = new EllipseGeometry(new Point(center, center), radius, radius);
            return;
        }

        const double startDeg = -90;
        var startRad = DegreesToRadians(startDeg);
        var endRad = DegreesToRadians(startDeg + angle);
        var start = new Point(center + radius * Math.Cos(startRad), center + radius * Math.Sin(startRad));
        var end = new Point(center + radius * Math.Cos(endRad), center + radius * Math.Sin(endRad));

        var figure = new PathFigure { StartPoint = start, IsClosed = false };
        figure.Segments.Add(new ArcSegment
        {
            Point = end,
            Size = new Size(radius, radius),
            IsLargeArc = angle > 180,
            SweepDirection = SweepDirection.Clockwise,
            IsStroked = true,
        });

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        ProgressArc.Data = geometry;
    }

    /// <summary>Converts degrees to radians.</summary>
    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180d;
}
