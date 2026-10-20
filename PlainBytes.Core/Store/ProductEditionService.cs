// SPDX-FileCopyrightText: 2026 PlainBytes Studio
// SPDX-License-Identifier: GPL-3.0-only
using PlainBytes.Core.Resources;

namespace PlainBytes.Core.Store;

/// <summary>
/// Resolves trial vs purchased state for one add-on: Store license, local trial guard, purchase UI helpers.
/// </summary>
public sealed class ProductEditionService
{
    /// <summary>Default local trial length in calendar days when not overridden in catalog.</summary>
    public const int DefaultTrialDays = 7;

    private readonly StorePurchaseService _storePurchase;
    private readonly string _trialCredentialTarget;
    private readonly int _trialDays;
    private readonly string _addOnStoreId;
    private readonly string? _addOnProductId;

    /// <summary>Raised after <see cref="RefreshAsync"/> updates <see cref="Current"/>.</summary>
    public event Action? EditionChanged;

    /// <summary>Last resolved edition; updated by <see cref="RefreshAsync"/>.</summary>
    public ProductEditionSnapshot Current { get; private set; } = new()
    {
        Kind = ProductEditionKind.Purchased,
    };

    /// <summary>Wires Store license changes to automatic refresh.</summary>
    public ProductEditionService(
        StorePurchaseService storePurchase,
        string trialCredentialTarget,
        string addOnStoreId,
        string? addOnProductId = null,
        int trialDays = DefaultTrialDays)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trialCredentialTarget);
        if (string.IsNullOrWhiteSpace(addOnStoreId) && string.IsNullOrWhiteSpace(addOnProductId))
            throw new ArgumentException("At least one of add-on Store id or product id is required.");

        _storePurchase = storePurchase;
        _trialCredentialTarget = trialCredentialTarget;
        _addOnStoreId = addOnStoreId ?? string.Empty;
        _addOnProductId = addOnProductId;
        _trialDays = trialDays;
        _storePurchase.LicenseChanged += OnStoreLicenseChanged;
    }

    /// <summary>Re-queries edition state, updates <see cref="Current"/>, and optionally raises <see cref="EditionChanged"/>.</summary>
    public async Task<ProductEditionSnapshot> RefreshAsync(bool notify = true)
    {
        Current = await QueryAsync().ConfigureAwait(true);
        if (notify)
            EditionChanged?.Invoke();
        return Current;
    }

    /// <summary>
    /// Determines edition without mutating <see cref="Current"/>.
    /// Unpackaged runs and active add-on licenses yield <see cref="ProductEditionKind.Purchased"/>.
    /// </summary>
    public async Task<ProductEditionSnapshot> QueryAsync()
    {
        if (!AppPackageInfo.IsRunningAsPackagedApp())
        {
            return new ProductEditionSnapshot
            {
                Kind = ProductEditionKind.Purchased
            };
        }

        if (await _storePurchase.IsAddOnActiveAsync(_addOnStoreId, _addOnProductId).ConfigureAwait(false))
        {
            return new ProductEditionSnapshot
            {
                Kind = ProductEditionKind.Purchased,
            };
        }

        LocalTrialGuard.EnsureFirstRunUtc(_trialCredentialTarget);
        var daysRemaining = LocalTrialGuard.GetTrialDaysRemaining(_trialCredentialTarget, _trialDays);
        if (daysRemaining > 0)
        {
            return new ProductEditionSnapshot
            {
                Kind = ProductEditionKind.Trial,
                TrialDaysRemaining = daysRemaining,
            };
        }

        return new ProductEditionSnapshot
        {
            Kind = ProductEditionKind.TrialExpired,
            TrialDaysRemaining = 0,
        };
    }

    /// <summary>Localized Store price string for the purchase button, if available.</summary>
    public Task<string?> GetPurchaseFormattedPriceAsync()
    {
        if (string.IsNullOrWhiteSpace(_addOnStoreId))
            return Task.FromResult<string?>(null);

        return _storePurchase.GetAddOnFormattedPriceAsync(_addOnStoreId);
    }

    /// <summary>Opens the system Store purchase dialog for this add-on.</summary>
    public Task<AppStorePurchaseResult> RequestPurchaseAsync()
    {
        if (string.IsNullOrWhiteSpace(_addOnStoreId))
        {
            return Task.FromResult(new AppStorePurchaseResult
            {
                Status = AppStorePurchaseStatus.NotPurchased,
            });
        }

        return _storePurchase.RequestAddOnPurchaseAsync(_addOnStoreId);
    }

    /// <summary>Flags a successful purchase this session (e.g. after external confirmation).</summary>
    public void MarkPurchaseCompletedThisSession() =>
        _storePurchase.MarkPurchaseCompletedThisSession();

    /// <summary>Title-bar trial badge text during <see cref="ProductEditionKind.Trial"/>; null otherwise.</summary>
    public static string? FormatTrialTitleSuffix(ProductEditionSnapshot edition)
    {
        if (edition.Kind != ProductEditionKind.Trial)
            return null;

        if (edition.TrialDaysRemaining is not int days)
            return Strings.License_TrialTitleSuffix;

        if (days <= 0)
            return Strings.License_TrialTitleSuffixLessThanDay;

        if (days == 1)
            return Strings.Format(Strings.License_TrialTitleSuffixDay, 1);

        return Strings.Format(Strings.License_TrialTitleSuffixDays, days);
    }

    /// <summary>Purchase button label with optional formatted price.</summary>
    public static string FormatPurchaseButtonText(string? formattedPrice)
    {
        if (string.IsNullOrWhiteSpace(formattedPrice))
            return Strings.License_BuyNow;

        return Strings.Format(Strings.License_BuyNowWithPrice, formattedPrice.Trim());
    }

    /// <summary>Refreshes edition when Store reports a license change.</summary>
    private void OnStoreLicenseChanged() =>
        _ = RefreshAsync();
}
