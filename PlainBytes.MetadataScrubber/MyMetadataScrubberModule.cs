// SPDX-FileCopyrightText: 2026 PlainBytes Studio
// SPDX-License-Identifier: GPL-3.0-only

using System.Reflection;
using System.Windows;
using PlainBytes.Core.Interfaces;

namespace PlainBytes.MetadataScrubber;

/// <summary>
/// <see cref="IToolModule"/> entry for MetadataScrubber; supplies tool identity and the home <see cref="MainModule"/>.
/// </summary>
public class MyMetadataScrubberModule : IToolModule
{
    /// <inheritdoc />
    public string ToolName => "MetadataScrubber";

    /// <summary>
    /// Application version from the assembly (csproj <c>Version</c> / <c>InformationalVersion</c>),
    /// not the MSIX <c>Package.appxmanifest</c> identity version.
    /// </summary>
    public string ToolVersion => ResolveAssemblyVersion();

    /// <summary>Returns a new home scrub page control.</summary>
    public FrameworkElement GetMainControl() => new MainModule();

    /// <summary>No one-time setup required for this tool.</summary>
    public void Initialize()
    {
    }

    private static string ResolveAssemblyVersion()
    {
        var assembly = typeof(MyMetadataScrubberModule).Assembly;
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            var plus = informational.IndexOf('+');
            return plus >= 0 ? informational[..plus] : informational;
        }

        var version = assembly.GetName().Version;
        return version is null ? "0.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
    }
}
