// SPDX-FileCopyrightText: 2026 PlainBytes Studio
// SPDX-License-Identifier: GPL-3.0-only

using System.IO;
using System.Text;
using PlainBytes.Core.Store;

namespace PlainBytes.MetadataScrubber;

/// <summary>
/// Headless scrub: cleans files with the default strategy, appends a local log, and shows a system toast.
/// Accepts <c>--scrub-default "a" "b"</c> or <c>--scrub-default --list "paths.txt"</c> (multi-select).
/// </summary>
internal static class CliDefaultScrub
{
    /// <summary>Primary CLI flag that selects this mode.</summary>
    public const string Flag = "--scrub-default";

    /// <summary>Optional flag followed by a UTF-8 text file listing one path per line.</summary>
    public const string ListFlag = "--list";

    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PlainBytes",
        "MetadataScrubber",
        "logs");

    private static readonly string TempDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PlainBytes",
        "MetadataScrubber",
        "temp");

    /// <summary>True when <see cref="Flag"/> appears in the argument list.</summary>
    public static bool IsRequested(string[] args) =>
        args is not null &&
        Array.Exists(args, a => string.Equals(a, Flag, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Parses file paths from args when this mode is requested; returns true even if the path list is empty.
    /// Paths are de-duplicated case-insensitively while preserving order.
    /// </summary>
    public static bool TryParse(string[] args, out IReadOnlyList<string> paths)
    {
        paths = Array.Empty<string>();
        if (!IsRequested(args))
            return false;

        var list = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], Flag, StringComparison.OrdinalIgnoreCase))
                continue;

            if (string.Equals(args[i], ListFlag, StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length)
                    continue;

                var listPath = args[++i].Trim().Trim('"');
                list.AddRange(ReadPathList(listPath));
                TryDeleteOwnedListFile(listPath);
                continue;
            }

            if (string.IsNullOrWhiteSpace(args[i]))
                continue;

            list.Add(args[i].Trim().Trim('"'));
        }

        // De-duplicate while preserving order.
        var distinct = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in list)
        {
            if (string.IsNullOrWhiteSpace(p))
                continue;
            if (seen.Add(p))
                distinct.Add(p);
        }

        paths = distinct;
        return true;
    }

    /// <summary>Reads one file path per line from a UTF-8 list file.</summary>
    private static IReadOnlyList<string> ReadPathList(string listPath)
    {
        if (string.IsNullOrWhiteSpace(listPath) || !File.Exists(listPath))
            return [];

        var result = new List<string>();
        foreach (var line in File.ReadLines(listPath, Encoding.UTF8))
        {
            var path = line.Trim().Trim('"');
            if (!string.IsNullOrWhiteSpace(path))
                result.Add(path);
        }

        return result;
    }

    /// <summary>
    /// Deletes shell-owned temp list files under the app temp folder (<c>scrub-list-*.txt</c>) only.
    /// </summary>
    private static void TryDeleteOwnedListFile(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return;

            var full = Path.GetFullPath(path);
            var tempRoot = Path.GetFullPath(TempDirectory);
            if (!full.StartsWith(tempRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return;
            if (!Path.GetFileName(full).StartsWith("scrub-list-", StringComparison.OrdinalIgnoreCase) ||
                !full.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                return;

            File.Delete(full);
        }
        catch
        {
            // Cleanup failure must not abort the scrub job.
        }
    }

    /// <summary>Removes <c>scrub-list-*.txt</c> temp files older than 10 minutes.</summary>
    private static void SweepStaleListFiles()
    {
        try
        {
            if (!Directory.Exists(TempDirectory))
                return;

            var cutoff = DateTime.UtcNow.AddMinutes(-10);
            foreach (var file in Directory.EnumerateFiles(TempDirectory, "scrub-list-*.txt"))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(file) < cutoff)
                        File.Delete(file);
                }
                catch
                {
                    // ignore
                }
            }
        }
        catch
        {
            // ignore
        }
    }

    /// <summary>
    /// Runs default-strategy scrub for <paramref name="paths"/>.
    /// Exit codes: 0 all succeeded; 1 all failed or no valid files; 2 partial success (or license blocked).
    /// </summary>
    public static int Run(IReadOnlyList<string> paths)
    {
        SweepStaleListFiles();

        var logPath = Path.Combine(LogDirectory, $"scrub-cli-{DateTime.Now:yyyyMMdd}.log");
        Directory.CreateDirectory(LogDirectory);

        var sb = new StringBuilder();
        void Line(string text) => sb.AppendLine(text);

        Line($"==== {DateTime.Now:yyyy-MM-dd HH:mm:ss} {Flag} ====");

        var edition = ProductEditionAccess.QueryAsync("MetadataScrubber").GetAwaiter().GetResult();
        if (!ProductEditionAccess.IsFullFeatureAccess(edition))
        {
            Line("[FAIL] 试用已结束，请购买完整版后继续使用。");
            AppendLog(logPath, sb.ToString());
            CliResultToast.ShowLicenseExpired();
            return 2;
        }

        Line($"默认策略: {StrategySettings.DefaultStrategyId}");
        Line($"文件数: {paths.Count}");

        if (paths.Count == 0)
        {
            Line("未传入文件路径。");
            Line("----");
            var emptyToast = CliResultToast.ShowScrubResult(0, 0);
            if (emptyToast is not null)
                Line($"Toast: {emptyToast}");
            else
                Line("Toast: ok");
            AppendLog(logPath, sb.ToString());
            return 1;
        }

        var progressToast = CliResultToast.ShowScrubProgress(paths.Count);
        if (progressToast is not null)
            Line($"Toast: {progressToast}");

        var options = StrategySettings.GetOptions(StrategySettings.DefaultStrategyId);
        IReadOnlyList<ScrubFileResult> results;
        try
        {
            results = DocumentMetadataScrubber
                .ScrubManyAsync(paths, options)
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception ex)
        {
            Line($"[FAIL] 清理过程异常: {ex.Message}");
            Line("----");
            var exToast = CliResultToast.ShowScrubResult(0, paths.Count, ex.Message);
            if (exToast is not null)
                Line($"Toast: {exToast}");
            else
                Line("Toast: ok");
            AppendLog(logPath, sb.ToString());
            return 1;
        }

        var ok = 0;
        var fail = 0;
        string? firstError = null;
        foreach (var r in results)
        {
            if (r.Success)
            {
                ok++;
                Line($"[OK]   {r.Path}");
            }
            else
            {
                fail++;
                firstError ??= r.Error;
                Line($"[FAIL] {r.Path}  {r.Error}");
            }
        }

        if (results.Count < paths.Count)
        {
            var done = new HashSet<string>(results.Select(r => r.Path), StringComparer.OrdinalIgnoreCase);
            foreach (var p in paths)
            {
                if (done.Contains(p))
                    continue;
                fail++;
                firstError ??= "未处理";
                Line($"[FAIL] {p}  未处理");
            }
        }

        Line("----");
        Line($"成功 {ok} / 失败 {fail} / 共 {ok + fail}");
        Line($"日志: {logPath}");

        var toastStatus = CliResultToast.ShowScrubResult(ok, fail, firstError);
        Line(toastStatus is null ? "Toast: ok" : $"Toast: {toastStatus}");
        AppendLog(logPath, sb.ToString());

        if (ok == 0)
            return 1;
        if (fail > 0)
            return 2;
        return 0;
    }

    /// <summary>Appends the session log; write failures do not change exit-code semantics.</summary>
    private static void AppendLog(string logPath, string content)
    {
        try
        {
            File.AppendAllText(logPath, content + Environment.NewLine, Encoding.UTF8);
        }
        catch
        {
            // Log failure must not change exit-code semantics.
        }
    }
}
