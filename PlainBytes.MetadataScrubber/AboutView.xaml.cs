// SPDX-FileCopyrightText: 2026 PlainBytes Studio
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PlainBytes.Core.Catalog;
using PlainBytes.Core.Controls;
using PlainBytes.Core.Store;
using PlainBytes.MetadataScrubber.Controls;
using PlainBytes.MetadataScrubber.Resources;

namespace PlainBytes.MetadataScrubber;

/// <summary>
/// About page: app version, GPLv3 / source links, and "more tools" from <see cref="SuiteCatalog"/>.
/// </summary>
public partial class AboutView : UserControl
{
    private const string CurrentProductId = "MetadataScrubber";
    private const string SourceCodeUrl = "https://github.com/plainbytesstudio/PlainBytes.MetadataScrubber";
    private const string LicenseUrl = "https://www.gnu.org/licenses/gpl-3.0.html";

    public AboutView()
    {
        InitializeComponent();
        AboutVersionText.Text = Strings.Format(
            Strings.About_VersionFormat,
            AppPackageInfo.GetDisplayVersion(new MyMetadataScrubberModule().ToolVersion));
        MoreToolsList.ItemsSource = SuiteCatalog.GetAboutMoreTools(CurrentProductId);
    }

    /// <summary>Opens the suite products page in the hosting <see cref="ShellWindow"/>.</summary>
    private void ViewAllTools_Click(object sender, MouseButtonEventArgs e)
    {
        if (Window.GetWindow(this) is ShellWindow shell)
            shell.ShowSuiteProducts();
    }

    private void ContactUs_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (Window.GetWindow(this) is not ShellWindow shell)
            return;

        var overlay = new CustomDevelopmentOverlay();
        overlay.Closed += () => shell.HideOverlay();
        shell.ShowOverlay(overlay);
    }

    private void SourceCode_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        OpenUrl(SourceCodeUrl);
    }

    private void LicenseLink_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        var localLicense = Path.Combine(AppContext.BaseDirectory, "LICENSE");
        if (File.Exists(localLicense))
            OpenPath(localLicense);
        else
            OpenUrl(LicenseUrl);
    }

    private static void OpenUrl(string url) =>
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

    private static void OpenPath(string path) =>
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
}
