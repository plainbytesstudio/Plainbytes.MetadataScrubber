// SPDX-FileCopyrightText: 2026 PlainBytes Studio
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Toolkit.Uwp.Notifications;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
using PlainBytes.MetadataScrubber.Resources;
using CoreStrings = PlainBytes.Core.Resources.Strings;

namespace PlainBytes.MetadataScrubber;

/// <summary>
/// System toasts for headless CLI scrub (no main window).
/// Shows "in progress" first when files are present, then updates the same Tag/Group with the final result.
/// </summary>
internal static class CliResultToast
{
    /// <summary>Must match ToastActivatorCLSID in Package.appxmanifest.</summary>
    public const string ToastActivatorClsid = "a3e8b2c1-7d4f-4e9a-9c1b-2f6d8a0b5e47";

    private const string ToastTag = "PlainBytes.MetadataScrubber.CliScrub";
    private const string ToastGroup = "PlainBytes.MetadataScrubber";
    private const string TitleKey = "title";
    private const string BodyKey = "body";

    private static bool _activationHooked;
    private static bool _progressShown;
    private static uint _sequence;

    /// <summary>Hooks toast activation once so notifications can display from a headless process.</summary>
    public static void EnsureInitialized()
    {
        if (_activationHooked)
            return;

        _activationHooked = true;
        try
        {
            _ = ToastNotificationManagerCompat.WasCurrentProcessToastActivated();
            ToastNotificationManagerCompat.OnActivated += static _ => { };
        }
        catch
        {
            // Initialization may be retried on subsequent Show calls.
        }
    }

    /// <summary>
    /// Shows the in-progress toast for the given file count.
    /// Returns null on success, or an error string for the scrub log.
    /// </summary>
    public static string? ShowScrubProgress(int fileCount)
    {
        EnsureInitialized();

        var body = fileCount <= 1
            ? Strings.Toast_ProgressBodyOne
            : Strings.Format(Strings.Toast_ProgressBodyMany, fileCount);

        try
        {
            ShowBindable(Strings.Toast_ProgressTitle, body);
            _progressShown = true;
            return null;
        }
        catch (Exception ex)
        {
            _progressShown = false;
            return $"toast progress failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Shows or updates the final scrub result toast.
    /// In-place update is used when no colored body is required and progress was shown first.
    /// </summary>
    public static string? ShowScrubResult(int successCount, int failCount, string? firstError = null)
    {
        EnsureInitialized();

        if (successCount < 0)
            successCount = 0;
        if (failCount < 0)
            failCount = 0;

        string title;
        string body;
        string? bodyColor = null;
        if (successCount == 0 && failCount == 0)
        {
            title = Strings.Toast_IncompleteTitle;
            body = Strings.Toast_NoFiles;
        }
        else if (failCount == 0)
        {
            title = Strings.Format(Strings.Overlay_SuccessCount, successCount);
            body = Strings.Toast_SuccessBody;
        }
        else if (successCount == 0)
        {
            title = Strings.Toast_IncompleteTitle;
            body = string.IsNullOrWhiteSpace(firstError)
                ? Strings.Toast_AllFailedBody
                : Strings.Format(Strings.Toast_ErrorWithLog, Truncate(firstError!, 100));
            bodyColor = "#fa5252";
        }
        else
        {
            title = Strings.Format(Strings.Toast_PartialTitle, successCount, failCount);
            body = Strings.Toast_PartialBody;
            bodyColor = "#fcc419";
        }

        // Colored body text cannot change color via Update; replace the whole toast instead.
        if (bodyColor is null && _progressShown && TryUpdate(title, body))
        {
            HoldForBanner();
            return null;
        }

        return Show(title, body, bodyColor);
    }

    /// <summary>Shows trial-expired message for CLI runs blocked by edition.</summary>
    public static string? ShowLicenseExpired()
    {
        EnsureInitialized();
        return Show(CoreStrings.License_ExpiredTitle, CoreStrings.License_ExpiredMessage, "#fa5252");
    }

    /// <summary>Shows a plain toast; returns null on success or a fallback status string for the scrub log.</summary>
    public static string? Show(string title, string body) =>
        Show(title, body, bodyColor: null);

    /// <summary>Displays toast via packaged or unpackaged notifier, with optional colored body image.</summary>
    private static string? Show(string title, string body, string? bodyColor)
    {
        EnsureInitialized();

        var imagePath = bodyColor is null ? null : TryWriteBodyImage(body, bodyColor);

        try
        {
            if (IsPackaged())
            {
                ShowPackaged(title, body, imagePath);
            }
            else
            {
                BuildToast(title, body, imagePath)
                    .Show(toast =>
                    {
                        toast.Tag = ToastTag;
                        toast.Group = ToastGroup;
                    });
            }

            HoldForBanner();
            return null;
        }
        catch (Exception ex)
        {
            try
            {
                BuildToast(title, body, imagePath: null)
                    .Show(toast =>
                    {
                        toast.Tag = ToastTag;
                        toast.Group = ToastGroup;
                    });
                Thread.Sleep(2500);
                return $"toast primary failed: {ex.Message}; fallback ok";
            }
            catch (Exception ex2)
            {
                return $"toast failed: {ex.Message} / {ex2.Message}";
            }
        }
    }

    /// <summary>Builds generic toast content with title and body text or inline image.</summary>
    private static ToastContentBuilder BuildToast(string title, string body, string? imagePath)
    {
        var builder = new ToastContentBuilder().AddText(title);
        if (imagePath is not null)
            builder.AddInlineImage(new Uri(imagePath), hintRemoveMargin: true);
        else
            builder.AddText(body);
        return builder;
    }

    /// <summary>Shows a bindable toast whose title/body can be updated in place later.</summary>
    private static void ShowBindable(string title, string body)
    {
        var xml = new XmlDocument();
        xml.LoadXml(
            "<toast><visual><binding template=\"ToastGeneric\">" +
            "<text>{" + TitleKey + "}</text>" +
            "<text>{" + BodyKey + "}</text>" +
            "</binding></visual></toast>");

        var toast = new ToastNotification(xml)
        {
            Tag = ToastTag,
            Group = ToastGroup,
            Data = CreateData(title, body),
            ExpirationTime = DateTimeOffset.Now.AddMinutes(5),
        };

        if (IsPackaged())
            ToastNotificationManager.CreateToastNotifier().Show(toast);
        else
            ToastNotificationManagerCompat.CreateToastNotifier().Show(toast);
    }

    /// <summary>Updates title/body on the existing progress toast when Tag/Group match.</summary>
    private static bool TryUpdate(string title, string body)
    {
        try
        {
            var data = CreateData(title, body);
            var result = IsPackaged()
                ? ToastNotificationManager.CreateToastNotifier().Update(data, ToastTag, ToastGroup)
                : ToastNotificationManagerCompat.CreateToastNotifier().Update(data, ToastTag, ToastGroup);
            return result == NotificationUpdateResult.Succeeded;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Creates notification data with an incremented sequence number for toast updates.</summary>
    private static NotificationData CreateData(string title, string body)
    {
        _sequence++;
        var data = new NotificationData { SequenceNumber = _sequence };
        data.Values[TitleKey] = title;
        data.Values[BodyKey] = body;
        return data;
    }

    /// <summary>Renders colored body text to a PNG for inline toast image (Update cannot change text color).</summary>
    private static string? TryWriteBodyImage(string text, string hex)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PlainBytes",
                "MetadataScrubber",
                "temp");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "cli-toast-body.png");

            var brush = (SolidColorBrush)new BrushConverter().ConvertFrom(hex)!;
            brush.Freeze();

            const double maxWidth = 334;
            var formatted = new FormattedText(
                text,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface(
                    new FontFamily("Segoe UI Variable, Microsoft YaHei UI, Segoe UI"),
                    FontStyles.Normal,
                    FontWeights.Normal,
                    FontStretches.Normal),
                15,
                brush,
                1.0);
            formatted.MaxTextWidth = maxWidth;
            formatted.MaxLineCount = 2;
            formatted.Trimming = TextTrimming.CharacterEllipsis;

            var width = (int)maxWidth;
            var height = Math.Max(1, (int)Math.Ceiling(formatted.Height));

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
                dc.DrawText(formatted, new Point(0, 0));

            var bmp = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(visual);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bmp));
            using (var fs = File.Create(path))
                encoder.Save(fs);

            return path;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Shows toast through the packaged WinRT notifier API.</summary>
    private static void ShowPackaged(string title, string body, string? imagePath)
    {
        var content = BuildToast(title, body, imagePath).GetToastContent();

        var xml = new XmlDocument();
        xml.LoadXml(content.GetContent());

        var toast = new ToastNotification(xml)
        {
            Tag = ToastTag,
            Group = ToastGroup,
            ExpirationTime = DateTimeOffset.Now.AddMinutes(5),
        };

        ToastNotificationManager.CreateToastNotifier().Show(toast);
    }

    /// <summary>Brief delay so the banner remains visible before the headless process exits.</summary>
    private static void HoldForBanner() =>
        Thread.Sleep(IsPackaged() ? 2500 : 1200);

    /// <summary>True when running inside an MSIX package with a valid family name.</summary>
    private static bool IsPackaged()
    {
        try
        {
            var package = Windows.ApplicationModel.Package.Current;
            return package is not null && !string.IsNullOrEmpty(package.Id?.FamilyName);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Truncates text for toast display with an ellipsis suffix.</summary>
    private static string Truncate(string text, int max)
    {
        text = text.Trim();
        if (text.Length <= max)
            return text;
        return text[..(max - 1)] + "…";
    }
}
