// SPDX-FileCopyrightText: 2026 PlainBytes Studio
// SPDX-License-Identifier: GPL-3.0-only
using System.Windows;
using System.Windows.Controls;
using PlainBytes.Core.Resources;
using PlainBytes.Core.Store;

namespace PlainBytes.Core.Controls;

/// <summary>
/// Modal overlay shown during trial (purchase prompt) or after purchase (licensed info).
/// </summary>
public partial class PurchasePromptOverlay : UserControl
{
    /// <summary>Raised when the user chooses to continue the trial or dismiss the licensed-info dialog.</summary>
    public event Action? ContinueTrialRequested;

    /// <summary>Raised when the user taps the primary purchase button during trial.</summary>
    public event Action? PurchaseRequested;

    private bool _showContinueTrial;
    private int? _trialDaysRemaining;

    public PurchasePromptOverlay()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Configures the overlay for an active trial: purchase + continue trial actions.
    /// </summary>
    /// <param name="trialDaysRemaining">Days left in the trial; used for the secondary button label.</param>
    /// <param name="formattedPrice">Localized Store price for the primary button, or null to omit amount.</param>
    public void ConfigureTrialPrompt(int? trialDaysRemaining, string? formattedPrice)
    {
        _showContinueTrial = true;
        _trialDaysRemaining = trialDaysRemaining;
        RefreshStrings(formattedPrice);
    }

    /// <summary>
    /// Configures the overlay for licensed users (informational only, single dismiss button).
    /// </summary>
    public void ConfigurePurchasedInfo()
    {
        _showContinueTrial = false;
        _trialDaysRemaining = null;
        TitleText.Text = Strings.License_PurchasedTitle;
        MessageText.Text = Strings.License_PurchasedMessage;
        SecondaryButton.Visibility = Visibility.Collapsed;
        PurchaseButton.Content = Strings.License_GotIt;
    }

    /// <summary>
    /// Refreshes localized copy for the trial-purchase layout.
    /// </summary>
    /// <param name="formattedPrice">Updated Store price string for the purchase button.</param>
    /// <remarks>No-op when the overlay is in purchased-info mode.</remarks>
    public void RefreshStrings(string? formattedPrice = null)
    {
        if (!_showContinueTrial)
            return;

        TitleText.Text = Strings.License_PurchasePromptTitle;
        MessageText.Text = Strings.License_PurchasePromptMessage;
        PurchaseButton.Content = ProductEditionService.FormatPurchaseButtonText(formattedPrice);

        if (_trialDaysRemaining is int days and > 0)
        {
            SecondaryButton.Visibility = Visibility.Visible;
            SecondaryButton.Content = days == 1
                ? Strings.Format(Strings.License_ContinueTrialDay, 1)
                : Strings.Format(Strings.License_ContinueTrialDays, days);
        }
        else
        {
            SecondaryButton.Visibility = Visibility.Visible;
            SecondaryButton.Content = Strings.License_ContinueTrial;
        }
    }

    /// <summary>Secondary action: continue trial or dismiss licensed-info dialog.</summary>
    private void SecondaryButton_Click(object sender, RoutedEventArgs e) =>
        ContinueTrialRequested?.Invoke();

    /// <summary>
    /// Primary action: start purchase during trial, or dismiss when showing licensed info.
    /// </summary>
    private void PurchaseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_showContinueTrial)
            PurchaseRequested?.Invoke();
        else
            ContinueTrialRequested?.Invoke();
    }
}
