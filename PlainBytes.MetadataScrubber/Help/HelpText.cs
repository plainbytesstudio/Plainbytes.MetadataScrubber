// SPDX-FileCopyrightText: 2026 PlainBytes Studio
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using System.Resources;

namespace PlainBytes.MetadataScrubber.Help;

/// <summary>
/// Localized help strings from <c>HelpResources</c>; falls back to the key when a translation is missing.
/// </summary>
internal static class HelpText
{
    private static readonly ResourceManager Manager =
        new("PlainBytes.MetadataScrubber.Help.HelpResources", typeof(HelpText).Assembly);

    /// <summary>Returns the string for <paramref name="key"/> in <see cref="CultureInfo.CurrentUICulture"/>, or the key itself if absent.</summary>
    public static string Get(string key)
    {
        var value = Manager.GetString(key, CultureInfo.CurrentUICulture);
        return string.IsNullOrEmpty(value) ? key : value;
    }
}
