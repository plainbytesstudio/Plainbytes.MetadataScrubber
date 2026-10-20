// SPDX-FileCopyrightText: 2026 PlainBytes Studio
// SPDX-License-Identifier: GPL-3.0-only

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using PlainBytes.MetadataScrubber.Resources;

namespace PlainBytes.MetadataScrubber.Controls;

/// <summary>One failed file row shown in the expandable failure list.</summary>
internal sealed class OverlayFailItem
{
    public OverlayFailItem(string fileName, string reason, bool isLast = false)
    {
        FileName = fileName;
        Reason = reason;
        IsLast = isLast;
    }

    /// <summary>Base file name only (no path).</summary>
    public string FileName { get; }

    /// <summary>Localized, user-friendly failure reason.</summary>
    public string Reason { get; }

    /// <summary>True for the last row to suppress trailing separator styling.</summary>
    public bool IsLast { get; }
}

/// <summary>
/// Bottom sheet overlay for batch metadata scrub: progress, cancel, and completion summary with optional failure details.
/// </summary>
public partial class ScrubProgressOverlay : UserControl
{
    private const string SuccessIcon = "pack://application:,,,/PlainBytes.Core;component/Assets/Images/success.png";
    private const string WarningIcon = "pack://application:,,,/PlainBytes.Core;component/Assets/Images/warning.png";
    private const string ErrorIcon = "pack://application:,,,/PlainBytes.Core;component/Assets/Images/error.png";
    private const double CollapsedHeight = 86;
    private const double ExpandedMaxHeight = 260;
    private const double BottomGap = 10;
    private const double SeparatorBlock = 21;
    private const double FailRowHeight = 28;

    /// <summary>Raised after the sheet finishes its close animation and is collapsed.</summary>
    public event Action? DismissRequested;

    private int _total;
    private int _current;
    private bool _isDismissing;
    private bool _isComplete;
    private bool _failListVisible;
    private CancellationTokenSource? _cts;

    public ScrubProgressOverlay()
    {
        InitializeComponent();
        IsVisibleChanged += OnIsVisibleChanged;
    }

    /// <summary>Cancellation token tied to the active scrub session; valid after <see cref="Show"/>.</summary>
    public CancellationToken CancellationToken =>
        _cts?.Token ?? CancellationToken.None;

    /// <summary>Opens the sheet for a new batch and resets UI to working state.</summary>
    /// <param name="totalFiles">Expected file count (minimum 1 for display).</param>
    public void Show(int totalFiles)
    {
        _isDismissing = false;
        _isComplete = false;
        _failListVisible = false;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        _total = Math.Max(totalFiles, 1);
        _current = 0;
        TitleText.Text = Strings.Overlay_Working;
        FileNameText.Text = string.Empty;
        ActionButton.Content = Strings.Common_Cancel;
        ActionButton.Visibility = Visibility.Visible;
        ViewReasonButton.Visibility = Visibility.Collapsed;
        Ring.Visibility = Visibility.Visible;
        StatusImage.Visibility = Visibility.Collapsed;
        BarTrack.Visibility = Visibility.Visible;
        CountText.Visibility = Visibility.Visible;
        HideFailList();
        FailList.ItemsSource = null;
        ResetTopAccent();
        UpdateProgress(0, 0, _total);
        Visibility = Visibility.Visible;
        AnimateSheet(open: true);
        // Keep keyboard focus on the action button so Alt key tips do not highlight the page host.
        Dispatcher.BeginInvoke(new Action(() => ActionButton.Focus()),
            System.Windows.Threading.DispatcherPriority.Input);
    }

    /// <summary>Closes the sheet and cancels the active scrub session.</summary>
    public void Hide()
    {
        BeginDismiss(cancel: true);
    }

    /// <summary>Updates ring, bar, count, and optional current file name while work is in progress.</summary>
    public void UpdateProgress(double percent, int current, int total, string? currentFile = null)
    {
        if (_isComplete)
            return;

        _current = current;
        _total = Math.Max(total, 1);
        var pct = Math.Clamp(percent, 0, 100);
        Ring.Progress = pct;
        CountText.Text = $"{_current} / {_total}";
        if (!string.IsNullOrWhiteSpace(currentFile))
            FileNameText.Text = System.IO.Path.GetFileName(currentFile);
        UpdateBarWidth(pct);
    }

    /// <summary>
    /// Switches to completion UI: status icon, summary text, and optional "view reasons" for partial/total failures.
    /// </summary>
    internal void Complete(int successCount, int failCount, IReadOnlyList<ScrubFileResult>? failedResults = null)
    {
        if (_isDismissing)
            return;

        _isComplete = true;
        successCount = Math.Max(successCount, 0);
        failCount = Math.Max(failCount, 0);

        TitleText.Text = Strings.Overlay_Complete;
        BarTrack.Visibility = Visibility.Collapsed;
        CountText.Visibility = Visibility.Collapsed;
        Ring.Visibility = Visibility.Collapsed;
        StatusImage.Visibility = Visibility.Visible;
        ActionButton.Content = Strings.Overlay_Done;
        HideFailList();

        var failItems = (failedResults ?? [])
            .Where(r => !r.Success)
            .Select(r => new OverlayFailItem(
                System.IO.Path.GetFileName(r.Path),
                FormatFailReason(r.Error)))
            .ToList();
        if (failItems.Count > 0)
            failItems[^1] = new OverlayFailItem(failItems[^1].FileName, failItems[^1].Reason, isLast: true);
        FailList.ItemsSource = failItems;

        if (failCount == 0)
        {
            FileNameText.Text = Strings.Format(Strings.Overlay_SuccessCount, successCount);
            StatusImage.Source = LoadIcon(SuccessIcon);
            ViewReasonButton.Visibility = Visibility.Collapsed;
            ActionButton.Visibility = Visibility.Visible;
            SetTopAccent("#20c997");
        }
        else if (successCount == 0)
        {
            TitleText.Text = Strings.Overlay_Failed;
            FileNameText.Text = Strings.Format(Strings.Overlay_AllFailed, failCount);
            StatusImage.Source = LoadIcon(ErrorIcon);
            ViewReasonButton.Visibility = Visibility.Visible;
            ActionButton.Visibility = Visibility.Visible;
            SetTopAccent("#fa5252");
        }
        else
        {
            FileNameText.Text = Strings.Format(Strings.Overlay_Partial, successCount, failCount);
            StatusImage.Source = LoadIcon(WarningIcon);
            ViewReasonButton.Visibility = Visibility.Visible;
            ActionButton.Visibility = Visibility.Visible;
            SetTopAccent("#fcc419");
        }
    }

    /// <summary>Applies a colored top accent strip and adjusts sheet border for completion state.</summary>
    private void SetTopAccent(string hex)
    {
        TopAccent.Background = (Brush)new BrushConverter().ConvertFrom(hex)!;
        TopAccent.Visibility = Visibility.Visible;
        SheetPanel.BorderThickness = new Thickness(1, 0, 1, 1);
    }

    /// <summary>Restores default sheet border without completion accent.</summary>
    private void ResetTopAccent()
    {
        TopAccent.Visibility = Visibility.Collapsed;
        SheetPanel.BorderThickness = new Thickness(1);
        SheetPanel.BorderBrush = (Brush)new BrushConverter().ConvertFrom("#E6E8EB")!;
    }

    /// <summary>Expands the sheet to show the failure list with height capped by window and row count.</summary>
    private void ShowFailList()
    {
        if (_failListVisible)
            return;

        _failListVisible = true;
        FailSeparator.Visibility = Visibility.Visible;
        FailListScroll.Visibility = Visibility.Visible;

        var listContent = FailRowHeight * (1 + FailList.Items.Count);
        var maxByWindow = ActualHeight > BottomGap
            ? ActualHeight - BottomGap
            : ExpandedMaxHeight;
        var maxPanel = Math.Min(ExpandedMaxHeight, maxByWindow);
        var maxList = Math.Max(FailRowHeight, maxPanel - CollapsedHeight - SeparatorBlock);
        var needsScroll = listContent > maxList;
        var listHeight = needsScroll ? maxList : listContent;

        FailListScroll.Height = listHeight;
        FailListScroll.VerticalScrollBarVisibility = needsScroll
            ? ScrollBarVisibility.Auto
            : ScrollBarVisibility.Hidden;
        SheetPanel.Height = CollapsedHeight + SeparatorBlock + listHeight;
        SheetPanel.Margin = new Thickness(0, 0, 0, BottomGap);
    }

    /// <summary>Collapses the failure list and restores the compact sheet height.</summary>
    private void HideFailList()
    {
        _failListVisible = false;
        FailSeparator.Visibility = Visibility.Collapsed;
        FailListScroll.Visibility = Visibility.Collapsed;
        FailListScroll.Height = double.NaN;
        FailListScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
        SheetPanel.Height = CollapsedHeight;
        SheetPanel.Margin = new Thickness(0, 0, 0, BottomGap);
    }

    /// <summary>Maps raw exception text to a localized fail reason for the overlay list.</summary>
    private static string FormatFailReason(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
            return Strings.FailReason_Generic;

        var text = error.Trim();
        if (ContainsAny(text, "being used by another process", Strings.Error_Occupied, Strings.FailReason_Occupied))
            return Strings.FailReason_Occupied;
        if (ContainsAny(text, "NotSupported", Strings.Error_UnsupportedType, Strings.FailReason_Unsupported))
            return Strings.FailReason_Unsupported;
        if (ContainsAny(text, "FileNotFound", Strings.Error_NotFound, Strings.FailReason_NotFound))
            return Strings.FailReason_NotFound;
        if (ContainsAny(text, "Unauthorized", "Access is denied", Strings.Error_AccessDenied, Strings.Error_WriteDenied, Strings.FailReason_AccessDenied))
            return Strings.FailReason_AccessDenied;

        return Strings.FailReason_Generic;
    }

    /// <summary>Returns true when <paramref name="text"/> contains any non-empty token (case-insensitive).</summary>
    private static bool ContainsAny(string text, params string[] tokens)
    {
        foreach (var token in tokens)
        {
            if (!string.IsNullOrEmpty(token)
                && text.Contains(token, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>Loads a frozen pack-URI bitmap for completion status icons.</summary>
    private static BitmapImage LoadIcon(string packUri)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri(packUri, UriKind.Absolute);
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();
        return image;
    }

    /// <summary>Sets linear progress bar fill width from percentage.</summary>
    private void UpdateBarWidth(double percent)
    {
        var pct = Math.Clamp(percent, 0, 100);
        BarFillColumn.Width = new GridLength(pct, GridUnitType.Star);
        BarRestColumn.Width = new GridLength(100 - pct, GridUnitType.Star);
    }

    /// <summary>Plays close animation, optionally cancels work, then hides and raises <see cref="DismissRequested"/>.</summary>
    private void BeginDismiss(bool cancel)
    {
        if (_isDismissing)
            return;

        _isDismissing = true;
        if (cancel)
            _cts?.Cancel();
        AnimateSheet(open: false, onCompleted: () =>
        {
            Visibility = Visibility.Collapsed;
            DismissRequested?.Invoke();
        });
    }

    /// <summary>Slides the bottom sheet in or out with a quadratic ease.</summary>
    private void AnimateSheet(bool open, Action? onCompleted = null)
    {
        var from = open ? 80 : 0;
        var to = open ? 0 : 80;
        var anim = new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(280))
        {
            EasingFunction = new QuadraticEase { EasingMode = open ? EasingMode.EaseOut : EasingMode.EaseIn },
        };
        if (onCompleted != null)
            anim.Completed += (_, _) => onCompleted();
        SheetTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, anim);
    }

    /// <summary>Refreshes bar width when the overlay becomes visible mid-run.</summary>
    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible && !_isComplete)
            UpdateProgress(Ring.Progress, _current, _total);
    }

    /// <summary>Cancel during work or dismiss after completion.</summary>
    private void ActionButton_Click(object sender, RoutedEventArgs e)
    {
        BeginDismiss(cancel: !_isComplete);
    }

    /// <summary>Expands the per-file failure list.</summary>
    private void ViewReasonButton_Click(object sender, RoutedEventArgs e)
    {
        ShowFailList();
    }

    /// <summary>Absorbs mask clicks so they do not reach content behind the overlay.</summary>
    private void Mask_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }
}
