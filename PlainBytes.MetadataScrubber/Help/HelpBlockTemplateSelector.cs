// SPDX-FileCopyrightText: 2026 PlainBytes Studio
// SPDX-License-Identifier: GPL-3.0-only

using System.Windows;
using System.Windows.Controls;

namespace PlainBytes.MetadataScrubber.Help;

/// <summary>
/// Picks the XAML <see cref="DataTemplate"/> for each help content block type on the Help page.
/// </summary>
public sealed class HelpBlockTemplateSelector : DataTemplateSelector
{
    /// <summary>Template for <see cref="HelpDescriptionBlock"/>.</summary>
    public DataTemplate? DescriptionTemplate { get; set; }

    /// <summary>Template for <see cref="HelpHeadingBlock"/>.</summary>
    public DataTemplate? HeadingTemplate { get; set; }

    /// <summary>Template for <see cref="HelpStepsBlock"/>.</summary>
    public DataTemplate? StepsTemplate { get; set; }

    /// <summary>Template for <see cref="HelpCardsBlock"/>.</summary>
    public DataTemplate? CardsTemplate { get; set; }

    /// <summary>Template for <see cref="HelpFaqBlock"/>.</summary>
    public DataTemplate? FaqTemplate { get; set; }

    /// <summary>Template for <see cref="HelpVersionBlock"/>.</summary>
    public DataTemplate? VersionTemplate { get; set; }

    /// <summary>Template for <see cref="HelpTipBlock"/>.</summary>
    public DataTemplate? TipTemplate { get; set; }

    /// <summary>Template for <see cref="HelpLinkBlock"/>.</summary>
    public DataTemplate? LinkTemplate { get; set; }

    /// <summary>Maps a help block instance to its configured template.</summary>
    public override DataTemplate? SelectTemplate(object item, DependencyObject container) =>
        item switch
        {
            HelpDescriptionBlock => DescriptionTemplate,
            HelpHeadingBlock => HeadingTemplate,
            HelpStepsBlock => StepsTemplate,
            HelpCardsBlock => CardsTemplate,
            HelpFaqBlock => FaqTemplate,
            HelpVersionBlock => VersionTemplate,
            HelpTipBlock => TipTemplate,
            HelpLinkBlock => LinkTemplate,
            _ => base.SelectTemplate(item, container),
        };
}
