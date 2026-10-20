// SPDX-FileCopyrightText: 2026 PlainBytes Studio
// SPDX-License-Identifier: GPL-3.0-only
using PlainBytes.Core.Catalog;

namespace PlainBytes.Core.Store;

/// <summary>
/// Factory and one-shot queries for <see cref="ProductEditionService"/> keyed by <see cref="SuiteCatalog"/> product id.
/// </summary>
public static class ProductEditionAccess
{
    /// <summary>
    /// Builds an edition service for a product that has a purchase add-on configured in <see cref="SuiteCatalog"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Unknown product id or missing add-on configuration.</exception>
    public static ProductEditionService CreateService(string productId, StorePurchaseService storePurchase)
    {
        var product = RequireProduct(productId);
        if (!product.HasPurchaseAddOn)
            throw new InvalidOperationException($"Product '{productId}' has no purchase add-on configured.");

        return new ProductEditionService(
            storePurchase,
            product.TrialCredentialTarget,
            product.PurchaseAddOnStoreId,
            product.PurchaseAddOnProductId,
            product.TrialDays);
    }

    /// <summary>
    /// Resolves current edition for CLI and headless checks.
    /// When no add-on is configured, returns <see cref="ProductEditionKind.Purchased"/> so development builds are not gated.
    /// </summary>
    public static async Task<ProductEditionSnapshot> QueryAsync(string productId)
    {
        var product = SuiteCatalog.FindById(productId);
        if (product is null || !product.HasPurchaseAddOn)
        {
            return new ProductEditionSnapshot { Kind = ProductEditionKind.Purchased };
        }

        var store = new StorePurchaseService();
        return await CreateService(productId, store).QueryAsync().ConfigureAwait(false);
    }

    /// <summary>True when trial is active or the add-on is owned.</summary>
    public static bool IsFullFeatureAccess(ProductEditionSnapshot edition) =>
        edition.IsFullFeatureAccess;

    /// <summary>Resolves a catalog entry or throws when the id is unknown.</summary>
    private static SuiteProductInfo RequireProduct(string productId) =>
        SuiteCatalog.FindById(productId) ?? throw new InvalidOperationException($"Unknown product id '{productId}'.");
}
