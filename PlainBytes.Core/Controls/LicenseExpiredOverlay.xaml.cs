// SPDX-FileCopyrightText: 2026 PlainBytes Studio
// SPDX-License-Identifier: GPL-3.0-only
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using PlainBytes.Core.Resources;
using PlainBytes.Core.Store;
using Windows.System;

namespace PlainBytes.Core.Controls;

/// <summary>
/// Full-window modal shown when the local trial has ended; blocks use until purchase or exit.
/// </summary>
public partial class LicenseExpiredOverlay : UserControl
{
    /// <summary>Raised when the user chooses to close the application.</summary>
    public event Action? CloseRequested;

    /// <summary>Raised when the user chooses to purchase (host should call Store in-app purchase).</summary>
    public event Action? PurchaseRequested;

    public LicenseExpiredOverlay()
    {
        InitializeComponent();
        RefreshStrings();
    }

    /// <summary>Applies localized title, message, and default button labels.</summary>
    public void RefreshStrings()
    {
        TitleText.Text = Strings.License_ExpiredTitle;
        MessageText.Text = Strings.License_ExpiredMessage;
        CloseButton.Content = Strings.License_CloseApp;
        UpdatePurchaseButtonText(null);
    }

    /// <summary>
    /// Sets the primary purchase button text, optionally including a localized Store price.
    /// </summary>
    /// <param name="formattedPrice">Store <c>FormattedPrice</c>, or null for a price-less label.</param>
    public void UpdatePurchaseButtonText(string? formattedPrice) =>
        PurchaseButton.Content = ProductEditionService.FormatPurchaseButtonText(formattedPrice);

    /// <summary>Closes the app via <see cref="CloseRequested"/>.</summary>
    private void CloseButton_Click(object sender, RoutedEventArgs e) =>
        CloseRequested?.Invoke();

    /// <summary>Starts purchase flow via <see cref="PurchaseRequested"/>.</summary>
    private void PurchaseButton_Click(object sender, RoutedEventArgs e) =>
        PurchaseRequested?.Invoke();

    /// <summary>
    /// Opens the Microsoft Store product page for the given listing id (fallback for headless/CLI scenarios).
    /// </summary>
    /// <param name="storeProductId">Store Product Id (e.g. main app listing id).</param>
    /// <remarks>Prefer in-app add-on purchase in GUI; this does not complete an add-on transaction.</remarks>
    public static async Task OpenStoreProductPageAsync(string? storeProductId)
    {
        if (string.IsNullOrWhiteSpace(storeProductId))
            return;

        var uri = new Uri($"ms-windows-store://pdp/?ProductId={storeProductId.Trim()}");
        try
        {
            if (await Launcher.LaunchUriAsync(uri))
                return;
        }
        catch
        {
            // fall through
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = uri.AbsoluteUri,
                UseShellExecute = true,
            });
        }
        catch
        {
            // ignore
        }
    }
}
