// SPDX-FileCopyrightText: 2026 PlainBytes Studio
// SPDX-License-Identifier: GPL-3.0-only
using System.Windows;

namespace PlainBytes.Core.Interfaces;

/// <summary>
/// Contract for a tool module loaded by the shared Shell; each Store app implements one instance.
/// </summary>
public interface IToolModule
{
    /// <summary>Display name of the tool (e.g. MetadataScrubber).</summary>
    string ToolName { get; }

    /// <summary>Semantic version string exposed in About and diagnostics.</summary>
    string ToolVersion { get; }

    /// <summary>Returns the root UI control hosting this tool's business logic.</summary>
    FrameworkElement GetMainControl();

    /// <summary>One-time setup invoked before the main control is shown.</summary>
    void Initialize();
}
