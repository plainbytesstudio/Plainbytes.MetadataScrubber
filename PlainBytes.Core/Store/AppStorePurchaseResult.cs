// SPDX-FileCopyrightText: 2026 PlainBytes Studio
// SPDX-License-Identifier: GPL-3.0-only
namespace PlainBytes.Core.Store;

/// <summary>Outcome of a Store add-on purchase request (<see cref="StorePurchaseService.RequestAddOnPurchaseAsync"/>).</summary>
public enum AppStorePurchaseStatus
{
    /// <summary>User completed a new purchase.</summary>
    Succeeded,

    /// <summary>Add-on was already owned before or after the dialog.</summary>
    AlreadyPurchased,

    /// <summary>User cancelled or closed the dialog without buying.</summary>
    NotPurchased,

    /// <summary>Store reported a connectivity failure.</summary>
    NetworkError,

    /// <summary>Store reported a server-side failure.</summary>
    ServerError,

    /// <summary>Unmapped Store status, exception, or non-packaged runtime.</summary>
    Unknown,
}

/// <summary>Result payload returned after requesting an in-app Store purchase.</summary>
public sealed class AppStorePurchaseResult
{
    /// <summary>Purchase outcome from the Store dialog.</summary>
    public required AppStorePurchaseStatus Status { get; init; }
}
