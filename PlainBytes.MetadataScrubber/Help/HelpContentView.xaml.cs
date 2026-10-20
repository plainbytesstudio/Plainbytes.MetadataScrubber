// SPDX-FileCopyrightText: 2026 PlainBytes Studio
// SPDX-License-Identifier: GPL-3.0-only

using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace PlainBytes.MetadataScrubber.Help;

/// <summary>
/// Renders one <see cref="HelpSection"/>: section title and a list of content blocks.
/// </summary>
public partial class HelpContentView : UserControl
{
    /// <summary>Identifies <see cref="Section"/>.</summary>
    public static readonly DependencyProperty SectionProperty =
        DependencyProperty.Register(
            nameof(Section),
            typeof(HelpSection),
            typeof(HelpContentView),
            new PropertyMetadata(null, OnSectionChanged));

    public HelpContentView()
    {
        InitializeComponent();
    }

    /// <summary>Raised when a help link block is activated; argument is <see cref="HelpLinkBlock.ActionId"/>.</summary>
    public event Action<string>? LinkActionRequested;

    /// <summary>Active help section to display; null clears title and blocks.</summary>
    public HelpSection? Section
    {
        get => (HelpSection?)GetValue(SectionProperty);
        set => SetValue(SectionProperty, value);
    }

    /// <summary>Updates title, blocks list, and scrolls to top when <see cref="Section"/> changes.</summary>
    private static void OnSectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not HelpContentView view)
            return;

        var section = e.NewValue as HelpSection;
        view.TitleText.Text = section?.Title ?? "";
        view.BlocksList.ItemsSource = section?.Blocks;
        view.RootScroll.ScrollToHome();
    }

    private void HelpLink_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string actionId } || string.IsNullOrWhiteSpace(actionId))
            return;

        e.Handled = true;
        LinkActionRequested?.Invoke(actionId);
    }
}

/// <summary>
/// Converts null/empty/whitespace strings to <see cref="Visibility"/>.
/// Pass parameter <c>Invert</c> to reverse Visible/Collapsed.
/// </summary>
public sealed class NullOrEmptyToVisibilityConverter : IValueConverter
{
    /// <summary>Returns Visible when value is non-empty (or Collapsed when inverted).</summary>
    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        var invert = parameter as string == "Invert";
        var empty = value is null || (value is string s && string.IsNullOrWhiteSpace(s));
        var visible = !empty;
        if (invert)
            visible = !visible;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Not supported; returns <see cref="Binding.DoNothing"/>.</summary>
    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
        Binding.DoNothing;
}
