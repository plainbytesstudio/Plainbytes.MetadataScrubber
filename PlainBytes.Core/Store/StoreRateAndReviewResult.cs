// SPDX-FileCopyrightText: 2026 PlainBytes Studio
// SPDX-License-Identifier: GPL-3.0-only
namespace PlainBytes.Core.Store;

/// <summary>
/// Outcome of the Microsoft Store in-app rate-and-review dialog.
/// </summary>
public enum StoreRateAndReviewStatus
{
    /// <summary>The user completed or submitted a rating/review.</summary>
    Success,

    /// <summary>The user dismissed the dialog without submitting.</summary>
    Canceled,

    /// <summary>The Store API returned a generic error.</summary>
    Error,

    /// <summary>The request failed due to a network issue.</summary>
    NetworkError,

    /// <summary>Rate-and-review is unavailable (e.g. unpackaged run or unsupported environment).</summary>
    NotSupported,
}

/// <summary>
/// Result returned by <see cref="StoreRating.RequestRateAndReviewAppAsync"/>.
/// </summary>
/// <param name="Status">High-level outcome of the rate-and-review request.</param>
/// <param name="Message">Optional error detail from the Store API; null on success or cancel.</param>
public sealed record StoreRateAndReviewResult(StoreRateAndReviewStatus Status, string? Message = null);
