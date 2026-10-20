// SPDX-FileCopyrightText: 2026 PlainBytes Studio
// SPDX-License-Identifier: GPL-3.0-only
using PlainBytes.Core.Resources;

namespace PlainBytes.Core.Catalog;

/// <summary>
/// Static registry of PlainBytes Store products (ids, store links, icons, purchase add-ons).
/// </summary>
public static class SuiteCatalog
{
    /// <summary>Fallback pack URI when a product entry has no custom icon.</summary>
    public const string DefaultIconPath =
        "pack://application:,,,/PlainBytes.Core;component/Assets/Images/Application72.png";

    private const string CorePackPrefix = "pack://application:,,,/PlainBytes.Core;component/";

    /// <summary>Builds a pack URI for an image under Core Assets/Images.</summary>
    private static string CoreImage(string fileName) => $"{CorePackPrefix}Assets/Images/{fileName}";

    /// <summary>Builds a pack URI for an icon under Core Assets/Icons.</summary>
    private static string CoreIcon(string fileName) => $"{CorePackPrefix}Assets/Icons/{fileName}";

    private static readonly SuiteProductInfo[] ProductCatalog =
    [
        new(
            id: "MetadataScrubber",
            name: "Metadata Scrubber",
            description: Strings.Catalog_MetadataScrubber_Description,
            packageIdentityName: "PlainBytesStudio.PlainBytes.MetadataScrubber",
            storeProductId: "9NPG7C2ZKHWS",
            iconPath: CoreImage("logo_MetadataScrubber.png"),
            purchaseAddOnStoreId: "9P0KQ7Z4Z1DP",
            purchaseAddOnProductId: "ProUnlock"),
        new(
            id: "ImageConverter",
            name: "Image Converter",
            description: Strings.Catalog_ImageConverter_Description,
            packageIdentityName: "PlainBytesStudio.PlainBytes.ImageConverter",
            storeProductId: "9P4G2JPBQHVK",
            iconPath: CoreImage("logo_ImageConverter.png"),
            purchaseAddOnStoreId: "9N612ZCD4QTF",
            purchaseAddOnProductId: "ProUnlock"),
        new(
            id: "DocumentExtractor",
            name: "Document Extractor",
            description: Strings.Catalog_DocumentExtractor_Description,
            packageIdentityName: "PlainBytesStudio.PlainBytes.DocumentExtractor",
            storeProductId: "9PF07HL4S766",
            iconPath: CoreIcon("logo_DocumentExtractor.ico"),
            purchaseAddOnStoreId: "9NF6QFS2JZ4Z",
            purchaseAddOnProductId: "ProUnlock"),
    ];

    /// <summary>All products in display order.</summary>
    public static IReadOnlyList<SuiteProductInfo> Products => ProductCatalog;

    /// <summary>Finds a product by internal id (case-insensitive); returns null when id is empty or unknown.</summary>
    public static SuiteProductInfo? FindById(string? id) =>
        string.IsNullOrWhiteSpace(id)
            ? null
            : Products.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Products for the About page "More tools" section, excluding the current app and capped at <paramref name="maxCount"/>.
    /// </summary>
    public static IReadOnlyList<SuiteProductInfo> GetAboutMoreTools(string? excludeProductId, int maxCount = 3) =>
        Products
            .Where(p => !string.Equals(p.Id, excludeProductId, StringComparison.OrdinalIgnoreCase))
            .Take(maxCount)
            .ToList();
}
