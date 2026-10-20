// SPDX-FileCopyrightText: 2026 PlainBytes Studio
// SPDX-License-Identifier: GPL-3.0-only
using System.Globalization;
using System.IO;
using System.Resources;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PlainBytes.Core.Catalog;
using PlainBytes.Core.Resources;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace PlainBytes.Core.Controls;

/// <summary>
/// Shared Fluent shell for PlainBytes tools: navigation rail, title bar, overlays, and fixed bottom pages.
/// </summary>
public partial class ShellWindow : FluentWindow
{
    /// <summary>Default window title from shared resources (suite brand).</summary>
    public static string BaseTitle => Strings.App_Title;

    /// <summary>Bottom rail nav id: more PlainBytes tools (product matrix).</summary>
    public const string NavIdMatrix = "matrix";

    /// <summary>Bottom rail nav id: support PlainBytes (tip tiers).</summary>
    public const string NavIdSupport = "support";

    private static readonly Color BrandPrimary = (Color)ColorConverter.ConvertFromString("#2563EB")!;

    // TEMP: debug culture list (aligned with SatelliteResourceLanguages)
    private static readonly (string Culture, string Label)[] DebugCultures =
    [
        ("en-US", "English"),
        ("zh-Hans", "简体中文"),
        ("zh-Hant", "繁體中文"),
        ("ja", "日本語"),
        ("ko", "한국어"),
        ("de", "Deutsch"),
        ("fr", "Français"),
        ("ru", "Русский"),
        ("it", "Italiano"),
        ("es", "Español"),
        ("pt", "Português"),
        ("nl", "Nederlands"),
        ("ar", "العربية"),
    ];

    private const double DesignWidth = 1100;
    private const double DesignHeight = 640;
    private const double WorkAreaMargin = 8;

    private SupportView _supportView = new();
    private SuiteProductsView _suiteProductsView = new();
    private string? _currentProductId;
    private string? _toolName;
    private string? _titleSuffix;
    private Action? _editionBadgeClickHandler;
    private string? _selectedNavId;
    private readonly List<(string Id, Border Indicator, Action OnClick)> _topNav = [];

    /// <summary>
    /// TEMP: raised after debug UI culture change; host should rebuild localized pages and top navigation.
    /// </summary>
    public event Action? CultureChanged;

    public ShellWindow()
    {
        InitializeComponent();
        ApplyLayoutDirection();
        ApplicationAccentColorManager.Apply(BrandPrimary, ApplicationTheme.Light);
        AppTitleBar.Loaded += OnTitleBarLoaded;
        AppTitleBar.SizeChanged += (_, _) => CenterTitleBarButtons();
        Closing += (_, _) => SaveWindowState();
    }

    /// <summary>
    /// Skip walking the full visual tree for UI Automation.
    /// Opening any Popup/ToolTip/ComboBox otherwise forces AutomationPeer creation
    /// for every control (WPF #5807), freezing complex pages for 1–2+ seconds.
    /// </summary>
    protected override AutomationPeer OnCreateAutomationPeer() =>
        new ShellWindowAutomationPeer(this);

    /// <summary>
    /// Falls back to Acrylic on Windows 10 when Mica is unavailable,
    /// then applies saved or first-launch window bounds.
    /// </summary>
    protected override void OnSourceInitialized(EventArgs e)
    {
        if (!WindowBackdrop.IsSupported(WindowBackdropType.Mica))
            WindowBackdropType = WindowBackdropType.Acrylic;

        base.OnSourceInitialized(e);
        ApplyDesignMinimumsCappedToWorkArea();
        RestoreWindowState();
        if (WindowState == WindowState.Normal)
            EnsureBoundsVisibleInWorkArea();
    }

    /// <summary>Window peer that reports no children, avoiding tree-wide peer build.</summary>
    private sealed class ShellWindowAutomationPeer(FrameworkElement owner)
        : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetNameCore() => nameof(ShellWindow);

        protected override AutomationControlType GetAutomationControlTypeCore() =>
            AutomationControlType.Window;

        protected override List<AutomationPeer> GetChildrenCore() => [];
    }

    /// <summary>
    /// First launch: maximize. Later launches: restore saved bounds and state.
    /// </summary>
    private void RestoreWindowState()
    {
        var key = GetWindowStateStorageKey();
        if (!ShellWindowStateStore.TryLoad(key, out var data))
        {
            ApplyFirstLaunchRestoreBounds();
            WindowState = WindowState.Maximized;
            return;
        }

        WindowStartupLocation = WindowStartupLocation.Manual;

        var savedState = Enum.IsDefined(typeof(WindowState), data.WindowState)
            ? (WindowState)data.WindowState
            : WindowState.Normal;

        if (savedState == WindowState.Maximized)
        {
            ApplySavedBounds(data);
            EnsureBoundsVisibleInWorkArea();
            WindowState = WindowState.Maximized;
            return;
        }

        if (savedState == WindowState.Minimized)
            savedState = WindowState.Normal;

        ApplySavedBounds(data);
        WindowState = WindowState.Normal;
    }

    /// <summary>Persists bounds and state on exit.</summary>
    private void SaveWindowState()
    {
        var key = GetWindowStateStorageKey();
        Rect bounds;
        WindowState state;

        if (WindowState == WindowState.Normal)
        {
            bounds = new Rect(Left, Top, Width, Height);
            state = WindowState.Normal;
        }
        else
        {
            bounds = RestoreBounds;
            state = WindowState == WindowState.Minimized ? WindowState.Normal : WindowState;
        }

        ShellWindowStateStore.Save(key, new ShellWindowStateStore.Snapshot
        {
            Left = bounds.Left,
            Top = bounds.Top,
            Width = bounds.Width,
            Height = bounds.Height,
            WindowState = (int)state,
        });
    }

    /// <summary>Per-tool key from <see cref="SetCurrentProductId"/>; falls back to suite default.</summary>
    private string GetWindowStateStorageKey() =>
        string.IsNullOrWhiteSpace(_currentProductId) ? "Default" : _currentProductId.Trim();

    /// <summary>
    /// Caps XAML design minimums (1100×640) to the current monitor work area.
    /// At 175% on 1080p this becomes ~1081×561, preventing MinHeight/MinWidth ping-pong with saved state.
    /// </summary>
    private void ApplyDesignMinimumsCappedToWorkArea()
    {
        var (maxW, maxH) = GetWorkAreaMaxSize();
        MinWidth = Math.Min(DesignWidth, maxW);
        MinHeight = Math.Min(DesignHeight, maxH);
        if (Width > maxW)
            Width = maxW;
        if (Height > maxH)
            Height = maxH;
    }

    /// <summary>Applies persisted bounds clamped to design minimums and work area.</summary>
    private void ApplySavedBounds(ShellWindowStateStore.Snapshot data)
    {
        WindowStartupLocation = WindowStartupLocation.Manual;
        var (maxW, maxH) = GetWorkAreaMaxSize();
        Width = Math.Clamp(data.Width, MinWidth, maxW);
        Height = Math.Clamp(data.Height, MinHeight, maxH);
        Left = data.Left;
        Top = data.Top;
    }

    /// <summary>
    /// First launch only: fit default XAML size to the work area and center once,
    /// so WPF records valid <see cref="Window.RestoreBounds"/> before maximizing.
    /// Does not run on later maximize/restore cycles.
    /// </summary>
    private void ApplyFirstLaunchRestoreBounds()
    {
        WindowStartupLocation = WindowStartupLocation.Manual;
        FitSizeToWorkArea();

        var work = SystemParameters.WorkArea;
        Left = work.Left + (work.Width - Width) / 2;
        Top = work.Top + (work.Height - Height) / 2;
        Left = Math.Clamp(Left, work.Left + WorkAreaMargin, work.Right - WorkAreaMargin - Width);
        Top = Math.Clamp(Top, work.Top + WorkAreaMargin, work.Bottom - WorkAreaMargin - Height);
    }

    /// <summary>
    /// After loading saved Normal bounds: shrink if too large, nudge only when off-screen.
    /// Preserves saved position when the window is already fully visible.
    /// </summary>
    private void EnsureBoundsVisibleInWorkArea()
    {
        if (WindowState == WindowState.Maximized)
            return;

        FitSizeToWorkArea();

        var work = SystemParameters.WorkArea;

        if (Left + Width > work.Right - WorkAreaMargin)
            Left = work.Right - WorkAreaMargin - Width;
        if (Top + Height > work.Bottom - WorkAreaMargin)
            Top = work.Bottom - WorkAreaMargin - Height;
        if (Left < work.Left + WorkAreaMargin)
            Left = work.Left + WorkAreaMargin;
        if (Top < work.Top + WorkAreaMargin)
            Top = work.Top + WorkAreaMargin;
    }

    /// <summary>Maximum client size that fits the primary work area (minus margin).</summary>
    private static (double maxW, double maxH) GetWorkAreaMaxSize()
    {
        var work = SystemParameters.WorkArea;
        var maxW = Math.Max(320, work.Width - WorkAreaMargin * 2);
        var maxH = Math.Max(240, work.Height - WorkAreaMargin * 2);
        return (maxW, maxH);
    }

    /// <summary>Clamps <see cref="MinWidth"/>, <see cref="MinHeight"/>, <see cref="Width"/>, and <see cref="Height"/> to the work area.</summary>
    private void FitSizeToWorkArea()
    {
        var (maxW, maxH) = GetWorkAreaMaxSize();

        if (MinWidth > maxW)
            MinWidth = maxW;
        if (MinHeight > maxH)
            MinHeight = maxH;
        if (Width > maxW)
            Width = maxW;
        if (Height > maxH)
            Height = maxH;
    }

    /// <summary>
    /// Optional gate before replacing main content. Return false to abort.
    /// Invoked on the UI thread; may await modal UI.
    /// </summary>
    public Func<FrameworkElement, Task<bool>>? ConfirmBeforeContentChangeAsync { get; set; }

    /// <summary>Raised when <see cref="SetContent"/> was aborted by the confirm gate.</summary>
    public event EventHandler? ContentChangeCancelled;

    /// <summary>Replaces the main content area with the given tool page.</summary>
    public void SetContent(FrameworkElement content) =>
        _ = SetContentAsync(content);

    /// <summary>Replaces content after optional confirm; returns whether the change applied.</summary>
    public async Task<bool> SetContentAsync(FrameworkElement content)
    {
        if (ConfirmBeforeContentChangeAsync is not null
            && ContentArea.Content is FrameworkElement current
            && !ReferenceEquals(current, content))
        {
            if (!await ConfirmBeforeContentChangeAsync(content).ConfigureAwait(true))
            {
                ContentChangeCancelled?.Invoke(this, EventArgs.Empty);
                return false;
            }
        }

        ContentArea.Content = content;
        return true;
    }

    /// <summary>Current main content element, if any.</summary>
    public FrameworkElement? CurrentContent => ContentArea.Content as FrameworkElement;

    /// <summary>
    /// Shows a full-window modal overlay and blocks hit-testing on chrome and body beneath it.
    /// </summary>
    public void ShowOverlay(UIElement overlay)
    {
        if (overlay is FrameworkElement fe)
        {
            fe.HorizontalAlignment = HorizontalAlignment.Stretch;
            fe.VerticalAlignment = VerticalAlignment.Stretch;
        }

        OverlayHost.Content = overlay;
        OverlayHost.Visibility = Visibility.Visible;
        AppTitleBar.IsHitTestVisible = false;
        ShellBody.IsHitTestVisible = false;
        TopToastHost.IsHitTestVisible = false;
    }

    /// <summary>Dismisses the full-window overlay and restores hit-testing.</summary>
    public void HideOverlay()
    {
        OverlayHost.Content = null;
        OverlayHost.Visibility = Visibility.Collapsed;
        AppTitleBar.IsHitTestVisible = true;
        ShellBody.IsHitTestVisible = true;
        TopToastHost.IsHitTestVisible = true;
    }

    /// <summary>Shows a toast in the top-right host (e.g. rate-and-review bar).</summary>
    public void ShowTopToast(UIElement toast)
    {
        if (toast is FrameworkElement fe)
            fe.FlowDirection = ShellBody.FlowDirection;

        if (!ReferenceEquals(TopToastHost.Content, toast))
            TopToastHost.Content = toast;
        TopToastHost.Visibility = Visibility.Visible;
    }

    /// <summary>Hides the top-right toast host.</summary>
    public void HideTopToast()
    {
        TopToastHost.Content = null;
        TopToastHost.Visibility = Visibility.Collapsed;
    }

    /// <summary>Sets the tool segment of the window title (e.g. ImageConverter).</summary>
    public void SetToolName(string? toolName)
    {
        _toolName = toolName;
        ApplyWindowTitle();
    }

    /// <summary>
    /// Sets an optional title suffix shown as a badge (e.g. trial days remaining).
    /// </summary>
    /// <param name="suffix">Badge text; null or empty clears the badge.</param>
    /// <param name="badgeClickable">When true, the badge is clickable.</param>
    /// <param name="onBadgeClick">Invoked when the user clicks the badge.</param>
    public void SetTitleSuffix(string? suffix, bool badgeClickable = false, Action? onBadgeClick = null)
    {
        _titleSuffix = string.IsNullOrWhiteSpace(suffix) ? null : suffix.Trim();
        _editionBadgeClickHandler = badgeClickable ? onBadgeClick : null;
        ApplyWindowTitle();
    }

    /// <summary>Updates title bar text, window Title, and edition badge visibility.</summary>
    private void ApplyWindowTitle()
    {
        var baseTitle = string.IsNullOrWhiteSpace(_toolName)
            ? BaseTitle
            : $"{BaseTitle} - {_toolName}";

        TitleTextBlock.Text = baseTitle;
        AppTitleBar.Title = string.IsNullOrWhiteSpace(_titleSuffix)
            ? baseTitle
            : $"{baseTitle} {_titleSuffix}";
        Title = AppTitleBar.Title;

        if (string.IsNullOrWhiteSpace(_titleSuffix))
        {
            EditionBadgeButton.Visibility = Visibility.Collapsed;
            EditionBadgeButton.Content = null;
            EditionBadgeButton.IsEnabled = false;
            return;
        }

        EditionBadgeButton.Content = _titleSuffix;
        EditionBadgeButton.Visibility = Visibility.Visible;
        EditionBadgeButton.IsEnabled = _editionBadgeClickHandler is not null;
        EditionBadgeButton.Cursor = _editionBadgeClickHandler is null
            ? System.Windows.Input.Cursors.Arrow
            : System.Windows.Input.Cursors.Hand;
    }

    /// <summary>Edition badge click handler wired from <see cref="SetTitleSuffix"/>.</summary>
    private void EditionBadgeButton_Click(object sender, RoutedEventArgs e) =>
        _editionBadgeClickHandler?.Invoke();

    /// <summary>
    /// Sets the running product id; updates title bar icon and matrix "installed" badge.
    /// </summary>
    public void SetCurrentProductId(string? productId)
    {
        _currentProductId = productId;
        _suiteProductsView.CurrentProductId = productId;
        ApplyProductBranding(productId);
    }

    /// <summary>Sets the title bar logo; null resets to the suite default icon.</summary>
    public void SetTitleIcon(ImageSource? icon) =>
        TitleBarIcon.Source = icon ?? LoadPackImage(SuiteCatalog.DefaultIconPath);

    /// <summary>Applies per-product title icon from <see cref="SuiteCatalog"/>.</summary>
    private void ApplyProductBranding(string? productId)
    {
        var product = SuiteCatalog.FindById(productId);
        SetTitleIcon(product is null ? null : LoadPackImage(product.IconPath));
    }

    /// <summary>Loads a bitmap from a pack URI in PlainBytes.Core.</summary>
    private static ImageSource LoadPackImage(string packUri)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri(packUri, UriKind.Absolute);
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();
        return image;
    }

    /// <summary>
    /// Rebuilds top-of-rail navigation from tool-provided items. Bottom entries (language, matrix, support) are unchanged.
    /// </summary>
    public void ConfigureTopNavigation(IEnumerable<ShellNavItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var previous = _selectedNavId;
        TopNavHost.Children.Clear();
        _topNav.Clear();

        foreach (var item in items)
        {
            var (slot, indicator) = CreateTopNavSlot(item);
            TopNavHost.Children.Add(slot);
            _topNav.Add((item.Id, indicator, item.OnClick));
        }

        ApplyIndicatorLayout();

        if (previous is not null
            && (_topNav.Exists(t => t.Id == previous)
                || previous is NavIdMatrix or NavIdSupport))
            SelectNav(previous);
        else
            SelectNav(null);
    }

    /// <summary>Updates the selection indicator for a top nav id or bottom matrix/support id.</summary>
    public void SelectNav(string? id)
    {
        _selectedNavId = id;

        foreach (var (_, indicator, _) in _topNav)
            indicator.Visibility = Visibility.Collapsed;

        MatrixNavIndicator.Visibility = Visibility.Collapsed;
        SupportNavIndicator.Visibility = Visibility.Collapsed;

        if (id is null)
            return;

        if (id == NavIdMatrix)
        {
            MatrixNavIndicator.Visibility = Visibility.Visible;
            return;
        }

        if (id == NavIdSupport)
        {
            SupportNavIndicator.Visibility = Visibility.Visible;
            return;
        }

        foreach (var (navId, indicator, _) in _topNav)
        {
            if (navId == id)
            {
                indicator.Visibility = Visibility.Visible;
                break;
            }
        }
    }

    /// <summary>Clears rail selection indicators without changing content.</summary>
    public void ClearNavigationSelection() => SelectNav(null);

    /// <summary>Selects and invokes the click handler for a configured top nav item, if present.</summary>
    public void ActivateTopNav(string id)
    {
        foreach (var (navId, _, onClick) in _topNav)
        {
            if (navId != id)
                continue;

            SelectNav(id);
            onClick();
            return;
        }
    }

    /// <summary>Activates the top nav item with id <c>help</c>, if configured.</summary>
    public void ShowHelp() => ActivateTopNav("help");

    /// <summary>
    /// Loads a navigation rail icon from PlainBytes.Core embedded resources.
    /// </summary>
    /// <param name="fileName">File name under Assets/Images (case-insensitive).</param>
    /// <remarks>
    /// Uses ResourceManager instead of pack URI because pack://application resolves to the host exe assembly.
    /// </remarks>
    public static ImageSource LoadCoreNavIcon(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var asm = typeof(ShellWindow).Assembly;
        var manager = new ResourceManager(asm.GetName().Name + ".g", asm);
        var key = "assets/images/" + fileName.ToLowerInvariant();
        using var stream = manager.GetStream(key)
            ?? throw new IOException($"Cannot locate resource '{key}'.");

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    /// <summary>Builds one top-rail slot: selection indicator + icon button.</summary>
    private (Grid Slot, Border Indicator) CreateTopNavSlot(ShellNavItem item)
    {
        var indicator = new Border
        {
            Width = 3,
            Height = 16,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 0, 0),
            Background = (Brush)FindResource("PB.Brushes.Primary"),
            CornerRadius = new CornerRadius(2),
            Visibility = Visibility.Collapsed,
        };

        var button = new Wpf.Ui.Controls.Button
        {
            Style = (Style)FindResource("NavigationRailButtonStyle"),
            ToolTip = item.ToolTip,
            Content = new System.Windows.Controls.Image
            {
                Width = 20,
                Height = 20,
                Source = item.Icon,
                SnapsToDevicePixels = true,
            },
        };
        RenderOptions.SetBitmapScalingMode(
            (DependencyObject)button.Content,
            BitmapScalingMode.HighQuality);

        var id = item.Id;
        var onClick = item.OnClick;
        button.Click += (_, _) =>
        {
            SelectNav(id);
            onClick();
        };

        var slot = new Grid
        {
            Width = 48,
            Height = 48,
            Background = Brushes.Transparent,
        };
        slot.Children.Add(indicator);
        slot.Children.Add(button);
        return (slot, indicator);
    }

    /// <summary>TEMP: opens debug language context menu on the hidden language rail button.</summary>
    private void LanguageNavButton_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();
        var current = CultureInfo.CurrentUICulture.Name;

        foreach (var (culture, label) in DebugCultures)
        {
            var item = new System.Windows.Controls.MenuItem
            {
                Header = label,
                Tag = culture,
                IsChecked = string.Equals(current, culture, StringComparison.OrdinalIgnoreCase)
                    || current.StartsWith(culture + "-", StringComparison.OrdinalIgnoreCase)
                    || (culture == "en-US" && current.StartsWith("en", StringComparison.OrdinalIgnoreCase)),
            };
            item.Click += LanguageMenuItem_Click;
            menu.Items.Add(item);
        }

        menu.PlacementTarget = LanguageNavButton;
        menu.Placement = CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft
            ? System.Windows.Controls.Primitives.PlacementMode.Left
            : System.Windows.Controls.Primitives.PlacementMode.Right;
        LanguageNavButton.ContextMenu = menu;
        menu.IsOpen = true;
    }

    /// <summary>Applies culture selected from the debug language menu.</summary>
    private void LanguageMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.MenuItem { Tag: string cultureName })
            return;

        ApplyDebugCulture(cultureName);
    }

    /// <summary>
    /// TEMP: switches UI culture, refreshes shell strings, and notifies the host to rebuild tool views.
    /// </summary>
    private void ApplyDebugCulture(string cultureName)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        ApplyLayoutDirection();
        RefreshChromeStrings();

        _supportView = new SupportView();
        _suiteProductsView = new SuiteProductsView { CurrentProductId = _currentProductId };

        CultureChanged?.Invoke();

        // Re-show fixed shell pages with fresh localized instances
        if (_selectedNavId == NavIdMatrix)
            ShowSuiteProducts();
        else if (_selectedNavId == NavIdSupport)
            ShowSupport();
    }

    /// <summary>Applies RTL/LTR to body and overlay; keeps title bar LTR.</summary>
    private void ApplyLayoutDirection()
    {
        var rtl = CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;
        var dir = rtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

        ShellBody.FlowDirection = dir;
        OverlayHost.FlowDirection = dir;
        if (TopToastHost.Content is FrameworkElement toast)
            toast.FlowDirection = dir;

        AppTitleBar.FlowDirection = FlowDirection.LeftToRight;

        ApplyIndicatorLayout();
    }

    /// <summary>Mirrors nav selection bar to the right edge in RTL layouts.</summary>
    private void ApplyIndicatorLayout()
    {
        var rtl = CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;
        var barAlign = rtl ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        var barMargin = rtl ? new Thickness(0, 0, 4, 0) : new Thickness(4, 0, 0, 0);

        foreach (var (_, indicator, _) in _topNav)
        {
            indicator.HorizontalAlignment = barAlign;
            indicator.Margin = barMargin;
        }

        foreach (var bar in new[] { MatrixNavIndicator, SupportNavIndicator })
        {
            bar.HorizontalAlignment = barAlign;
            bar.Margin = barMargin;
        }
    }

    /// <summary>Refreshes fixed rail tooltips and window title after culture change.</summary>
    private void RefreshChromeStrings()
    {
        MatrixNavButton.ToolTip = Strings.Nav_Matrix;
        SupportNavButton.ToolTip = Strings.Nav_Support;
        LanguageNavButton.ToolTip = $"Language / 语言 ({CultureInfo.CurrentUICulture.Name})";
        SetToolName(_toolName);
    }

    private void MatrixNavButton_Click(object sender, RoutedEventArgs e) => ShowSuiteProducts();

    /// <summary>Navigates to the suite product matrix page.</summary>
    public void ShowSuiteProducts()
    {
        SelectNav(NavIdMatrix);
        _suiteProductsView.CurrentProductId = _currentProductId;
        SetContent(_suiteProductsView);
    }

    private void SupportNavButton_Click(object sender, RoutedEventArgs e) => ShowSupport();

    /// <summary>Navigates to the support / tip tiers page.</summary>
    public void ShowSupport()
    {
        SelectNav(NavIdSupport);
        _supportView.RefreshSupporterState();
        SetContent(_supportView);
    }

    /// <summary>Defers title bar button centering until the TitleBar template is loaded.</summary>
    private void OnTitleBarLoaded(object sender, RoutedEventArgs e)
    {
        Dispatcher.BeginInvoke(CenterTitleBarButtons, DispatcherPriority.Loaded);
    }

    /// <summary>Vertically centers caption buttons within the custom title bar chrome.</summary>
    private void CenterTitleBarButtons()
    {
        var buttons = FindVisualChildren<TitleBarButton>(AppTitleBar).ToList();
        if (buttons.Count == 0 || AppTitleBar.ActualHeight <= 0)
            return;

        foreach (var button in buttons)
        {
            button.Width = 46;
            button.Height = 46;
            button.VerticalAlignment = VerticalAlignment.Center;
            button.Margin = new Thickness(0);
        }

        if (buttons[0].Parent is not FrameworkElement host)
            return;

        host.VerticalAlignment = VerticalAlignment.Center;
        host.HorizontalAlignment = HorizontalAlignment.Right;
        host.ClearValue(FrameworkElement.HeightProperty);
        host.Margin = new Thickness(0);

        if (VisualTreeHelper.GetParent(host) is FrameworkElement hostParent
            && !ReferenceEquals(hostParent, AppTitleBar)
            && hostParent.Name != "PART_MainGrid")
        {
            hostParent.VerticalAlignment = VerticalAlignment.Center;
            hostParent.ClearValue(FrameworkElement.HeightProperty);
        }
    }

    /// <summary>Depth-first search for visual children of type <typeparamref name="T"/>.</summary>
    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent)
        where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
                yield return match;

            foreach (var nested in FindVisualChildren<T>(child))
                yield return nested;
        }
    }
}
