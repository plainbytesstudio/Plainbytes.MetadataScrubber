// SPDX-FileCopyrightText: 2026 PlainBytes Studio
// SPDX-License-Identifier: GPL-3.0-only
namespace PlainBytes.Core.Store;

/// <summary>Read-only snapshot of the current product edition state.</summary>
public sealed class ProductEditionSnapshot
{
    /// <summary>Current edition state (<see cref="ProductEditionKind"/>).</summary>
    public required ProductEditionKind Kind { get; init; }

    /// <summary>Remaining trial days; only meaningful for <see cref="ProductEditionKind.Trial"/>.</summary>
    public int? TrialDaysRemaining { get; init; }

    /// <summary>True when the user has full access to the features (trial or purchased).</summary>
    public bool IsFullFeatureAccess =>
        Kind is ProductEditionKind.Purchased or ProductEditionKind.Trial;
}
