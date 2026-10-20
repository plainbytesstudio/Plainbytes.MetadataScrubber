// SPDX-FileCopyrightText: 2026 PlainBytes Studio
// SPDX-License-Identifier: GPL-3.0-only
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PlainBytes.Core.Catalog;
using PlainBytes.Core.Store;

namespace PlainBytes.Core.Controls;

/// <summary>
/// Wraps a grid of suite product cards; excludes the current tool and marks installed apps.
/// </summary>
public partial class SuiteProductMatrix : UserControl
{
    /// <summary>
    /// Identifies <see cref="CurrentProductId"/>; changing it rebuilds the product list.
    /// </summary>
    public static readonly DependencyProperty CurrentProductIdProperty =
        DependencyProperty.Register(
            nameof(CurrentProductId),
            typeof(string),
            typeof(SuiteProductMatrix),
            new PropertyMetadata(null, OnCurrentProductIdChanged));

    /// <summary>Raised before launching Store or an installed app; passes the clicked catalog entry.</summary>
    public event Action<SuiteProductInfo>? ProductClick;

    public SuiteProductMatrix()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshItems();
    }

    /// <summary>Running product id; omitted from the matrix and used as launch context.</summary>
    public string? CurrentProductId
    {
        get => (string?)GetValue(CurrentProductIdProperty);
        set => SetValue(CurrentProductIdProperty, value);
    }

    /// <summary>Dependency property callback; refreshes cards when the current product changes.</summary>
    private static void OnCurrentProductIdChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SuiteProductMatrix matrix)
            matrix.RefreshItems();
    }

    /// <summary>
    /// Binds <see cref="ProductList"/> from <see cref="SuiteCatalog"/>, excluding <see cref="CurrentProductId"/>.
    /// </summary>
    private void RefreshItems()
    {
        if (ProductList == null)
            return;

        var currentId = CurrentProductId;
        ProductList.ItemsSource = SuiteCatalog.Products
            .Where(p => !string.Equals(p.Id, currentId, StringComparison.OrdinalIgnoreCase))
            .Select(p => new SuiteProductCardItem
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                IconPath = p.IconPath,
                IsInstalled = SuiteAppLauncher.IsInstalled(p),
            })
            .ToList();
    }

    /// <summary>
    /// Handles card click: notifies host, then opens Store or activates an installed sibling app.
    /// </summary>
    private async void ProductCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: SuiteProductCardItem item })
            return;

        var product = SuiteCatalog.FindById(item.Id);
        if (product is null)
            return;

        ProductClick?.Invoke(product);
        await SuiteAppLauncher.OpenAsync(product, CurrentProductId);
    }
}
