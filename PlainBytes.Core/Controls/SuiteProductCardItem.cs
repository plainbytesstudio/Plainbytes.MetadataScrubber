// SPDX-FileCopyrightText: 2026 PlainBytes Studio
// SPDX-License-Identifier: GPL-3.0-only
namespace PlainBytes.Core.Controls;

/// <summary>
/// View-model row for a single product card in <see cref="SuiteProductMatrix"/>.
/// </summary>
internal sealed class SuiteProductCardItem
{
    /// <summary>Catalog product id (e.g. ImageConverter).</summary>
    public required string Id { get; init; }

    /// <summary>Display name shown on the card.</summary>
    public required string Name { get; init; }

    /// <summary>Short marketing description.</summary>
    public required string Description { get; init; }

    /// <summary>Pack URI or path to the product logo.</summary>
    public required string IconPath { get; init; }

    /// <summary>True when the product is installed on this machine.</summary>
    public required bool IsInstalled { get; init; }
}
