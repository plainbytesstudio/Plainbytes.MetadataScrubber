// SPDX-FileCopyrightText: 2026 PlainBytes Studio
// SPDX-License-Identifier: GPL-3.0-only

using System.Windows.Controls;

namespace PlainBytes.MetadataScrubber.Controls;

/// <summary>
/// Modal dim overlay with a spinning indicator and status message during long operations.
/// </summary>
public partial class LoadingOverlay : UserControl
{
    public LoadingOverlay()
    {
        InitializeComponent();
    }

    /// <summary>Updates the status text shown under the spinner.</summary>
    public void SetMessage(string message)
    {
        MessageText.Text = message;
    }
}
