// SPDX-FileCopyrightText: 2026 PlainBytes Studio
// SPDX-License-Identifier: GPL-3.0-only

namespace PlainBytes.MetadataScrubber.Help;

/// <summary>
/// Builds the Help page section tree from localized <see cref="HelpText"/> keys and pack-URI icons.
/// </summary>
internal static class HelpCatalog
{
    private const string Img = "pack://application:,,,/PlainBytes.MetadataScrubber;component/Assets/Images/";

    /// <summary>All help sections in sidebar / content order.</summary>
    public static IReadOnlyList<HelpSection> GetSections() =>
    [
        BuildQuickStart(),
        BuildOverview(),
        BuildFormats(),
        BuildStrategies(),
        BuildFaq(),
        BuildCustomDev(),
        BuildPrivacy(),
        BuildTips(),
        BuildChangelog(),
    ];

    /// <summary>Resolves a localized string by help resource key.</summary>
    private static string T(string key) => HelpText.Get(key);

    /// <summary>Quick start: tip plus numbered steps.</summary>
    private static HelpSection BuildQuickStart() => new()
    {
        Id = "QuickStart",
        Title = T("Section.QuickStart.Title"),
        Blocks =
        [
            new HelpDescriptionBlock { Text = T("QuickStart.Description") },
            new HelpTipBlock { Title = T("QuickStart.Tip.Title"), Body = T("QuickStart.Tip.Body") },
            new HelpStepsBlock
            {
                Items =
                [
                    new HelpStepItem { Title = T("QuickStart.Step1.Title"), Body = T("QuickStart.Step1.Body") },
                    new HelpStepItem { Title = T("QuickStart.Step2.Title"), Body = T("QuickStart.Step2.Body") },
                    new HelpStepItem { Title = T("QuickStart.Step3.Title"), Body = T("QuickStart.Step3.Body") },
                    new HelpStepItem { Title = T("QuickStart.Step4.Title"), Body = T("QuickStart.Step4.Body") },
                ],
            },
        ],
    };

    /// <summary>Product overview cards.</summary>
    private static HelpSection BuildOverview() => new()
    {
        Id = "Overview",
        Title = T("Section.Overview.Title"),
        Blocks =
        [
            new HelpDescriptionBlock { Text = T("Overview.Description") },
            new HelpCardsBlock
            {
                Items =
                [
                    new HelpCardItem { Title = T("Overview.Card1.Title"), Body = T("Overview.Card1.Body") },
                    new HelpCardItem { Title = T("Overview.Card2.Title"), Body = T("Overview.Card2.Body") },
                    new HelpCardItem { Title = T("Overview.Card3.Title"), Body = T("Overview.Card3.Body") },
                    new HelpCardItem { Title = T("Overview.Card4.Title"), Body = T("Overview.Card4.Body") },
                ],
            },
        ],
    };

    /// <summary>Supported formats (Word, Excel, PowerPoint, PDF) and format notes.</summary>
    private static HelpSection BuildFormats() => new()
    {
        Id = "Formats",
        Title = T("Section.Formats.Title"),
        Blocks =
        [
            new HelpDescriptionBlock { Text = T("Formats.Description") },
            new HelpCardsBlock
            {
                Items =
                [
                    new HelpCardItem
                    {
                        Title = T("Formats.Card.Word.Title"),
                        Meta = T("Formats.Card.Word.Meta"),
                        Body = T("Formats.Card.Word.Body"),
                        IconPath = Img + "word.png",
                    },
                    new HelpCardItem
                    {
                        Title = T("Formats.Card.Excel.Title"),
                        Meta = T("Formats.Card.Excel.Meta"),
                        Body = T("Formats.Card.Excel.Body"),
                        IconPath = Img + "excel.png",
                    },
                    new HelpCardItem
                    {
                        Title = T("Formats.Card.Ppt.Title"),
                        Meta = T("Formats.Card.Ppt.Meta"),
                        Body = T("Formats.Card.Ppt.Body"),
                        IconPath = Img + "ppt.png",
                    },
                    new HelpCardItem
                    {
                        Title = T("Formats.Card.Pdf.Title"),
                        Meta = T("Formats.Card.Pdf.Meta"),
                        Body = T("Formats.Card.Pdf.Body"),
                        IconPath = Img + "pdf.png",
                    },
                ],
            },
            new HelpHeadingBlock { Text = T("Formats.Office.Heading") },
            new HelpDescriptionBlock { Text = T("Formats.Office.Description") },
            new HelpHeadingBlock { Text = T("Formats.Pdf.Heading") },
            new HelpDescriptionBlock { Text = T("Formats.Pdf.Description") },
        ],
    };

    /// <summary>Scrub strategies / presets and related tips.</summary>
    private static HelpSection BuildStrategies() => new()
    {
        Id = "Strategies",
        Title = T("Section.Strategies.Title"),
        Blocks =
        [
            new HelpDescriptionBlock { Text = T("Strategies.Description") },
            new HelpCardsBlock
            {
                Items =
                [
                    new HelpCardItem { Title = T("Strategies.Card1.Title"), Body = T("Strategies.Card1.Body"), IconPath = Img + "part1.png" },
                    new HelpCardItem { Title = T("Strategies.Card2.Title"), Body = T("Strategies.Card2.Body"), IconPath = Img + "part2.png" },
                    new HelpCardItem { Title = T("Strategies.Card3.Title"), Body = T("Strategies.Card3.Body"), IconPath = Img + "part3.png" },
                ],
            },
            new HelpTipBlock { Title = T("Strategies.Default.Tip.Title"), Body = T("Strategies.Default.Tip.Body") },
            new HelpHeadingBlock { Text = T("Strategies.Preset.Heading") },
            new HelpDescriptionBlock { Text = T("Strategies.Preset.Description") },
        ],
    };

    /// <summary>Frequently asked questions.</summary>
    private static HelpSection BuildFaq() => new()
    {
        Id = "Faq",
        Title = T("Section.Faq.Title"),
        Blocks =
        [
            new HelpFaqBlock
            {
                Items =
                [
                    new HelpFaqItem { Question = T("Faq.Q1"), Answer = T("Faq.A1") },
                    new HelpFaqItem { Question = T("Faq.Q2"), Answer = T("Faq.A2") },
                    new HelpFaqItem { Question = T("Faq.Q3"), Answer = T("Faq.A3") },
                    new HelpFaqItem { Question = T("Faq.Q4"), Answer = T("Faq.A4") },
                    new HelpFaqItem { Question = T("Faq.Q5"), Answer = T("Faq.A5") },
                ],
            },
        ],
    };

    /// <summary>Custom development: secondary contact path from Help.</summary>
    private static HelpSection BuildCustomDev() => new()
    {
        Id = "CustomDev",
        Title = T("Section.CustomDev.Title"),
        Blocks =
        [
            new HelpDescriptionBlock { Text = T("CustomDev.Description") },
            new HelpLinkBlock
            {
                Text = T("CustomDev.Contact"),
                ActionId = "CustomDevContact",
            },
        ],
    };

    /// <summary>Privacy: local processing and original-file handling.</summary>
    private static HelpSection BuildPrivacy() => new()
    {
        Id = "Privacy",
        Title = T("Section.Privacy.Title"),
        Blocks =
        [
            new HelpHeadingBlock { Text = T("Privacy.Local.Heading") },
            new HelpDescriptionBlock { Text = T("Privacy.Local.Description") },
            new HelpHeadingBlock { Text = T("Privacy.Original.Heading") },
            new HelpDescriptionBlock { Text = T("Privacy.Original.Description") },
            new HelpTipBlock { Title = T("Privacy.Tip.Title"), Body = T("Privacy.Tip.Body") },
        ],
    };

    /// <summary>Practical tips for everyday use.</summary>
    private static HelpSection BuildTips() => new()
    {
        Id = "Tips",
        Title = T("Section.Tips.Title"),
        Blocks =
        [
            new HelpTipBlock { Title = T("Tips.1.Title"), Body = T("Tips.1.Body") },
            new HelpTipBlock { Title = T("Tips.2.Title"), Body = T("Tips.2.Body") },
            new HelpTipBlock { Title = T("Tips.3.Title"), Body = T("Tips.3.Body") },
            new HelpTipBlock { Title = T("Tips.4.Title"), Body = T("Tips.4.Body") },
            new HelpTipBlock { Title = T("Tips.5.Title"), Body = T("Tips.5.Body") },
        ],
    };

    /// <summary>Version history / changelog entries.</summary>
    private static HelpSection BuildChangelog() => new()
    {
        Id = "Changelog",
        Title = T("Section.Changelog.Title"),
        Blocks =
        [
            new HelpVersionBlock
            {
                Items =
                [
                    new HelpVersionItem
                    {
                        Version = "1.0.0",
                        VersionLabel = string.Format(System.Globalization.CultureInfo.CurrentUICulture, T("Changelog.VersionFormat"), "1.0.0"),
                        Title = T("Changelog.V100.Title"),
                        Notes =
                        [
                            T("Changelog.V100.N1"),
                            T("Changelog.V100.N2"),
                            T("Changelog.V100.N3"),
                            T("Changelog.V100.N4"),
                            T("Changelog.V100.N5"),
                            T("Changelog.V100.N6"),
                        ],
                    },
                ],
            },
        ],
    };
}
