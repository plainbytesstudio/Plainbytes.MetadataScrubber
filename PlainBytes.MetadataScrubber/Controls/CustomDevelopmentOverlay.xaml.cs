// SPDX-FileCopyrightText: 2026 PlainBytes Studio
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PlainBytes.MetadataScrubber.Controls;

/// <summary>
/// Explains custom-development intake and opens the user's mail client via mailto (address only).
/// </summary>
internal partial class CustomDevelopmentOverlay : UserControl
{
    public const string SupportEmail = "plainbytes.studio@gmail.com";

    public event Action? Closed;

    public CustomDevelopmentOverlay()
    {
        InitializeComponent();
        Loaded += (_, _) => ContactButton.Focus();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) =>
        Closed?.Invoke();

    private void ContactButton_Click(object sender, RoutedEventArgs e)
    {
        OpenMailClient();
        Closed?.Invoke();
    }

    private void Root_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
            return;

        Closed?.Invoke();
        e.Handled = true;
    }

    private static void OpenMailClient()
    {
        // Address only — subject/body via mailto can garble in some clients (e.g. classic Outlook).
        var uri = $"mailto:{SupportEmail}";

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = uri,
                UseShellExecute = true,
            });
        }
        catch
        {
            // No default mail client.
        }
    }
}
