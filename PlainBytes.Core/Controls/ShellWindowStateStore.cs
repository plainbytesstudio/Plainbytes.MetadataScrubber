// SPDX-FileCopyrightText: 2026 PlainBytes Studio
// SPDX-License-Identifier: GPL-3.0-only
using System.IO;
using System.Text.Json;
using System.Windows;

namespace PlainBytes.Core.Controls;

/// <summary>
/// Persists shell window bounds and <see cref="WindowState"/> per tool under LocalAppData.
/// </summary>
internal static class ShellWindowStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>Serialized window placement for one tool.</summary>
    internal sealed class Snapshot
    {
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public int WindowState { get; set; }
    }

    /// <summary>True when a saved snapshot exists for <paramref name="storageKey"/>.</summary>
    public static bool Exists(string storageKey) => File.Exists(GetStorePath(storageKey));

    /// <summary>Loads a snapshot; returns false when missing or invalid.</summary>
    public static bool TryLoad(string storageKey, out Snapshot snapshot)
    {
        snapshot = new Snapshot();
        var path = GetStorePath(storageKey);
        if (!File.Exists(path))
            return false;

        try
        {
            var json = File.ReadAllText(path);
            var loaded = JsonSerializer.Deserialize<Snapshot>(json, JsonOptions);
            if (loaded is null || loaded.Width < 320 || loaded.Height < 240)
                return false;

            snapshot = loaded;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Writes the snapshot atomically (best effort).</summary>
    public static void Save(string storageKey, Snapshot snapshot)
    {
        try
        {
            var path = GetStorePath(storageKey);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var json = JsonSerializer.Serialize(snapshot, JsonOptions);
            File.WriteAllText(path, json);
        }
        catch
        {
            // ignore persistence failures
        }
    }

    private static string GetStorePath(string storageKey)
    {
        var safeKey = SanitizeFileName(storageKey);
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PlainBytes",
            "Shell",
            $"{safeKey}.json");
    }

    private static string SanitizeFileName(string value)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            value = value.Replace(c, '_');

        return string.IsNullOrWhiteSpace(value) ? "Default" : value.Trim();
    }
}
