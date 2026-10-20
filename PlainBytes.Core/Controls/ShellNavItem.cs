// SPDX-FileCopyrightText: 2026 PlainBytes Studio
// SPDX-License-Identifier: GPL-3.0-only
using System.Windows.Media;

namespace PlainBytes.Core.Controls;

/// <summary>
/// Top navigation rail item configured by each tool; bottom entries (language, matrix, support) are owned by the shell.
/// </summary>
public sealed class ShellNavItem
{
    /// <summary>
    /// Creates a shell navigation entry with icon, tooltip, and click handler.
    /// </summary>
    /// <param name="id">Stable nav id (e.g. home, settings); used for selection restore.</param>
    /// <param name="toolTip">Tooltip text shown on hover.</param>
    /// <param name="icon">20×20 rail icon, typically from <see cref="ShellWindow.LoadCoreNavIcon"/>.</param>
    /// <param name="onClick">Invoked when the user selects this item.</param>
    public ShellNavItem(string id, string toolTip, ImageSource icon, Action onClick)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(icon);
        ArgumentNullException.ThrowIfNull(onClick);

        Id = id;
        ToolTip = toolTip ?? string.Empty;
        Icon = icon;
        OnClick = onClick;
    }

    /// <summary>Stable navigation id within the tool.</summary>
    public string Id { get; }

    /// <summary>Hover tooltip for the rail button.</summary>
    public string ToolTip { get; }

    /// <summary>Icon displayed on the navigation rail.</summary>
    public ImageSource Icon { get; }

    /// <summary>Callback when the user activates this nav item.</summary>
    public Action OnClick { get; }
}
