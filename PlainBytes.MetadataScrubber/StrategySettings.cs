// SPDX-FileCopyrightText: 2026 PlainBytes Studio
// SPDX-License-Identifier: GPL-3.0-only

using System.IO;
using System.Text.Json;
using PlainBytes.MetadataScrubber.Resources;

namespace PlainBytes.MetadataScrubber;

/// <summary>
/// One strategy catalog entry (built-in preset or user-defined custom).
/// </summary>
internal sealed class StrategyInfo
{
    /// <summary>Stable id (e.g. export, custom-xxxxxxxx).</summary>
    public required string Id { get; init; }

    /// <summary>Display name.</summary>
    public required string Name { get; set; }

    /// <summary>Short description shown in UI.</summary>
    public required string Description { get; set; }

    /// <summary>Pack URI for the strategy icon.</summary>
    public required string IconPath { get; init; }

    /// <summary>True for built-in presets that cannot be deleted.</summary>
    public bool IsPreset { get; init; }

    /// <summary>Scrub scope flags for this strategy.</summary>
    public ScrubOptions Options { get; set; } = ScrubOptions.Minimal;
}

/// <summary>
/// Offline persistence of the default strategy id and custom strategies under LocalApplicationData.
/// </summary>
internal static class StrategySettings
{
    /// <summary>Fallback preset id when the stored default is missing or invalid.</summary>
    public const string FallbackStrategyId = "export";
    private const string IconPack = "pack://application:,,,/PlainBytes.MetadataScrubber;component/Assets/Images/";

    private static readonly string StorePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PlainBytes",
        "MetadataScrubber",
        "strategy-settings.json");

    private const string CustomIconFile = "shield4.png";

    private static string _defaultStrategyId = FallbackStrategyId;
    private static readonly List<CustomStrategyModel> _customs = [];

    /// <summary>Raised after <see cref="DefaultStrategyId"/> is persisted.</summary>
    public static event Action? DefaultStrategyChanged;

    /// <summary>Raised after the custom strategy list is created, saved, or deleted.</summary>
    public static event Action? StrategiesChanged;

    /// <summary>Currently selected default strategy id.</summary>
    public static string DefaultStrategyId => _defaultStrategyId;

    static StrategySettings()
    {
        Load();
    }

    /// <summary>True when <paramref name="strategyId"/> is one of the three built-in presets.</summary>
    public static bool IsPresetId(string? strategyId)
    {
        var id = strategyId?.ToLowerInvariant();
        return id is "export" or "archive" or "minimal";
    }

    /// <summary>Returns presets followed by custom strategies in store order.</summary>
    public static IReadOnlyList<StrategyInfo> GetAllStrategies()
    {
        var list = new List<StrategyInfo>
        {
            CreatePreset("export", Strings.Strategy_Export_Name, Strings.Strategy_Export_Description, "shield1.png"),
            CreatePreset("archive", Strings.Strategy_Archive_Name, Strings.Strategy_Archive_Description, "shield2.png"),
            CreatePreset("minimal", Strings.Strategy_Minimal_Name, Strings.Strategy_Minimal_Description, "shield3.png"),
        };

        foreach (var custom in _customs)
        {
            list.Add(new StrategyInfo
            {
                Id = custom.Id!,
                Name = custom.Name ?? Strings.Strategy_Custom_Name,
                Description = custom.Description ?? "",
                IconPath = IconPack + CustomIconFile,
                IsPreset = false,
                Options = new ScrubOptions
                {
                    DocumentProperties = custom.DocumentProperties,
                    RevisionsAndComments = custom.RevisionsAndComments,
                    HiddenPathsAndResiduals = custom.HiddenPathsAndResiduals,
                },
            });
        }

        return list;
    }

    /// <summary>Finds a strategy by id; blank id uses <see cref="FallbackStrategyId"/>.</summary>
    public static StrategyInfo? FindStrategy(string? strategyId)
    {
        if (string.IsNullOrWhiteSpace(strategyId))
            strategyId = FallbackStrategyId;
        return GetAllStrategies().FirstOrDefault(s =>
            string.Equals(s.Id, strategyId, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Resolves scrub options for a strategy id; falls back to preset mapping when unknown.</summary>
    public static ScrubOptions GetOptions(string? strategyId)
    {
        var info = FindStrategy(strategyId);
        if (info is not null)
            return info.Options;
        return ScrubOptions.FromPresetId(strategyId);
    }

    /// <summary>Localized name and description for UI display.</summary>
    public static (string Name, string Description) GetDisplay(string? strategyId)
    {
        var info = FindStrategy(strategyId) ?? FindStrategy(FallbackStrategyId)!;
        return (info.Name, info.Description);
    }

    /// <summary>Sets and persists the default strategy; invalid ids fall back to <see cref="FallbackStrategyId"/>.</summary>
    public static void SetDefaultStrategyId(string strategyId)
    {
        if (string.IsNullOrWhiteSpace(strategyId))
            strategyId = FallbackStrategyId;

        if (FindStrategy(strategyId) is null)
            strategyId = FallbackStrategyId;

        if (string.Equals(_defaultStrategyId, strategyId, StringComparison.OrdinalIgnoreCase))
            return;

        _defaultStrategyId = strategyId;
        Persist();
        DefaultStrategyChanged?.Invoke();
    }

    /// <summary>Creates a new custom strategy with default scopes, persists it, and raises <see cref="StrategiesChanged"/>.</summary>
    public static StrategyInfo CreateCustomStrategy(string? preferredName = null)
    {
        var index = _customs.Count + 1;
        var id = "custom-" + Guid.NewGuid().ToString("N")[..8];
        var iconFile = CustomIconFile;
        var name = string.IsNullOrWhiteSpace(preferredName)
            ? Strings.Format(Strings.Strategy_Custom_NameIndexed, index)
            : preferredName.Trim();

        var model = new CustomStrategyModel
        {
            Id = id,
            Name = name,
            Description = Strings.Strategy_Custom_Description,
            IconFile = iconFile,
            DocumentProperties = true,
            RevisionsAndComments = true,
            HiddenPathsAndResiduals = false,
        };
        _customs.Add(model);
        Persist();
        StrategiesChanged?.Invoke();

        return new StrategyInfo
        {
            Id = id,
            Name = model.Name,
            Description = model.Description,
            IconPath = IconPack + iconFile,
            IsPreset = false,
            Options = new ScrubOptions
            {
                DocumentProperties = true,
                RevisionsAndComments = true,
                HiddenPathsAndResiduals = false,
            },
        };
    }

    /// <summary>
    /// Deletes a custom strategy. If it was the default, resets to <see cref="FallbackStrategyId"/>.
    /// Returns false for presets or unknown ids.
    /// </summary>
    public static bool DeleteCustomStrategy(string strategyId)
    {
        if (IsPresetId(strategyId))
            return false;

        var removed = _customs.RemoveAll(c =>
            string.Equals(c.Id, strategyId, StringComparison.OrdinalIgnoreCase)) > 0;
        if (!removed)
            return false;

        if (string.Equals(_defaultStrategyId, strategyId, StringComparison.OrdinalIgnoreCase))
        {
            _defaultStrategyId = FallbackStrategyId;
            Persist();
            DefaultStrategyChanged?.Invoke();
        }
        else
        {
            Persist();
        }

        StrategiesChanged?.Invoke();
        return true;
    }

    /// <summary>Replaces the custom strategy list from UI state and persists.</summary>
    public static void SaveCustomStrategies(IEnumerable<StrategyInfo> customs)
    {
        _customs.Clear();
        foreach (var item in customs.Where(c => !c.IsPreset))
        {
            if (string.IsNullOrWhiteSpace(item.Id))
                continue;

            _customs.Add(new CustomStrategyModel
            {
                Id = item.Id,
                Name = string.IsNullOrWhiteSpace(item.Name) ? Strings.Strategy_Custom_Name : item.Name.Trim(),
                Description = item.Description?.Trim() ?? "",
                IconFile = CustomIconFile,
                DocumentProperties = item.Options.DocumentProperties,
                RevisionsAndComments = item.Options.RevisionsAndComments,
                HiddenPathsAndResiduals = item.Options.HiddenPathsAndResiduals,
            });
        }

        Persist();
        StrategiesChanged?.Invoke();
    }

    /// <summary>Builds a localized preset <see cref="StrategyInfo"/>.</summary>
    private static StrategyInfo CreatePreset(string id, string name, string description, string iconFile) =>
        new()
        {
            Id = id,
            Name = name,
            Description = description,
            IconPath = IconPack + iconFile,
            IsPreset = true,
            Options = ScrubOptions.FromPresetId(id),
        };

    /// <summary>Loads default id and customs from JSON; resets on parse failure.</summary>
    private static void Load()
    {
        try
        {
            if (!File.Exists(StorePath))
                return;

            var json = File.ReadAllText(StorePath);
            var data = JsonSerializer.Deserialize<StoreModel>(json);
            if (data is null)
                return;

            if (!string.IsNullOrWhiteSpace(data.DefaultStrategyId))
                _defaultStrategyId = data.DefaultStrategyId!;

            _customs.Clear();
            if (data.Strategies is null)
                return;

            foreach (var item in data.Strategies)
            {
                if (string.IsNullOrWhiteSpace(item.Id) || IsPresetId(item.Id))
                    continue;
                _customs.Add(item);
            }

            // If the default strategy was deleted, fall back to a preset.
            if (FindStrategy(_defaultStrategyId) is null)
                _defaultStrategyId = FallbackStrategyId;
        }
        catch
        {
            _defaultStrategyId = FallbackStrategyId;
            _customs.Clear();
        }
    }

    /// <summary>Writes current default id and customs to LocalApplicationData JSON.</summary>
    private static void Persist()
    {
        try
        {
            var dir = Path.GetDirectoryName(StorePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var model = new StoreModel
            {
                DefaultStrategyId = _defaultStrategyId,
                Strategies = _customs.ToList(),
            };

            var json = JsonSerializer.Serialize(model, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(StorePath, json);
        }
        catch
        {
            // Ignore local write failures.
        }
    }

    /// <summary>JSON root for strategy-settings.json.</summary>
    private sealed class StoreModel
    {
        public string? DefaultStrategyId { get; set; }
        public List<CustomStrategyModel>? Strategies { get; set; }
    }

    /// <summary>Serialized custom strategy row (scopes stored as bools).</summary>
    private sealed class CustomStrategyModel
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? IconFile { get; set; }
        public bool DocumentProperties { get; set; }
        public bool RevisionsAndComments { get; set; }
        public bool HiddenPathsAndResiduals { get; set; }
    }
}
