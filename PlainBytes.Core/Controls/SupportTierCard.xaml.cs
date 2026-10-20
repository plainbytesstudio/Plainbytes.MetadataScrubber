// SPDX-FileCopyrightText: 2026 PlainBytes Studio
// SPDX-License-Identifier: GPL-3.0-only
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace PlainBytes.Core.Controls;

/// <summary>
/// Single support tier card on the Support page (icon, price, optional "Popular" badge, tip button).
/// </summary>
public partial class SupportTierCard : UserControl
{
    /// <summary>Identifies <see cref="IconPath"/>.</summary>
    public static readonly DependencyProperty IconPathProperty =
        DependencyProperty.Register(nameof(IconPath), typeof(ImageSource), typeof(SupportTierCard),
            new PropertyMetadata(null, (d, e) => ((SupportTierCard)d).IconImage.Source = e.NewValue as ImageSource));

    /// <summary>Identifies <see cref="IconBackground"/>.</summary>
    public static readonly DependencyProperty IconBackgroundProperty =
        DependencyProperty.Register(nameof(IconBackground), typeof(Brush), typeof(SupportTierCard),
            new PropertyMetadata(null, (d, e) =>
            {
                if (e.NewValue is Brush brush)
                    ((SupportTierCard)d).IconBackdrop.Background = brush;
            }));

    /// <summary>Identifies <see cref="Title"/>.</summary>
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(SupportTierCard),
            new PropertyMetadata("", (d, e) => ((SupportTierCard)d).TitleText.Text = e.NewValue as string ?? ""));

    /// <summary>Identifies <see cref="Description"/>.</summary>
    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(nameof(Description), typeof(string), typeof(SupportTierCard),
            new PropertyMetadata("", (d, e) => ((SupportTierCard)d).DescriptionText.Text = e.NewValue as string ?? ""));

    /// <summary>Identifies <see cref="Price"/>.</summary>
    public static readonly DependencyProperty PriceProperty =
        DependencyProperty.Register(nameof(Price), typeof(string), typeof(SupportTierCard),
            new PropertyMetadata("", (d, e) => ((SupportTierCard)d).PriceText.Text = e.NewValue as string ?? ""));

    /// <summary>Identifies <see cref="IsRecommended"/>.</summary>
    public static readonly DependencyProperty IsRecommendedProperty =
        DependencyProperty.Register(nameof(IsRecommended), typeof(bool), typeof(SupportTierCard),
            new PropertyMetadata(false, OnIsRecommendedChanged));

    /// <summary>Raised when the user clicks the support / tip button.</summary>
    public event RoutedEventHandler? SupportClick;

    public SupportTierCard()
    {
        InitializeComponent();
        if (System.Globalization.CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft)
        {
            PopularTag.HorizontalAlignment = HorizontalAlignment.Left;
            PopularTag.Margin = new Thickness(14, 12, 0, 0);
        }

        Loaded += (_, _) => ApplyRecommendedStyle();
    }

    /// <summary>Tier icon image source.</summary>
    public ImageSource? IconPath
    {
        get => (ImageSource?)GetValue(IconPathProperty);
        set => SetValue(IconPathProperty, value);
    }

    /// <summary>Background brush behind the circular icon.</summary>
    public Brush? IconBackground
    {
        get => (Brush?)GetValue(IconBackgroundProperty);
        set => SetValue(IconBackgroundProperty, value);
    }

    /// <summary>Tier title (e.g. Coffee).</summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Short tier description.</summary>
    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <summary>Display price string (e.g. $2.99).</summary>
    public string Price
    {
        get => (string)GetValue(PriceProperty);
        set => SetValue(PriceProperty, value);
    }

    /// <summary>When true, shows the Popular badge and primary button style.</summary>
    public bool IsRecommended
    {
        get => (bool)GetValue(IsRecommendedProperty);
        set => SetValue(IsRecommendedProperty, value);
    }

    /// <summary>Updates Popular badge and button style when recommendation flag changes.</summary>
    private static void OnIsRecommendedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((SupportTierCard)d).ApplyRecommendedStyle();

    /// <summary>Applies recommended vs default visual styling.</summary>
    private void ApplyRecommendedStyle()
    {
        if (PopularTag == null || SupportButton == null)
            return;

        PopularTag.Visibility = IsRecommended ? Visibility.Visible : Visibility.Collapsed;
        SupportButton.Style = (Style)FindResource(
            IsRecommended ? "TierPrimaryButtonStyle" : "TierOutlineButtonStyle");
    }

    /// <summary>Forwards the support button click to <see cref="SupportClick"/>.</summary>
    private void SupportButton_Click(object sender, RoutedEventArgs e) =>
        SupportClick?.Invoke(this, e);

    /// <summary>Hover enter: highlight border and shadow.</summary>
    private void CardRoot_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        AnimateBorderBrush("#BFDBFE");
        AnimateShadow(0.12);
    }

    /// <summary>Hover leave: restore default border and shadow.</summary>
    private void CardRoot_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        AnimateBorderBrush("#E2E8F0");
        AnimateShadow(0);
    }

    /// <summary>Animates card border color over 150ms.</summary>
    private void AnimateBorderBrush(string color)
    {
        var target = (Color)ColorConverter.ConvertFromString(color)!;
        var current = CardRoot.BorderBrush is SolidColorBrush scb ? scb.Color : target;
        var brush = new SolidColorBrush(current);
        CardRoot.BorderBrush = brush;
        brush.BeginAnimation(
            SolidColorBrush.ColorProperty,
            new ColorAnimation(target, TimeSpan.FromMilliseconds(150)));
    }

    /// <summary>Animates drop shadow opacity over 150ms.</summary>
    private void AnimateShadow(double opacity)
    {
        CardShadow.BeginAnimation(
            DropShadowEffect.OpacityProperty,
            new DoubleAnimation(opacity, TimeSpan.FromMilliseconds(150)));
    }
}
