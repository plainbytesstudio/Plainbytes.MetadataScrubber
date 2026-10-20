// SPDX-FileCopyrightText: 2026 PlainBytes Studio
// SPDX-License-Identifier: GPL-3.0-only
namespace PlainBytes.Core.Support;

/// <summary>
/// Supporter unlock state for optional tip/add-on tiers; defaults to locked until Store validation sets it.
/// </summary>
public static class SupporterState
{
    /// <summary>True when the user has unlocked supporter perks via a Store add-on.</summary>
    public static bool IsUnlocked { get; private set; }

    /// <summary>Updates unlock state after Store add-on license checks.</summary>
    public static void SetUnlocked(bool unlocked) => IsUnlocked = unlocked;
}
