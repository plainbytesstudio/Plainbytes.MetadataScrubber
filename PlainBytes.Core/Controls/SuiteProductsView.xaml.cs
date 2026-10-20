// SPDX-FileCopyrightText: 2026 PlainBytes Studio
// SPDX-License-Identifier: GPL-3.0-only
using System.Windows;
using System.Windows.Controls;

namespace PlainBytes.Core.Controls;

/// <summary>
/// Shell page listing other PlainBytes tools; hosts <see cref="SuiteProductMatrix"/> with page chrome.
/// </summary>
public partial class SuiteProductsView : UserControl
{
    /// <summary>
    /// Identifies <see cref="CurrentProductId"/>; forwarded to the embedded product matrix.
    /// </summary>
    public static readonly DependencyProperty CurrentProductIdProperty =
        DependencyProperty.Register(
            nameof(CurrentProductId),
            typeof(string),
            typeof(SuiteProductsView),
            new PropertyMetadata(null, OnCurrentProductIdChanged));

    public SuiteProductsView()
    {
        InitializeComponent();
        Loaded += (_, _) => ProductMatrix.CurrentProductId = CurrentProductId;
    }

    /// <summary>Running product id; excluded from the matrix and passed to launch logic.</summary>
    public string? CurrentProductId
    {
        get => (string?)GetValue(CurrentProductIdProperty);
        set => SetValue(CurrentProductIdProperty, value);
    }

    /// <summary>Syncs <see cref="CurrentProductId"/> to the nested <see cref="SuiteProductMatrix"/>.</summary>
    private static void OnCurrentProductIdChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SuiteProductsView view && view.ProductMatrix != null)
            view.ProductMatrix.CurrentProductId = e.NewValue as string;
    }
}
