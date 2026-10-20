// SPDX-FileCopyrightText: 2026 PlainBytes Studio
// SPDX-License-Identifier: GPL-3.0-only

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using PlainBytes.Core.Controls;
using PlainBytes.Core.Store;
using PlainBytes.MetadataScrubber.Controls;
using PlainBytes.MetadataScrubber.Resources;

namespace PlainBytes.MetadataScrubber;

/// <summary>
/// Home scrub page: drop zone, file list with expandable metadata, strategy picker, and batch scrub flow.
/// </summary>
public partial class MainModule : UserControl
{
    private const double DropZoneMaxHeightIdle = 430;
    private const double DropZoneHeightCompact = 130;
    /// <summary>Idle drop-zone icon is shown only when the shell window is taller than this.</summary>
    private const double DropZoneIconMinWindowHeight = 706;
    private const int LoadingOverlayThreshold = 1;
    private const double FileListCornerRadius = 12;
    private const string IconPack = "pack://application:,,,/PlainBytes.MetadataScrubber;component/Assets/Images/";

    private readonly ObservableCollection<FileListItem> _fileItems = [];
    private readonly RateReviewPromptState _ratePrompt = new("MetadataScrubber");
    private readonly RateReviewToastBar _rateToast = new();
    private readonly SemaphoreSlim _fileLoadGate = new(1, 1);
    private FileListItem? _expandedItem;
    private bool _isFilesSelectedLayout;
    private string _strategyId = StrategySettings.FallbackStrategyId;
    private bool _isScrubbing;

    public MainModule()
    {
        InitializeComponent();
        FileListItems.ItemsSource = _fileItems;
        _strategyId = StrategySettings.DefaultStrategyId;
        RebuildStrategyMenus();
        ApplyStrategyUi(_strategyId);
        DropZone.SizeChanged += (_, _) => UpdateDropZoneIconVisibility();
        SizeChanged += (_, _) => UpdateDropZoneIconVisibility();
        Loaded += MainModule_Loaded;
        Unloaded += MainModule_Unloaded;
    }

    /// <summary>Subscribes to strategy setting changes when the page is shown.</summary>
    private void MainModule_Loaded(object sender, RoutedEventArgs e)
    {
        StrategySettings.DefaultStrategyChanged -= OnDefaultStrategyChanged;
        StrategySettings.DefaultStrategyChanged += OnDefaultStrategyChanged;
        StrategySettings.StrategiesChanged -= OnStrategiesChanged;
        StrategySettings.StrategiesChanged += OnStrategiesChanged;

        if (Window.GetWindow(this) is { } window)
        {
            window.SizeChanged -= OnHostWindowSizeChanged;
            window.SizeChanged += OnHostWindowSizeChanged;
        }

        RebuildStrategyMenus();
        if (StrategySettings.FindStrategy(_strategyId) is null)
            _strategyId = StrategySettings.DefaultStrategyId;
        ApplyStrategyUi(_strategyId);
        UpdateDropZoneIconVisibility();
    }

    /// <summary>Unsubscribes strategy events and hides the rate toast when leaving the page.</summary>
    private void MainModule_Unloaded(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is { } window)
            window.SizeChanged -= OnHostWindowSizeChanged;

        StrategySettings.DefaultStrategyChanged -= OnDefaultStrategyChanged;
        StrategySettings.StrategiesChanged -= OnStrategiesChanged;
        HideRateToast();
    }

    private void OnHostWindowSizeChanged(object sender, SizeChangedEventArgs e) =>
        UpdateDropZoneIconVisibility();

    /// <summary>Applies the new default strategy when settings change.</summary>
    private void OnDefaultStrategyChanged()
    {
        Dispatcher.Invoke(() =>
        {
            _strategyId = StrategySettings.DefaultStrategyId;
            ApplyStrategyUi(_strategyId);
        });
    }

    /// <summary>Rebuilds strategy menus when the strategy list changes.</summary>
    private void OnStrategiesChanged()
    {
        Dispatcher.Invoke(() =>
        {
            RebuildStrategyMenus();
            if (StrategySettings.FindStrategy(_strategyId) is null)
                _strategyId = StrategySettings.DefaultStrategyId;
            ApplyStrategyUi(_strategyId);
        });
    }

    /// <summary>Rebuilds idle and active strategy context menus.</summary>
    private void RebuildStrategyMenus()
    {
        RebuildStrategyMenu(ActiveStrategyMenu);
        RebuildStrategyMenu(IdleStrategyMenu);
    }

    /// <summary>Fills a context menu with all strategies from settings.</summary>
    private void RebuildStrategyMenu(ContextMenu? menu)
    {
        if (menu is null)
            return;

        menu.Items.Clear();
        foreach (var strategy in StrategySettings.GetAllStrategies())
        {
            var item = new MenuItem
            {
                Header = strategy.Name,
                Tag = strategy.Id,
            };
            item.Click += StrategyMenuItem_Click;
            menu.Items.Add(item);
        }
    }

    private void DropZone_DragEnter(object sender, DragEventArgs e)
    {
        // TODO: Idle → highlight drop zone on drag enter
    }

    private void DropZone_DragLeave(object sender, DragEventArgs e)
    {
        // TODO: Clear drop-zone highlight
    }

    /// <summary>Allows copy drop only when the drag payload includes supported files or folders.</summary>
    private void DropZone_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = HasSupportedFiles(e) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    /// <summary>Adds dropped files (and expands folders) to the list.</summary>
    private void DropZone_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths)
            return;

        AddFiles(paths, expandDropped: true);
    }

    /// <summary>Opens a multi-select file dialog for supported document types.</summary>
    private void SelectFilesButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = Strings.Home_SelectFiles,
            Multiselect = true,
            CheckFileExists = true,
            Filter =
                $"{Strings.Dialog_FilterSupported}|*.docx;*.docm;*.xlsx;*.xlsm;*.pptx;*.pptm;*.pdf|" +
                $"{Strings.Type_Word}|*.docx;*.docm|" +
                $"{Strings.Type_Excel}|*.xlsx;*.xlsm|" +
                $"{Strings.Type_PowerPoint}|*.pptx;*.pptm|" +
                $"{Strings.Type_Pdf}|*.pdf",
        };

        if (dialog.ShowDialog() != true)
            return;

        AddFiles(dialog.FileNames);
    }

    /// <summary>True when the drag payload contains at least one supported file or a folder.</summary>
    private static bool HasSupportedFiles(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return false;

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths)
            return false;

        return paths.Any(p =>
            (File.Exists(p) && DocumentFileSupport.IsSupported(p))
            || Directory.Exists(p));
    }

    /// <summary>Queues asynchronous file import (fire-and-forget).</summary>
    private void AddFiles(IEnumerable<string> paths, bool expandDropped = false) =>
        _ = AddFilesAsync(paths, expandDropped);

    /// <summary>Imports paths from single-instance activation or startup args.</summary>
    public void ImportStartupFiles(IEnumerable<string> paths) => AddFiles(paths);

    /// <summary>
    /// Loads files off the UI thread with optional loading overlay, then commits items to the list.
    /// </summary>
    private async Task AddFilesAsync(IEnumerable<string> paths, bool expandDropped = false)
    {
        var rawPaths = paths as string[] ?? paths.ToArray();
        if (rawPaths.Length == 0)
            return;

        await _fileLoadGate.WaitAsync();
        ShellWindow? shell = null;
        LoadingOverlay? overlay = null;

        try
        {
            var existingPaths = _fileItems
                .Select(i => i.FilePath)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var shouldShowOverlay = rawPaths.Length >= LoadingOverlayThreshold
                || (expandDropped && rawPaths.Any(Directory.Exists));

            shell = Window.GetWindow(this) as ShellWindow;
            if (shouldShowOverlay && shell is not null)
            {
                overlay = new LoadingOverlay();
                shell.ShowOverlay(overlay);
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            }

            IProgress<(int Done, int Total)>? progress = overlay is null
                ? null
                : new Progress<(int Done, int Total)>(report =>
                    overlay.SetMessage(Strings.Format(Strings.Loading_LoadingFiles, report.Done, report.Total)));

            var processed = await Task.Run(() =>
                BuildFileItems(rawPaths, expandDropped, existingPaths, progress));

            var added = CommitFileItems(processed);
            if (added == 0 && _fileItems.Count == 0)
                return;

            if (!_isFilesSelectedLayout && _fileItems.Count > 0)
                ApplyFilesSelectedLayout();
            else
                UpdateFileCountUi(_fileItems.Count);

            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);

            shell?.HideOverlay();
            overlay = null;
        }
        finally
        {
            shell?.HideOverlay();
            _fileLoadGate.Release();
        }
    }

    /// <summary>
    /// Expands folders if needed, skips duplicates, and builds list items with metadata on a worker thread.
    /// </summary>
    private static List<(string Path, FileListItem? Item)> BuildFileItems(
        string[] rawPaths,
        bool expandDropped,
        HashSet<string> existingPaths,
        IProgress<(int Done, int Total)>? progress)
    {
        var pathList = expandDropped
            ? ExpandDroppedPaths(rawPaths).ToList()
            : rawPaths.Where(DocumentFileSupport.IsSupported).ToList();

        var uniquePaths = new List<string>(pathList.Count);
        foreach (var path in pathList)
        {
            if (!existingPaths.Add(path))
                continue;

            uniquePaths.Add(path);
        }

        var total = uniquePaths.Count;
        var results = new List<(string Path, FileListItem? Item)>(total);
        for (var i = 0; i < uniquePaths.Count; i++)
        {
            var path = uniquePaths[i];
            FileListItem? item = null;
            try
            {
                item = CreateFileListItem(path);
            }
            catch
            {
                // Skip files that cannot be read.
            }

            results.Add((path, item));

            if (progress is not null && ((i + 1) % 5 == 0 || i == total - 1))
                progress.Report((i + 1, total));
        }

        return results;
    }

    /// <summary>Adds successfully built items to the observable collection; returns how many were added.</summary>
    private int CommitFileItems(IReadOnlyList<(string Path, FileListItem? Item)> processed)
    {
        var existing = new HashSet<string>(
            _fileItems.Select(i => i.FilePath),
            StringComparer.OrdinalIgnoreCase);

        var added = 0;
        foreach (var (path, item) in processed)
        {
            if (item is null || !existing.Add(path))
                continue;

            _fileItems.Add(item);
            added++;
        }

        return added;
    }

    /// <summary>Yields supported files from paths, recursively expanding directories when dropped.</summary>
    private static IEnumerable<string> ExpandDroppedPaths(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (File.Exists(path) && DocumentFileSupport.IsSupported(path))
            {
                yield return path;
                continue;
            }

            if (!Directory.Exists(path))
                continue;

            foreach (var file in Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories))
            {
                if (DocumentFileSupport.IsSupported(file))
                    yield return file;
            }
        }
    }

    /// <summary>Creates a list row with type, size, icon, and metadata groups for one file.</summary>
    private static FileListItem CreateFileListItem(string path)
    {
        var info = new FileInfo(path);
        var item = new FileListItem
        {
            FilePath = path,
            FileName = info.Name,
            TypeLabel = DocumentFileSupport.GetTypeLabel(path),
            SizeLabel = DocumentFileSupport.FormatSize(info.Exists ? info.Length : 0),
            IconPath = IconPack + DocumentFileSupport.GetIconFileName(path),
            Groups = DocumentMetadataReader.Read(path),
        };
        item.SetStatus(Strings.Status_Pending);
        return item;
    }

    /// <summary>Switches from idle drop zone to compact layout with file list and scrub action bar.</summary>
    private void ApplyFilesSelectedLayout()
    {
        _isFilesSelectedLayout = true;

        DropZoneRow.Height = GridLength.Auto;
        DropZoneRow.MaxHeight = DropZoneHeightCompact;
        FileListRow.Height = new GridLength(1, GridUnitType.Star);

        DropZone.Height = DropZoneHeightCompact;
        DropZone.MaxHeight = DropZoneHeightCompact;
        DropZoneIcon.Visibility = Visibility.Collapsed;
        DropZoneTitle.Visibility = Visibility.Collapsed;
        DropZoneSubtitle.Visibility = Visibility.Collapsed;
        DropZoneContentPanel.MaxWidth = double.PositiveInfinity;
        DropZoneContentPanel.Margin = new Thickness(24, 16, 24, 16);
        DropZoneFormatHint.Text = Strings.Home_DropHintCompact;
        DropZoneFormatHint.TextWrapping = TextWrapping.Wrap;

        FileListCard.Visibility = Visibility.Visible;
        TipBar.Visibility = Visibility.Collapsed;
        StrategyCard.Visibility = Visibility.Collapsed;
        StrategyActionBar.Visibility = Visibility.Visible;

        UpdateFileCountUi(_fileItems.Count);
    }

    /// <summary>
    /// Idle layout: show the drop-zone icon only when the shell <see cref="Window.ActualHeight"/>
    /// exceeds <see cref="DropZoneIconMinWindowHeight"/>. Compact layout always hides the icon.
    /// </summary>
    private void UpdateDropZoneIconVisibility()
    {
        if (_isFilesSelectedLayout)
        {
            DropZoneIcon.Visibility = Visibility.Collapsed;
            return;
        }

        var windowHeight = Window.GetWindow(this)?.ActualHeight ?? ActualHeight;
        DropZoneIcon.Visibility = windowHeight > DropZoneIconMinWindowHeight
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    /// <summary>
    /// CornerRadius only paints a round border; clip header/list fills so square corners don't poke out.
    /// </summary>
    private void FileListBody_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (FileListBody.ActualWidth <= 0 || FileListBody.ActualHeight <= 0)
            return;

        FileListBody.Clip = new RectangleGeometry(
            new Rect(0, 0, FileListBody.ActualWidth, FileListBody.ActualHeight),
            FileListCornerRadius,
            FileListCornerRadius);
    }

    /// <summary>Updates file-count label and scrub button caption.</summary>
    private void UpdateFileCountUi(int count)
    {
        FileListCountText.Text = Strings.Format(Strings.Home_FileCount, count);
        ClearMetadataButton.Content = Strings.Format(Strings.Home_ScrubButton, count);
    }

    /// <summary>Toggles row expand unless the click originated on a nested button.</summary>
    private void FileRow_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: FileListItem item })
            return;

        // Clicks on row buttons are handled by the button; do not toggle expand again.
        if (e.OriginalSource is DependencyObject source
            && FindAncestorButton(source) is not null)
            return;

        ToggleFileRowExpand(item);
    }

    /// <summary>Expands or collapses the row via the explicit expand control.</summary>
    private void FileRowExpand_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: FileListItem item })
            return;

        ToggleFileRowExpand(item);
    }

    /// <summary>Ensures only one file row is expanded at a time.</summary>
    private void ToggleFileRowExpand(FileListItem item)
    {
        if (_expandedItem == item)
        {
            item.IsExpanded = false;
            _expandedItem = null;
            return;
        }

        if (_expandedItem is not null)
            _expandedItem.IsExpanded = false;

        item.IsExpanded = true;
        _expandedItem = item;
    }

    /// <summary>Walks the visual tree upward to find a button ancestor.</summary>
    private static System.Windows.Controls.Primitives.ButtonBase? FindAncestorButton(DependencyObject? node)
    {
        while (node is not null)
        {
            if (node is System.Windows.Controls.Primitives.ButtonBase button)
                return button;
            node = System.Windows.Media.VisualTreeHelper.GetParent(node);
        }

        return null;
    }

    /// <summary>Shows the Store rate-and-review toast in the shell top toast host.</summary>
    private void ShowRateToast()
    {
        _rateToast.Show(_ratePrompt);
        if (Window.GetWindow(this) is ShellWindow shell)
            shell.ShowTopToast(_rateToast);
    }

    /// <summary>Hides the rate-and-review toast if visible.</summary>
    private void HideRateToast()
    {
        _rateToast.Hide();
        if (Window.GetWindow(this) is ShellWindow shell)
            shell.HideTopToast();
    }

    /// <summary>Removes one file from the list.</summary>
    private void FileRowRemove_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: FileListItem item })
            return;

        if (ReferenceEquals(_expandedItem, item))
            _expandedItem = null;

        _fileItems.Remove(item);
        UpdateFileCountUi(_fileItems.Count);
        // TODO: Restore idle layout when the list becomes empty
    }

    /// <summary>Clears the entire file list.</summary>
    private void ClearFileListButton_Click(object sender, RoutedEventArgs e)
    {
        _expandedItem = null;
        _fileItems.Clear();
        UpdateFileCountUi(0);
        // TODO: Restore idle layout
    }

    /// <summary>
    /// Runs batch metadata scrub with progress overlay.
    /// </summary>
    private async void ClearMetadataButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isScrubbing || _fileItems.Count == 0)
            return;

        if (Window.GetWindow(this) is not ShellWindow shell)
            return;

        _isScrubbing = true;
        ClearMetadataButton.IsEnabled = false;
        HideRateToast();

        var overlay = new ScrubProgressOverlay();
        var snapshot = _fileItems.ToList();
        var allSucceeded = false;
        var successCount = 0;
        overlay.DismissRequested += () =>
        {
            shell.HideOverlay();
            if (allSucceeded && _ratePrompt.RecordSuccessfulRun(successCount))
                ShowRateToast();
        };
        shell.ShowOverlay(overlay);
        overlay.Show(snapshot.Count);

        var options = StrategySettings.GetOptions(_strategyId);
        var progress = new Progress<ScrubProgressReport>(report =>
        {
            var pct = report.Total == 0 ? 100 : report.Completed * 100d / report.Total;
            overlay.UpdateProgress(pct, report.Completed, report.Total, report.CurrentFile);
        });

        try
        {
            var results = await DocumentMetadataScrubber.ScrubManyAsync(
                snapshot.Select(i => i.FilePath).ToList(),
                options,
                progress,
                overlay.CancellationToken);

            var byPath = results.ToDictionary(r => r.Path, StringComparer.OrdinalIgnoreCase);
            foreach (var item in snapshot)
            {
                if (!byPath.TryGetValue(item.FilePath, out var result))
                    continue;

                if (result.Success)
                {
                    item.SetStatus(Strings.Status_Cleaned);
                    item.Groups = DocumentMetadataReader.Read(item.FilePath);
                    var info = new FileInfo(item.FilePath);
                    item.SizeLabel = DocumentFileSupport.FormatSize(info.Exists ? info.Length : 0);
                }
                else
                {
                    item.SetStatus(FormatFailureStatus(result.Error), failed: true);
                }
            }

            var cancelled = overlay.CancellationToken.IsCancellationRequested;
            if (cancelled)
            {
                _ratePrompt.ResetConsecutiveCount();
            }
            else
            {
                successCount = results.Count(r => r.Success);
                var failed = results.Count - successCount;
                allSucceeded = failed == 0 && successCount > 0;
                if (!allSucceeded)
                    _ratePrompt.ResetConsecutiveCount();

                overlay.Complete(successCount, failed, results);
            }
        }
        finally
        {
            _isScrubbing = false;
            ClearMetadataButton.IsEnabled = true;
        }
    }

    /// <summary>Maps scrub errors to a short status string for the file row.</summary>
    private static string FormatFailureStatus(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
            return Strings.Status_Failed;

        var text = error.Trim();
        if (ContainsOccupied(text))
            return Strings.Status_FailedOccupied;

        if (text.Length > 40)
            text = text[..40] + "…";
        return Strings.Format(Strings.Status_FailedPrefix, text);
    }

    /// <summary>Detects file-in-use messages across English and localized tokens.</summary>
    private static bool ContainsOccupied(string text) =>
        text.Contains("being used by another process", StringComparison.OrdinalIgnoreCase)
        || ContainsToken(text, Strings.Error_Occupied)
        || ContainsToken(text, Strings.FailReason_Occupied)
        || ContainsToken(text, Strings.Status_FailedOccupied);

    private static bool ContainsToken(string text, string token) =>
        !string.IsNullOrEmpty(token) && text.Contains(token, StringComparison.Ordinal);

    /// <summary>Applies the strategy selected from a context menu item.</summary>
    private void StrategyMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string id })
            return;

        _strategyId = id;
        ApplyStrategyUi(id);
    }

    /// <summary>Updates strategy name, hint, and default badge on idle and active chrome.</summary>
    private void ApplyStrategyUi(string id)
    {
        var (name, hint) = StrategySettings.GetDisplay(id);

        StrategySelectButton.Content = name;
        if (StrategyHintText is not null)
            StrategyHintText.Text = hint;

        if (IdleStrategyNameText is not null)
            IdleStrategyNameText.Text = name;
        if (IdleStrategyHintText is not null)
            IdleStrategyHintText.Text = hint;

        var isDefault = string.Equals(id, StrategySettings.DefaultStrategyId, StringComparison.OrdinalIgnoreCase);
        if (IdleDefaultStrategyBadge is not null)
            IdleDefaultStrategyBadge.Visibility = isDefault ? Visibility.Visible : Visibility.Collapsed;

        UpdateTipBarFromDefaultStrategy();
    }

    /// <summary>Refreshes the tip bar text from the current default strategy description.</summary>
    private void UpdateTipBarFromDefaultStrategy()
    {
        if (TipBarText is null)
            return;

        var (_, description) = StrategySettings.GetDisplay(StrategySettings.DefaultStrategyId);
        TipBarText.Text = Strings.Format(Strings.Home_TipBar, description);
    }

    /// <summary>Opens the help page in the hosting shell.</summary>
    private void LearnMoreHyperlink_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is ShellWindow shell)
            shell.ShowHelp();
    }
}

/// <summary>One metadata name/value pair shown under an expanded file row.</summary>
internal sealed class MetaField(string name, string value)
{
    public string Name { get; } = name;
    public string Value { get; } = value;
}

/// <summary>Named group of metadata fields (e.g. document properties, revisions).</summary>
internal sealed class MetaGroup(string title, IReadOnlyList<MetaField> fields)
{
    public string Title { get; } = title;
    public IReadOnlyList<MetaField> Fields { get; } = fields;
}

/// <summary>View-model for one file row in the home list (status, expand, metadata groups).</summary>
internal sealed class FileListItem : INotifyPropertyChanged
{
    private bool _isExpanded;
    private bool _isFailed;
    private string _status = Strings.Status_Pending;
    private string _sizeLabel = "";
    private IReadOnlyList<MetaGroup> _groups = [];

    public string FilePath { get; init; } = "";
    public string FileName { get; init; } = "";
    public string TypeLabel { get; init; } = "";
    public string IconPath { get; init; } = "";

    /// <summary>Formatted file size label; updates after scrub when size may change.</summary>
    public string SizeLabel
    {
        get => _sizeLabel;
        set
        {
            if (_sizeLabel == value)
                return;
            _sizeLabel = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Pending / cleaned / failed status text.</summary>
    public string Status => _status;

    /// <summary>True when the last scrub attempt for this file failed.</summary>
    public bool IsFailed => _isFailed;

    /// <summary>Updates status text and failed styling.</summary>
    public void SetStatus(string status, bool failed = false)
    {
        if (_status == status && _isFailed == failed)
            return;
        _status = status;
        _isFailed = failed;
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(IsFailed));
    }

    /// <summary>Metadata groups shown when the row is expanded.</summary>
    public IReadOnlyList<MetaGroup> Groups
    {
        get => _groups;
        set
        {
            _groups = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasMetadata));
        }
    }

    /// <summary>True when at least one metadata group is present.</summary>
    public bool HasMetadata => Groups.Count > 0;

    /// <summary>Whether the metadata details panel is expanded.</summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value)
                return;
            _isExpanded = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
