// SPDX-FileCopyrightText: 2026 PlainBytes Studio
// SPDX-License-Identifier: GPL-3.0-only
using System.Windows;
using System.Windows.Threading;
using PlainBytes.Core.Catalog;
using PlainBytes.Core.Controls;

namespace PlainBytes.Core.Store;

/// <summary>
/// Shell-integrated edition UI for add-on purchase and local trial: title badge, overlays, and purchase flow.
/// </summary>
public sealed class ProductEditionUiHost
{
    private readonly ShellWindow _shell;
    private readonly ProductEditionService _editionService;
    private readonly StorePurchaseService _storePurchase;
    private readonly Action _closeApp;
    private readonly Func<string?> _getToolName;

    private LicenseExpiredOverlay? _licenseOverlay;
    private PurchasePromptOverlay? _purchasePromptOverlay;
    private bool _editionUiBusy;
    private string? _cachedPurchasePrice;

    /// <summary>
    /// Creates a host when <paramref name="productId"/> has a purchase add-on in <see cref="SuiteCatalog"/>; otherwise null.
    /// </summary>
    public static ProductEditionUiHost? TryCreate(
        ShellWindow shell,
        string productId,
        Action closeApp,
        Func<string?> getToolName)
    {
        var product = SuiteCatalog.FindById(productId);
        if (product is null || !product.HasPurchaseAddOn)
            return null;

        return new ProductEditionUiHost(shell, productId, closeApp, getToolName);
    }

    private ProductEditionUiHost(
        ShellWindow shell,
        string productId,
        Action closeApp,
        Func<string?> getToolName)
    {
        _shell = shell;
        _closeApp = closeApp;
        _getToolName = getToolName;
        _storePurchase = new StorePurchaseService();
        _editionService = ProductEditionAccess.CreateService(productId, _storePurchase);
        _editionService.EditionChanged += OnEditionChanged;
    }

    /// <summary>Edition state mirrored from the last shell refresh.</summary>
    public ProductEditionSnapshot CurrentEdition { get; private set; } = new()
    {
        Kind = ProductEditionKind.Purchased,
    };

    /// <summary>Associates the Store context with the shell window HWND.</summary>
    public void AttachWindow() => _storePurchase.AttachWindow(_shell);

    /// <summary>Initial refresh and background price fetch.</summary>
    public async Task InitializeAsync()
    {
        if (_editionUiBusy)
            return;

        _editionUiBusy = true;
        try
        {
            var edition = await Task.Run(() => _editionService.RefreshAsync(notify: false))
                .ConfigureAwait(true);
            ApplyEditionToShell(edition);
        }
        finally
        {
            _editionUiBusy = false;
        }

        _ = RefreshPurchasePriceAsync();
    }

    /// <summary>Handles title-bar trial badge click: purchase prompt, purchased info, or expired overlay.</summary>
    public void ShowEditionBadgePrompt()
    {
        if (CurrentEdition.Kind == ProductEditionKind.Purchased)
        {
            ShowPurchasedInfoOverlay();
            return;
        }

        if (CurrentEdition.Kind == ProductEditionKind.Trial)
            _ = ShowTrialPurchasePromptAsync();
        else
            ShowLicenseExpiredOverlay();
    }

    /// <summary>Re-applies badge and overlay state without re-querying Store.</summary>
    public void ApplyCurrentEditionToShell() => ApplyEditionToShell(CurrentEdition);

    /// <summary>Updates overlay copy after locale change and refreshes cached price on buttons.</summary>
    public void RefreshOverlayStrings()
    {
        _licenseOverlay?.RefreshStrings();
        _licenseOverlay?.UpdatePurchaseButtonText(_cachedPurchasePrice);
        _purchasePromptOverlay?.RefreshStrings(_cachedPurchasePrice);
    }

    /// <summary>Unsubscribes edition events; call when the shell is closing.</summary>
    public void Detach()
    {
        _editionService.EditionChanged -= OnEditionChanged;
    }

    /// <summary>Marshals edition refresh to the shell dispatcher.</summary>
    private void OnEditionChanged()
    {
        if (_shell.Dispatcher.CheckAccess())
            _ = RefreshAsync();
        else
            _shell.Dispatcher.Invoke(() => _ = RefreshAsync());
    }

    /// <summary>Queries edition service and updates shell chrome; coalesces concurrent refreshes.</summary>
    private async Task RefreshAsync()
    {
        if (_editionUiBusy)
            return;

        _editionUiBusy = true;
        try
        {
            var edition = await _editionService.RefreshAsync(notify: false).ConfigureAwait(true);
            ApplyEditionToShell(edition);
        }
        finally
        {
            _editionUiBusy = false;
        }
    }

    /// <summary>Fetches Store price and pushes it to any visible overlays.</summary>
    private async Task RefreshPurchasePriceAsync()
    {
        _cachedPurchasePrice = await Task.Run(() => _editionService.GetPurchaseFormattedPriceAsync())
            .ConfigureAwait(true);
        _licenseOverlay?.UpdatePurchaseButtonText(_cachedPurchasePrice);
        _purchasePromptOverlay?.RefreshStrings(_cachedPurchasePrice);
    }

    /// <summary>Syncs title suffix, tool name, and overlay visibility to edition kind.</summary>
    private void ApplyEditionToShell(ProductEditionSnapshot edition)
    {
        CurrentEdition = edition;
        var toolName = _getToolName();
        if (!string.IsNullOrWhiteSpace(toolName))
            _shell.SetToolName(toolName);

        switch (edition.Kind)
        {
            case ProductEditionKind.Purchased:
                _shell.SetTitleSuffix(null);
                HideEditionOverlays();
                break;
            case ProductEditionKind.Trial:
                _shell.SetTitleSuffix(
                    ProductEditionService.FormatTrialTitleSuffix(edition),
                    badgeClickable: true,
                    onBadgeClick: ShowEditionBadgePrompt);
                HideEditionOverlays();
                break;
            case ProductEditionKind.TrialExpired:
                _shell.SetTitleSuffix(null);
                ShowLicenseExpiredOverlay();
                break;
        }
    }

    /// <summary>Shows optional purchase dialog during active trial.</summary>
    private async Task ShowTrialPurchasePromptAsync()
    {
        _purchasePromptOverlay ??= CreatePurchasePromptOverlay();
        _purchasePromptOverlay.ConfigureTrialPrompt(CurrentEdition.TrialDaysRemaining, _cachedPurchasePrice);
        if (string.IsNullOrWhiteSpace(_cachedPurchasePrice))
            _cachedPurchasePrice = await _editionService.GetPurchaseFormattedPriceAsync().ConfigureAwait(true);

        _purchasePromptOverlay.RefreshStrings(_cachedPurchasePrice);
        _shell.ShowOverlay(_purchasePromptOverlay);
    }

    /// <summary>Shows thank-you / already-owned info when user taps badge while purchased.</summary>
    private void ShowPurchasedInfoOverlay()
    {
        _purchasePromptOverlay ??= CreatePurchasePromptOverlay();
        _purchasePromptOverlay.ConfigurePurchasedInfo();
        _shell.ShowOverlay(_purchasePromptOverlay);
    }

    /// <summary>Shows full-screen overlay when trial has expired.</summary>
    private void ShowLicenseExpiredOverlay()
    {
        _licenseOverlay ??= CreateLicenseOverlay();
        _licenseOverlay.RefreshStrings();
        _licenseOverlay.UpdatePurchaseButtonText(_cachedPurchasePrice);
        _shell.ShowOverlay(_licenseOverlay);
    }

    /// <summary>Dismisses edition overlays without changing edition state.</summary>
    private void HideEditionOverlays() => _shell.HideOverlay();

    /// <summary>Creates trial/purchased prompt overlay with shared purchase handlers.</summary>
    private PurchasePromptOverlay CreatePurchasePromptOverlay()
    {
        var overlay = new PurchasePromptOverlay();
        overlay.ContinueTrialRequested += () => _shell.HideOverlay();
        overlay.PurchaseRequested += () => _ = RunPurchaseFlowAsync(closeOverlayOnSuccess: true);
        return overlay;
    }

    /// <summary>Creates expired overlay wired to app close and purchase.</summary>
    private LicenseExpiredOverlay CreateLicenseOverlay()
    {
        var overlay = new LicenseExpiredOverlay();
        overlay.CloseRequested += _closeApp;
        overlay.PurchaseRequested += () => _ = RunPurchaseFlowAsync(closeOverlayOnSuccess: true);
        return overlay;
    }

    /// <summary>Runs Store purchase, refreshes edition, and optionally hides overlay on success.</summary>
    private async Task RunPurchaseFlowAsync(bool closeOverlayOnSuccess)
    {
        var result = await _editionService.RequestPurchaseAsync().ConfigureAwait(true);
        if (result.Status is AppStorePurchaseStatus.Succeeded or AppStorePurchaseStatus.AlreadyPurchased)
        {
            _editionService.MarkPurchaseCompletedThisSession();
            await RefreshAsync().ConfigureAwait(true);
            if (closeOverlayOnSuccess)
                HideEditionOverlays();
            return;
        }

        await RefreshAsync().ConfigureAwait(true);
    }
}
