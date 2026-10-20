// SPDX-FileCopyrightText: 2026 PlainBytes Studio
// SPDX-License-Identifier: GPL-3.0-only

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using MessageBoxResult = System.Windows.MessageBoxResult;
using PlainBytes.MetadataScrubber.Resources;

namespace PlainBytes.MetadataScrubber;

/// <summary>
/// Strategy settings page: list presets/custom strategies, edit scopes, set default, save/delete customs.
/// </summary>
public partial class StrategyManagementView : UserControl
{
    private readonly ObservableCollection<StrategyListItem> _strategies = [];
    private readonly ObservableCollection<CleanupScopeItem> _cleanupScopes = [];
    private StrategyListItem? _selectedStrategy;
    private bool _suppressScopeWriteBack;
    private const double StrategyBodyCornerRadius = 12;

    public StrategyManagementView()
    {
        InitializeComponent();
        ReloadStrategiesFromStore();
        LoadCleanupScopeTemplates();
        StrategyListItems.ItemsSource = _strategies;
        CleanupScopeItems.ItemsSource = _cleanupScopes;

        ApplyDefaultFlags();
        var initial = _strategies.FirstOrDefault(s => s.IsDefault) ?? _strategies.FirstOrDefault();
        if (initial is not null)
            ShowStrategyDetails(initial);
    }

    /// <summary>
    /// CornerRadius only paints a round border; clip column fills so square corners don't poke out.
    /// </summary>
    private void StrategyBody_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (StrategyBody.ActualWidth <= 0 || StrategyBody.ActualHeight <= 0)
            return;

        StrategyBody.Clip = new RectangleGeometry(
            new Rect(0, 0, StrategyBody.ActualWidth, StrategyBody.ActualHeight),
            StrategyBodyCornerRadius,
            StrategyBodyCornerRadius);
    }

    /// <summary>Reloads strategies from <see cref="StrategySettings"/> and restores selection when possible.</summary>
    private void ReloadStrategiesFromStore()
    {
        var selectedId = _selectedStrategy?.Id;
        _strategies.Clear();
        foreach (var info in StrategySettings.GetAllStrategies())
            _strategies.Add(StrategyListItem.FromInfo(info));

        ApplyDefaultFlags();

        if (!string.IsNullOrEmpty(selectedId))
        {
            var again = _strategies.FirstOrDefault(s =>
                string.Equals(s.Id, selectedId, StringComparison.OrdinalIgnoreCase));
            if (again is not null)
            {
                ShowStrategyDetails(again);
                return;
            }
        }

        var fallback = _strategies.FirstOrDefault(s => s.IsDefault) ?? _strategies.FirstOrDefault();
        if (fallback is not null)
            ShowStrategyDetails(fallback);
    }

    /// <summary>Copies scrub option flags onto a list item.</summary>
    private void ApplyScopesFromOptions(StrategyListItem item, ScrubOptions options)
    {
        item.DocumentProperties = options.DocumentProperties;
        item.RevisionsAndComments = options.RevisionsAndComments;
        item.HiddenPathsAndResiduals = options.HiddenPathsAndResiduals;
    }

    /// <summary>Creates the three fixed cleanup-scope rows bound to the detail panel.</summary>
    private void LoadCleanupScopeTemplates()
    {
        const string iconPack = "pack://application:,,,/PlainBytes.MetadataScrubber;component/Assets/Images/";
        _cleanupScopes.Clear();
        _cleanupScopes.Add(new CleanupScopeItem(
            CleanupScopeKind.DocumentProperties,
            Strings.Scope_DocumentProperties_Title,
            Strings.Scope_DocumentProperties_Description,
            iconPack + "part1.png",
            isEnabled: true));
        _cleanupScopes.Add(new CleanupScopeItem(
            CleanupScopeKind.RevisionsAndComments,
            Strings.Meta_Group_Revisions,
            Strings.Scope_Revisions_Description,
            iconPack + "part2.png",
            isEnabled: true));
        _cleanupScopes.Add(new CleanupScopeItem(
            CleanupScopeKind.HiddenPathsAndResiduals,
            Strings.Meta_Group_Hidden,
            Strings.Scope_Hidden_Description,
            iconPack + "part3.png",
            isEnabled: true));

        foreach (var scope in _cleanupScopes)
            scope.PropertyChanged += CleanupScope_PropertyChanged;
    }

    /// <summary>Marks which strategy matches <see cref="StrategySettings.DefaultStrategyId"/>.</summary>
    private void ApplyDefaultFlags()
    {
        var defaultId = StrategySettings.DefaultStrategyId;
        foreach (var strategy in _strategies)
            strategy.IsDefault = string.Equals(strategy.Id, defaultId, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Shows detail fields and cleanup scopes for the selected strategy.</summary>
    private void ShowStrategyDetails(StrategyListItem item)
    {
        _selectedStrategy = item;
        foreach (var strategy in _strategies)
            strategy.IsSelected = ReferenceEquals(strategy, item);

        StrategyDetailTitleText.Text = item.Name;
        StrategyDetailIcon.Source = new BitmapImage(new Uri(item.IconPath, UriKind.Absolute));
        StrategyNameTextBox.Text = item.Name;
        StrategyDescriptionTextBox.Text = item.Description;

        if (item.IsPreset)
            ApplyScopesFromOptions(item, ScrubOptions.FromPresetId(item.Id));

        ApplyScopesToUi(item);
        UpdatePresetEditLock(item.IsPreset);
    }

    /// <summary>Locks name/description/scopes/actions for built-in presets; unlocks for custom strategies.</summary>
    private void UpdatePresetEditLock(bool isPreset)
    {
        var editable = !isPreset;
        foreach (var scope in _cleanupScopes)
            scope.IsEditable = editable;

        DeleteStrategyButton.IsEnabled = editable;
        CancelStrategyButton.IsEnabled = editable;
        SaveStrategyButton.IsEnabled = editable;

        StrategyNameTextBox.IsReadOnly = isPreset;
        StrategyDescriptionTextBox.IsReadOnly = isPreset;
        StrategyNameTextBox.IsTabStop = editable;
        StrategyDescriptionTextBox.IsTabStop = editable;
        StrategyNameTextBox.Cursor = editable ? Cursors.IBeam : Cursors.Arrow;
        StrategyDescriptionTextBox.Cursor = editable ? Cursors.IBeam : Cursors.Arrow;

        var fieldBg = editable
            ? (Brush)new BrushConverter().ConvertFromString("#FFFFFF")!
            : (Brush)new BrushConverter().ConvertFromString("#F8FAFC")!;
        StrategyNameFieldBorder.Background = fieldBg;
        StrategyDescriptionFieldBorder.Background = fieldBg;
    }

    /// <summary>Pushes strategy scope flags into the UI without writing back to the model.</summary>
    private void ApplyScopesToUi(StrategyListItem item)
    {
        _suppressScopeWriteBack = true;
        try
        {
            foreach (var scope in _cleanupScopes)
            {
                scope.IsEnabled = scope.Kind switch
                {
                    CleanupScopeKind.DocumentProperties => item.DocumentProperties,
                    CleanupScopeKind.RevisionsAndComments => item.RevisionsAndComments,
                    CleanupScopeKind.HiddenPathsAndResiduals => item.HiddenPathsAndResiduals,
                    _ => false,
                };
            }
        }
        finally
        {
            _suppressScopeWriteBack = false;
        }
    }

    /// <summary>Writes scope toggle changes back to the selected custom strategy.</summary>
    private void CleanupScope_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressScopeWriteBack || e.PropertyName != nameof(CleanupScopeItem.IsEnabled))
            return;
        if (_selectedStrategy is null || _selectedStrategy.IsPreset || sender is not CleanupScopeItem scope)
            return;

        switch (scope.Kind)
        {
            case CleanupScopeKind.DocumentProperties:
                _selectedStrategy.DocumentProperties = scope.IsEnabled;
                break;
            case CleanupScopeKind.RevisionsAndComments:
                _selectedStrategy.RevisionsAndComments = scope.IsEnabled;
                break;
            case CleanupScopeKind.HiddenPathsAndResiduals:
                _selectedStrategy.HiddenPathsAndResiduals = scope.IsEnabled;
                break;
        }
    }

    /// <summary>Selects a strategy from the sidebar list.</summary>
    private void StrategyListItem_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: StrategyListItem item })
            ShowStrategyDetails(item);
    }

    /// <summary>Creates a new custom strategy and focuses its name field.</summary>
    private void NewStrategyButton_Click(object sender, RoutedEventArgs e)
    {
        var created = StrategySettings.CreateCustomStrategy();
        var item = StrategyListItem.FromInfo(created);
        _strategies.Add(item);
        ApplyDefaultFlags();
        ShowStrategyDetails(item);
        StrategyNameTextBox.Focus();
        StrategyNameTextBox.SelectAll();
    }

    private void ImportStrategyButton_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Import strategy
    }

    private void ExportStrategyButton_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Export strategy
    }

    private void StrategyMoreButton_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Per-strategy overflow actions
        e.Handled = true;
    }

    /// <summary>Sets the selected strategy as the app default (persists custom edits first if needed).</summary>
    private void SetDefaultStrategyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedStrategy is null)
            return;

        // Persist custom edits first so the default does not point at an unsaved draft.
        if (!_selectedStrategy.IsPreset)
            PersistCustomStrategiesFromUi();

        StrategySettings.SetDefaultStrategyId(_selectedStrategy.Id);
        ApplyDefaultFlags();
    }

    /// <summary>Deletes the selected custom strategy after confirmation.</summary>
    private void DeleteStrategyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedStrategy is null || _selectedStrategy.IsPreset)
            return;

        var name = _selectedStrategy.Name;
        var result = MessageBox.Show(
            Strings.Format(Strings.StrategyPage_DeleteConfirm, name),
            Strings.StrategyPage_Delete,
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.OK)
            return;

        var id = _selectedStrategy.Id;
        if (!StrategySettings.DeleteCustomStrategy(id))
            return;

        var index = _strategies.IndexOf(_selectedStrategy);
        _strategies.Remove(_selectedStrategy);
        ApplyDefaultFlags();

        StrategyListItem? next = null;
        if (_strategies.Count > 0)
        {
            var nextIndex = Math.Clamp(index, 0, _strategies.Count - 1);
            next = _strategies[nextIndex];
        }

        if (next is not null)
            ShowStrategyDetails(next);
        else
            _selectedStrategy = null;
    }

    /// <summary>Reverts unsaved custom edits from the persisted strategy store.</summary>
    private void CancelStrategyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedStrategy is null || _selectedStrategy.IsPreset)
            return;

        var saved = StrategySettings.FindStrategy(_selectedStrategy.Id);
        if (saved is null)
            return;

        _selectedStrategy.Name = saved.Name;
        _selectedStrategy.Description = saved.Description;
        ApplyScopesFromOptions(_selectedStrategy, saved.Options);
        StrategyDetailTitleText.Text = saved.Name;
        StrategyNameTextBox.Text = saved.Name;
        StrategyDescriptionTextBox.Text = saved.Description;
        ApplyScopesToUi(_selectedStrategy);
    }

    /// <summary>Saves all custom strategies from the current UI state.</summary>
    private void SaveStrategyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedStrategy?.IsPreset == true)
            return;

        PersistCustomStrategiesFromUi();
        if (_selectedStrategy is not null)
        {
            StrategyDetailTitleText.Text = _selectedStrategy.Name;
            ApplyDefaultFlags();
        }
    }

    /// <summary>Writes name/description/scopes for customs into <see cref="StrategySettings"/>.</summary>
    private void PersistCustomStrategiesFromUi()
    {
        if (_selectedStrategy is { IsPreset: false })
        {
            var name = StrategyNameTextBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(name))
                name = Strings.Strategy_Custom_Name;
            _selectedStrategy.Name = name;
            _selectedStrategy.Description = StrategyDescriptionTextBox.Text?.Trim() ?? "";
        }

        var customs = _strategies
            .Where(s => !s.IsPreset)
            .Select(s => s.ToInfo())
            .ToList();
        StrategySettings.SaveCustomStrategies(customs);
    }
}

/// <summary>Which cleanup scope a toggle row represents.</summary>
internal enum CleanupScopeKind
{
    DocumentProperties,
    RevisionsAndComments,
    HiddenPathsAndResiduals,
}

/// <summary>Sidebar list row for one strategy (preset or custom).</summary>
internal sealed class StrategyListItem : INotifyPropertyChanged
{
    private bool _isSelected;
    private bool _isDefault;
    private string _name;
    private string _description;

    public StrategyListItem(
        string id,
        string name,
        string description,
        string iconPath,
        ScrubOptions options,
        bool isPreset = false)
    {
        Id = id;
        _name = name;
        _description = description;
        IconPath = iconPath;
        IsPreset = isPreset;
        DocumentProperties = options.DocumentProperties;
        RevisionsAndComments = options.RevisionsAndComments;
        HiddenPathsAndResiduals = options.HiddenPathsAndResiduals;
    }

    /// <summary>Creates a list item from persisted strategy info.</summary>
    public static StrategyListItem FromInfo(StrategyInfo info) =>
        new(info.Id, info.Name, info.Description, info.IconPath, info.Options, info.IsPreset);

    /// <summary>Converts this row back to persistable strategy info.</summary>
    public StrategyInfo ToInfo() => new()
    {
        Id = Id,
        Name = Name,
        Description = Description,
        IconPath = IconPath,
        IsPreset = IsPreset,
        Options = ToOptions(),
    };

    public string Id { get; }
    public string IconPath { get; }

    /// <summary>True for built-in presets that cannot be deleted or edited.</summary>
    public bool IsPreset { get; }

    public bool DocumentProperties { get; set; }
    public bool RevisionsAndComments { get; set; }
    public bool HiddenPathsAndResiduals { get; set; }

    public string Name
    {
        get => _name;
        set
        {
            if (_name == value)
                return;
            _name = value;
            OnPropertyChanged();
        }
    }

    public string Description
    {
        get => _description;
        set
        {
            if (_description == value)
                return;
            _description = value;
            OnPropertyChanged();
        }
    }

    /// <summary>True when this row is selected in the sidebar.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;
            _isSelected = value;
            OnPropertyChanged();
        }
    }

    /// <summary>True when this strategy is the app default.</summary>
    public bool IsDefault
    {
        get => _isDefault;
        set
        {
            if (_isDefault == value)
                return;
            _isDefault = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Builds scrub options from the three scope flags.</summary>
    public ScrubOptions ToOptions() => new()
    {
        DocumentProperties = DocumentProperties,
        RevisionsAndComments = RevisionsAndComments,
        HiddenPathsAndResiduals = HiddenPathsAndResiduals,
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>One cleanup-scope toggle row in the strategy detail panel.</summary>
internal sealed class CleanupScopeItem : INotifyPropertyChanged
{
    private bool _isEnabled;
    private bool _isEditable = true;

    public CleanupScopeItem(
        CleanupScopeKind kind,
        string name,
        string description,
        string iconPath,
        bool isEnabled)
    {
        Kind = kind;
        Name = name;
        Description = description;
        IconPath = iconPath;
        _isEnabled = isEnabled;
    }

    public CleanupScopeKind Kind { get; }
    public string Name { get; }
    public string Description { get; }
    public string IconPath { get; }

    /// <summary>Whether this scope is included for the selected strategy.</summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value)
                return;
            _isEnabled = value;
            OnPropertyChanged();
        }
    }

    /// <summary>False for presets so the user cannot toggle scopes.</summary>
    public bool IsEditable
    {
        get => _isEditable;
        set
        {
            if (_isEditable == value)
                return;
            _isEditable = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
