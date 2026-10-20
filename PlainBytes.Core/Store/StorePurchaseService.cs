// SPDX-FileCopyrightText: 2026 PlainBytes Studio
// SPDX-License-Identifier: GPL-3.0-only
using System.Windows;
using System.Windows.Interop;
using Windows.Services.Store;
using WinRT.Interop;

namespace PlainBytes.Core.Store;

/// <summary>
/// Microsoft Store durable add-on: license checks, in-app purchase, and localized pricing.
/// </summary>
public sealed class StorePurchaseService
{
    private StoreContext? _context;
    private bool _listening;
    private bool _purchaseCompletedThisSession;

    /// <summary>
    /// Raised when offline license state may have changed (e.g. after a Store sync).
    /// </summary>
    public event Action? LicenseChanged;

    /// <summary>
    /// True when a purchase succeeded in this process session, used to avoid Store API lag in the UI.
    /// </summary>
    public bool PurchaseCompletedThisSession => _purchaseCompletedThisSession;

    /// <summary>
    /// Binds <see cref="StoreContext"/> to the host window HWND (required for Desktop Bridge purchase UI)
    /// and subscribes to <see cref="StoreContext.OfflineLicensesChanged"/>.
    /// </summary>
    /// <param name="window">Main shell window shown to the user.</param>
    public void AttachWindow(Window window)
    {
        _context ??= StoreContext.GetDefault();

        try
        {
            var hwnd = new WindowInteropHelper(window).EnsureHandle();
            InitializeWithWindow.Initialize(_context, hwnd);
        }
        catch
        {
            // Cached license reads may still work without an HWND.
        }

        if (_listening)
            return;

        _listening = true;
        _context.OfflineLicensesChanged += (_, _) => LicenseChanged?.Invoke();
    }

    /// <summary>
    /// Marks the current session as purchased so <see cref="IsAddOnActiveAsync"/> returns true immediately,
    /// before Store license refresh completes.
    /// </summary>
    public void MarkPurchaseCompletedThisSession() =>
        _purchaseCompletedThisSession = true;

    /// <summary>
    /// Returns whether the durable add-on license is active for the current user.
    /// </summary>
    /// <param name="addOnStoreId">Store Product Id of the add-on (e.g. 9P0KQ7Z4Z1DP).</param>
    /// <param name="addOnProductId">Optional in-app product id (e.g. ProUnlock) used as an alternate license key.</param>
    /// <returns>True if purchased or <see cref="PurchaseCompletedThisSession"/> is set; otherwise false.</returns>
    public async Task<bool> IsAddOnActiveAsync(string addOnStoreId, string? addOnProductId = null)
    {
        if (_purchaseCompletedThisSession)
            return true;

        if (!AppPackageInfo.IsRunningAsPackagedApp())
            return false;

        try
        {
            _context ??= StoreContext.GetDefault();
            var license = await _context.GetAppLicenseAsync().AsTask().ConfigureAwait(false);
            if (license is null)
                return false;

            foreach (var pair in license.AddOnLicenses)
            {
                if (!pair.Value.IsActive)
                    continue;

                if (MatchesAddOnKey(pair.Key, addOnStoreId, addOnProductId))
                    return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    /// <summary>
    /// Gets the localized purchase price string for the add-on in the user's current Store market.
    /// </summary>
    /// <param name="addOnStoreId">Store Product Id of the durable add-on.</param>
    /// <returns>
    /// <see cref="Windows.Services.Store.StorePrice.FormattedPrice"/> for the first SKU,
    /// or null when not packaged or the Store call fails.
    /// </returns>
    public async Task<string?> GetAddOnFormattedPriceAsync(string addOnStoreId)
    {
        if (!AppPackageInfo.IsRunningAsPackagedApp())
            return null;

        try
        {
            _context ??= StoreContext.GetDefault();
            var result = await _context.GetStoreProductsAsync(["Durable"], [addOnStoreId])
                .AsTask()
                .ConfigureAwait(false);
            if (result.ExtendedError != null)
                return null;

            if (!result.Products.TryGetValue(addOnStoreId, out var product))
                return null;

            return product.Skus.FirstOrDefault()?.Price.FormattedPrice;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Launches the system Store purchase dialog for the durable add-on.
    /// </summary>
    /// <param name="addOnStoreId">Store Product Id passed to <see cref="StoreContext.RequestPurchaseAsync"/>.</param>
    /// <returns>Purchase outcome; sets <see cref="PurchaseCompletedThisSession"/> on success or already owned.</returns>
    public async Task<AppStorePurchaseResult> RequestAddOnPurchaseAsync(string addOnStoreId)
    {
        if (!AppPackageInfo.IsRunningAsPackagedApp())
        {
            return new AppStorePurchaseResult
            {
                Status = AppStorePurchaseStatus.NotPurchased,
            };
        }

        try
        {
            _context ??= StoreContext.GetDefault();
            var result = await _context.RequestPurchaseAsync(addOnStoreId);
            var status = MapPurchaseStatus(result.Status);
            if (status is AppStorePurchaseStatus.Succeeded or AppStorePurchaseStatus.AlreadyPurchased)
                _purchaseCompletedThisSession = true;

            return new AppStorePurchaseResult { Status = status };
        }
        catch
        {
            return new AppStorePurchaseResult { Status = AppStorePurchaseStatus.Unknown };
        }
    }

    /// <summary>
    /// Matches a license dictionary key against Store Id or optional in-app product id.
    /// </summary>
    private static bool MatchesAddOnKey(string key, string addOnStoreId, string? addOnProductId)
    {
        if (string.Equals(key, addOnStoreId, StringComparison.OrdinalIgnoreCase))
            return true;

        return !string.IsNullOrWhiteSpace(addOnProductId)
               && string.Equals(key, addOnProductId, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Maps WinRT <see cref="StorePurchaseStatus"/> to the app-local purchase status enum.
    /// </summary>
    private static AppStorePurchaseStatus MapPurchaseStatus(StorePurchaseStatus status) =>
        status switch
        {
            StorePurchaseStatus.Succeeded => AppStorePurchaseStatus.Succeeded,
            StorePurchaseStatus.AlreadyPurchased => AppStorePurchaseStatus.AlreadyPurchased,
            StorePurchaseStatus.NotPurchased => AppStorePurchaseStatus.NotPurchased,
            StorePurchaseStatus.NetworkError => AppStorePurchaseStatus.NetworkError,
            StorePurchaseStatus.ServerError => AppStorePurchaseStatus.ServerError,
            _ => AppStorePurchaseStatus.Unknown,
        };
}
