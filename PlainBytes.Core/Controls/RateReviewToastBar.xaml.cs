// SPDX-FileCopyrightText: 2026 PlainBytes Studio
// SPDX-License-Identifier: GPL-3.0-only
using System.Windows;
using System.Windows.Controls;
using PlainBytes.Core.Store;

namespace PlainBytes.Core.Controls;

/// <summary>
/// Top-of-window toast bar that prompts the user to rate the app on Microsoft Store.
/// </summary>
public partial class RateReviewToastBar : UserControl
{
    private RateReviewPromptState? _state;

    public RateReviewToastBar()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Displays the rate-and-review bar and binds it to the given prompt state.
    /// </summary>
    /// <param name="state">Per-product counters used for "never show again" and streak reset.</param>
    /// <remarks>Closing via the X button resets the consecutive success streak.</remarks>
    public void Show(RateReviewPromptState state)
    {
        _state = state;
        Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Hides this bar and clears the host <see cref="ShellWindow"/> top toast slot.
    /// </summary>
    public void Hide()
    {
        Visibility = Visibility.Collapsed;
        if (Window.GetWindow(this) is ShellWindow shell)
            shell.HideTopToast();
    }

    /// <summary>
    /// Opens the Store rate-and-review dialog; suppresses future prompts on success.
    /// </summary>
    private async void Rate_Click(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this);
        var result = await StoreRating.RequestRateAndReviewAppAsync(owner);
        if (result.Status == StoreRateAndReviewStatus.Success)
            _state?.MarkNeverShowAgain();

        Hide();
    }

    /// <summary>
    /// Permanently dismisses the prompt via "never show again".
    /// </summary>
    private void Never_Click(object sender, RoutedEventArgs e)
    {
        _state?.MarkNeverShowAgain();
        Hide();
    }

    /// <summary>
    /// Dismisses without opting out; resets the consecutive success streak.
    /// </summary>
    private void Close_Click(object sender, RoutedEventArgs e)
    {
        _state?.ResetConsecutiveCount();
        Hide();
    }
}
