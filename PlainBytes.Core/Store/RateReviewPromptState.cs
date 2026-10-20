// SPDX-FileCopyrightText: 2026 PlainBytes Studio
// SPDX-License-Identifier: GPL-3.0-only
using System.IO;
using System.Text.Json;

namespace PlainBytes.Core.Store;

/// <summary>
/// Persists local counters and opt-out state for the rate-and-review toast, scoped per product.
/// </summary>
public sealed class RateReviewPromptState
{
    /// <summary>
    /// Number of consecutive fully successful runs required before showing the rate-and-review prompt.
    /// </summary>
    public const int RequiredConsecutiveSuccesses = 5;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _storePath;
    private Model _model = new();

    /// <summary>
    /// Creates state for the given product; loads existing data from
    /// <c>%LocalAppData%\PlainBytes\{productId}\rate-review-prompt.json</c>.
    /// </summary>
    /// <param name="productId">Catalog product id (e.g. MetadataScrubber).</param>
    public RateReviewPromptState(string productId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productId);
        _storePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PlainBytes",
            productId,
            "rate-review-prompt.json");
        Load();
    }

    /// <summary>True when the user chose "never show again".</summary>
    public bool NeverShowAgain => _model.NeverShowAgain;

    /// <summary>Current streak of fully successful runs without cancel or partial failure.</summary>
    public int ConsecutiveSuccessRuns => _model.ConsecutiveSuccessRuns;

    /// <summary>Lifetime count of files processed during recorded successful runs.</summary>
    public int TotalProcessedFiles => _model.TotalProcessedFiles;

    /// <summary>
    /// Records one fully successful run and increments counters.
    /// </summary>
    /// <param name="processedFileCount">Files completed in this run; ignored when zero.</param>
    /// <returns>
    /// True when <see cref="ConsecutiveSuccessRuns"/> reaches <see cref="RequiredConsecutiveSuccesses"/>
    /// and the prompt should be shown.
    /// </returns>
    public bool RecordSuccessfulRun(int processedFileCount)
    {
        if (_model.NeverShowAgain)
            return false;

        if (processedFileCount > 0)
            _model.TotalProcessedFiles += processedFileCount;

        _model.ConsecutiveSuccessRuns++;
        Save();
        return _model.ConsecutiveSuccessRuns >= RequiredConsecutiveSuccesses;
    }

    /// <summary>
    /// Clears the consecutive success streak after cancel, dismiss, or partial failure.
    /// </summary>
    public void ResetConsecutiveCount()
    {
        if (_model.ConsecutiveSuccessRuns == 0)
            return;

        _model.ConsecutiveSuccessRuns = 0;
        Save();
    }

    /// <summary>
    /// Permanently suppresses the rate-and-review prompt for this product.
    /// </summary>
    public void MarkNeverShowAgain()
    {
        _model.NeverShowAgain = true;
        _model.ConsecutiveSuccessRuns = 0;
        Save();
    }

    /// <summary>Loads persisted state from disk; falls back to defaults on error.</summary>
    private void Load()
    {
        try
        {
            if (!File.Exists(_storePath))
                return;

            var json = File.ReadAllText(_storePath);
            _model = JsonSerializer.Deserialize<Model>(json) ?? new Model();
        }
        catch
        {
            _model = new Model();
        }
    }

    /// <summary>Persists state to disk; failures are ignored (local preference only).</summary>
    private void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(_storePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(_storePath, JsonSerializer.Serialize(_model, JsonOptions));
        }
        catch
        {
            // Local preference; ignore write failures.
        }
    }

    private sealed class Model
    {
        public bool NeverShowAgain { get; set; }
        public int ConsecutiveSuccessRuns { get; set; }
        public int TotalProcessedFiles { get; set; }
    }
}
