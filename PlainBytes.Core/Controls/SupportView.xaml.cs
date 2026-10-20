// SPDX-FileCopyrightText: 2026 PlainBytes Studio
// SPDX-License-Identifier: GPL-3.0-only
using System.Windows;
using System.Windows.Controls;
using PlainBytes.Core.Support;

namespace PlainBytes.Core.Controls;

/// <summary>
/// Shell page listing support tiers; hosts <see cref="SupportTierCard"/> and page chrome.
/// </summary>
public partial class SupportView : UserControl
{
    /// <summary>Identifies <see cref="IsSupporter"/>.</summary>
    public static readonly DependencyProperty IsSupporterProperty =
        DependencyProperty.Register(
            nameof(IsSupporter),
            typeof(bool),
            typeof(SupportView),
            new PropertyMetadata(false, OnIsSupporterChanged));

    /// <summary>When true, shows the Supporter status card.</summary>
    public SupportView()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshSupporterState();
    }

    /// <summary>True when the user has supported the app.</summary>
    public bool IsSupporter
    {
        get => (bool)GetValue(IsSupporterProperty);
        set => SetValue(IsSupporterProperty, value);
    }

    /// <summary>Refreshes the Supporter status card when the user state changes.</summary>
    public void RefreshSupporterState()
    {
        IsSupporter = SupporterState.IsUnlocked;
    }

    /// <summary>Dependency property callback; shows/hides the Supporter status card.</summary>
    private static void OnIsSupporterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SupportView view)
            view.SupporterStatusCard.Visibility =
                (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
    }
}
