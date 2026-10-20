// SPDX-FileCopyrightText: 2026 PlainBytes Studio
// SPDX-License-Identifier: GPL-3.0-only
namespace PlainBytes.Core.Store;

/// <summary>Edition state for a tool with local trial and Store add-on purchase.</summary>
public enum ProductEditionKind
{
    /// <summary>Add-on owned; full features without restriction.</summary>
    Purchased,

    /// <summary>Within the local trial window; same features as purchased.</summary>
    Trial,

    /// <summary>Trial ended; purchase required to continue using paid features.</summary>
    TrialExpired,
}
