// SPDX-FileCopyrightText: 2026 PlainBytes Studio
// SPDX-License-Identifier: GPL-3.0-only

using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using PlainBytes.MetadataScrubber.Resources;

namespace PlainBytes.MetadataScrubber;

/// <summary>
/// Temporarily registers a classic Explorer shell verb under HKCU (appears under Win11 "Show more options").
/// Not a formal install path; for unpackaged debugging only. Unregister with <see cref="UnregisterFlag"/>.
/// </summary>
internal static class CliShellVerb
{
    /// <summary>CLI flag to register the debug shell verb for supported extensions.</summary>
    public const string RegisterFlag = "--register-shell-verb";

    /// <summary>CLI flag to remove the debug shell verb from HKCU.</summary>
    public const string UnregisterFlag = "--unregister-shell-verb";

    private const string VerbKeyName = "PlainBytes.ScrubDefault";

    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PlainBytes",
        "MetadataScrubber",
        "logs");

    /// <summary>
    /// Returns true when args request register or unregister; <paramref name="register"/> is true for register.
    /// </summary>
    public static bool TryParse(string[] args, out bool register)
    {
        register = false;
        if (args is null || args.Length == 0)
            return false;

        if (args.Any(a => string.Equals(a, RegisterFlag, StringComparison.OrdinalIgnoreCase)))
        {
            register = true;
            return true;
        }

        if (args.Any(a => string.Equals(a, UnregisterFlag, StringComparison.OrdinalIgnoreCase)))
        {
            register = false;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Registers or unregisters the verb for all supported extensions and notifies the shell.
    /// Returns 0 on success, 1 on failure.
    /// </summary>
    public static int Run(bool register)
    {
        var logPath = Path.Combine(LogDirectory, $"scrub-cli-{DateTime.Now:yyyyMMdd}.log");
        Directory.CreateDirectory(LogDirectory);
        var sb = new StringBuilder();
        void Line(string text) => sb.AppendLine(text);

        Line($"==== {DateTime.Now:yyyy-MM-dd HH:mm:ss} {(register ? RegisterFlag : UnregisterFlag)} ====");

        try
        {
            if (register)
            {
                var exe = ResolveExePath();
                if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe))
                {
                    Line($"[FAIL] 无法定位可执行文件: {exe}");
                    AppendLog(logPath, sb.ToString());
                    return 1;
                }

                var command = $"\"{exe}\" {CliDefaultScrub.Flag} \"%1\"";
                Line($"exe: {exe}");
                Line($"command: {command}");

                foreach (var ext in DocumentFileSupport.SupportedExtensions)
                {
                    WriteVerb(ext, command);
                    Line($"[OK]   已注册 {ext}");
                }
            }
            else
            {
                foreach (var ext in DocumentFileSupport.SupportedExtensions)
                {
                    RemoveVerb(ext);
                    Line($"[OK]   已移除 {ext}");
                }
            }

            NotifyShellAssocChanged();
            Line("----");
            Line(register
                ? $"完成。Win11：右键文件 → 显示更多选项 →「{Strings.ContextMenu_RootLabel}」。"
                : "完成。临时 Verb 已从 HKCU 移除。");
            Line($"日志: {logPath}");
            AppendLog(logPath, sb.ToString());
            return 0;
        }
        catch (Exception ex)
        {
            Line($"[FAIL] {ex.Message}");
            Line("----");
            AppendLog(logPath, sb.ToString());
            return 1;
        }
    }

    /// <summary>If the debug verb is already registered, refreshes menu labels for the current UI culture.</summary>
    public static void ApplyRegisteredLabels()
    {
        var any = false;
        foreach (var ext in DocumentFileSupport.SupportedExtensions)
        {
            using var key = Registry.CurrentUser.OpenSubKey(VerbRegistryPath(ext), writable: true);
            if (key is null)
                continue;

            key.SetValue(null, Strings.ContextMenu_RootLabel);
            any = true;
        }

        if (any)
            NotifyShellAssocChanged();
    }

    /// <summary>Resolves the current process executable path for the verb command line.</summary>
    private static string? ResolveExePath()
    {
        var path = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(path))
            return path;

        try
        {
            return Process.GetCurrentProcess().MainModule?.FileName;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>HKCU path for the shell verb under SystemFileAssociations.</summary>
    private static string VerbRegistryPath(string extension) =>
        $@"Software\Classes\SystemFileAssociations\{NormalizeExt(extension)}\shell\{VerbKeyName}";

    /// <summary>Normalizes an extension to lowercase with a leading dot.</summary>
    private static string NormalizeExt(string extension) =>
        extension.StartsWith('.') ? extension.ToLowerInvariant() : "." + extension.ToLowerInvariant();

    /// <summary>Creates or updates the HKCU verb and command for one extension.</summary>
    private static void WriteVerb(string extension, string command)
    {
        using var key = Registry.CurrentUser.CreateSubKey(VerbRegistryPath(extension), writable: true)
            ?? throw new InvalidOperationException($"无法创建注册表项: {extension}");
        key.SetValue(null, Strings.ContextMenu_RootLabel);
        key.SetValue("Icon", $"\"{ResolveExePath()}\",0");

        using var cmd = key.CreateSubKey("command", writable: true)
            ?? throw new InvalidOperationException($"无法创建 command: {extension}");
        cmd.SetValue(null, command);
    }

    /// <summary>Deletes the HKCU verb subtree for one extension if present.</summary>
    private static void RemoveVerb(string extension)
    {
        var path = VerbRegistryPath(extension);
        Registry.CurrentUser.DeleteSubKeyTree(path, throwOnMissingSubKey: false);
    }

    /// <summary>Notifies Explorer that file associations changed so the menu updates.</summary>
    private static void NotifyShellAssocChanged()
    {
        const uint SHCNE_ASSOCCHANGED = 0x08000000;
        const uint SHCNF_IDLIST = 0x0000;
        SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
    }

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    /// <summary>Appends session log text; write failures are ignored.</summary>
    private static void AppendLog(string logPath, string content)
    {
        try
        {
            File.AppendAllText(logPath, content + Environment.NewLine, Encoding.UTF8);
        }
        catch
        {
            // ignore
        }
    }
}
