// SPDX-FileCopyrightText: 2026 PlainBytes Studio
// SPDX-License-Identifier: GPL-3.0-only
using System.Windows;
using System.Windows.Interop;
using Windows.Services.Store;
using WinRT.Interop;

namespace PlainBytes.Core.Store;

/// <summary>
/// Invokes the system Microsoft Store rate-and-review UI for the running app.
/// </summary>
public static class StoreRating
{
    /// <summary>
    /// Shows the Store rating and review dialog for the current packaged application.
    /// </summary>
    /// <param name="owner">
    /// Host window used to initialize <see cref="StoreContext"/> with an HWND.
    /// Falls back to <see cref="Application.Current"/> main window when null.
    /// </param>
    /// <returns>
    /// A mapped <see cref="StoreRateAndReviewResult"/>; returns
    /// <see cref="StoreRateAndReviewStatus.NotSupported"/> when the call fails or the app is not packaged.
    /// </returns>
    public static async Task<StoreRateAndReviewResult> RequestRateAndReviewAppAsync(Window? owner = null)
    {
        try
        {
            var context = StoreContext.GetDefault();
            var window = owner ?? Application.Current?.MainWindow;
            if (window is not null)
            {
                var hwnd = new WindowInteropHelper(window).EnsureHandle();
                InitializeWithWindow.Initialize(context, hwnd);
            }

            var result = await context.RequestRateAndReviewAppAsync();
            return Map(result);
        }
        catch (Exception ex)
        {
            return new StoreRateAndReviewResult(StoreRateAndReviewStatus.NotSupported, ex.Message);
        }
    }

    /// <summary>
    /// Maps WinRT <see cref="Windows.Services.Store.StoreRateAndReviewResult"/> to the app-local result type.
    /// </summary>
    private static StoreRateAndReviewResult Map(Windows.Services.Store.StoreRateAndReviewResult result) =>
        result.Status switch
        {
            Windows.Services.Store.StoreRateAndReviewStatus.Succeeded =>
                new StoreRateAndReviewResult(StoreRateAndReviewStatus.Success),
            Windows.Services.Store.StoreRateAndReviewStatus.CanceledByUser =>
                new StoreRateAndReviewResult(StoreRateAndReviewStatus.Canceled),
            Windows.Services.Store.StoreRateAndReviewStatus.NetworkError =>
                new StoreRateAndReviewResult(StoreRateAndReviewStatus.NetworkError, result.ExtendedError?.Message),
            Windows.Services.Store.StoreRateAndReviewStatus.Error =>
                new StoreRateAndReviewResult(StoreRateAndReviewStatus.Error, result.ExtendedError?.Message),
            _ => new StoreRateAndReviewResult(StoreRateAndReviewStatus.NotSupported, result.ExtendedError?.Message),
        };
}
