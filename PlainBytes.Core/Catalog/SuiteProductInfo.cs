// SPDX-FileCopyrightText: 2026 PlainBytes Studio
// SPDX-License-Identifier: GPL-3.0-only
using PlainBytes.Core.Store;

namespace PlainBytes.Core.Catalog;

/// <summary>
/// Immutable metadata for one PlainBytes Store product (catalog, licensing, and launch identity).
/// </summary>
public sealed class SuiteProductInfo
{
    /// <summary>Creates a catalog entry; optional fields fall back to suite-wide defaults.</summary>
    public SuiteProductInfo(
        string id,
        string name,
        string description,
        string packageIdentityName,
        string storeProductId,
        string? iconPath = null,
        string? purchaseAddOnStoreId = null,
        string? purchaseAddOnProductId = null,
        string? trialCredentialTarget = null,
        int trialDays = ProductEditionService.DefaultTrialDays,
        string applicationId = "App",
        string? packageFamilyName = null)
    {
        Id = id;
        Name = name;
        Description = description;
        PackageIdentityName = packageIdentityName;
        StoreProductId = storeProductId;
        IconPath = iconPath ?? SuiteCatalog.DefaultIconPath;
        PurchaseAddOnStoreId = purchaseAddOnStoreId ?? string.Empty;
        PurchaseAddOnProductId = purchaseAddOnProductId ?? string.Empty;
        TrialCredentialTarget = trialCredentialTarget ?? $"PlainBytes.{id}.TrialGuard";
        TrialDays = trialDays;
        ApplicationId = string.IsNullOrWhiteSpace(applicationId) ? "App" : applicationId;
        PackageFamilyName = packageFamilyName ?? string.Empty;
    }

    /// <summary>Internal id for matrix/navigation (e.g. ImageConverter).</summary>
    public string Id { get; }

    /// <summary>User-facing product name.</summary>
    public string Name { get; }

    /// <summary>Short marketing description for catalog cards.</summary>
    public string Description { get; }

    /// <summary>Pack URI for title bar and About page artwork.</summary>
    public string IconPath { get; }

    /// <summary>Appx identity name (sideload and Store); used to detect local installation.</summary>
    public string PackageIdentityName { get; }

    /// <summary>Microsoft Store product id (e.g. 9NPG7C2ZKHWS) for opening the store listing.</summary>
    public string StoreProductId { get; }

    /// <summary>Durable add-on Store id used for in-app purchase.</summary>
    public string PurchaseAddOnStoreId { get; }

    /// <summary>Partner Center product id (e.g. ProUnlock); fallback key in license dictionaries.</summary>
    public string PurchaseAddOnProductId { get; }

    /// <summary>Credential Manager target recording local trial start time.</summary>
    public string TrialCredentialTarget { get; }

    /// <summary>Local trial length in calendar days before purchase is required.</summary>
    public int TrialDays { get; }

    /// <summary>True when a purchase add-on Store id or Partner Center product id is configured.</summary>
    public bool HasPurchaseAddOn =>
        !string.IsNullOrWhiteSpace(PurchaseAddOnStoreId)
        || !string.IsNullOrWhiteSpace(PurchaseAddOnProductId);

    /// <summary>Manifest application id (default App); combined with PFN to form AUMID.</summary>
    public string ApplicationId { get; }

    /// <summary>
    /// Optional known package family name (Store-signed).
    /// When empty, resolved from locally installed packages at launch.
    /// </summary>
    public string PackageFamilyName { get; }

    /// <summary>True when a Store listing id is configured.</summary>
    public bool HasStoreProduct => !string.IsNullOrWhiteSpace(StoreProductId);
}
