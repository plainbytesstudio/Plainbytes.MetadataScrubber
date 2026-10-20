// SPDX-FileCopyrightText: 2026 PlainBytes Studio
// SPDX-License-Identifier: GPL-3.0-only

namespace PlainBytes.MetadataScrubber.Help;

/// <summary>One sidebar section on the Help page, with ordered content blocks.</summary>
public sealed class HelpSection
{
    /// <summary>Stable section id used for navigation (e.g. QuickStart).</summary>
    public required string Id { get; init; }

    /// <summary>Localized section title shown in the sidebar and content header.</summary>
    public required string Title { get; init; }

    /// <summary>Ordered content blocks rendered by <see cref="HelpContentView"/>.</summary>
    public required IReadOnlyList<HelpBlock> Blocks { get; init; }
}

/// <summary>Base type for polymorphic help content blocks.</summary>
public abstract class HelpBlock;

/// <summary>Paragraph of body text.</summary>
public sealed class HelpDescriptionBlock : HelpBlock
{
    /// <summary>Localized description text.</summary>
    public required string Text { get; init; }
}

/// <summary>Section subheading within a help page.</summary>
public sealed class HelpHeadingBlock : HelpBlock
{
    /// <summary>Localized heading text.</summary>
    public required string Text { get; init; }
}

/// <summary>One numbered step: title plus body.</summary>
public sealed class HelpStepItem
{
    /// <summary>Step title.</summary>
    public required string Title { get; init; }

    /// <summary>Step body text.</summary>
    public required string Body { get; init; }
}

/// <summary>Ordered list of how-to steps.</summary>
public sealed class HelpStepsBlock : HelpBlock
{
    /// <summary>Steps to display.</summary>
    public required IReadOnlyList<HelpStepItem> Items { get; init; }
}

/// <summary>Card entry with optional icon and meta line.</summary>
public sealed class HelpCardItem
{
    /// <summary>Card title.</summary>
    public required string Title { get; init; }

    /// <summary>Card body text.</summary>
    public required string Body { get; init; }

    /// <summary>Optional pack URI for the card icon.</summary>
    public string? IconPath { get; init; }

    /// <summary>Optional secondary line (e.g. file extensions).</summary>
    public string? Meta { get; init; }
}

/// <summary>Grid/list of feature or format cards.</summary>
public sealed class HelpCardsBlock : HelpBlock
{
    /// <summary>Cards to display.</summary>
    public required IReadOnlyList<HelpCardItem> Items { get; init; }
}

/// <summary>One FAQ question and answer pair.</summary>
public sealed class HelpFaqItem
{
    /// <summary>Question text.</summary>
    public required string Question { get; init; }

    /// <summary>Answer text.</summary>
    public required string Answer { get; init; }
}

/// <summary>FAQ accordion or list block.</summary>
public sealed class HelpFaqBlock : HelpBlock
{
    /// <summary>FAQ items to display.</summary>
    public required IReadOnlyList<HelpFaqItem> Items { get; init; }
}

/// <summary>One changelog version entry.</summary>
public sealed class HelpVersionItem
{
    /// <summary>Raw version string (e.g. 1.0.0).</summary>
    public required string Version { get; init; }

    /// <summary>Localized display label for the version.</summary>
    public required string VersionLabel { get; init; }

    /// <summary>Short release title.</summary>
    public required string Title { get; init; }

    /// <summary>Bullet notes for this release.</summary>
    public required IReadOnlyList<string> Notes { get; init; }
}

/// <summary>Changelog / version history block.</summary>
public sealed class HelpVersionBlock : HelpBlock
{
    /// <summary>Version entries newest-first or catalog order.</summary>
    public required IReadOnlyList<HelpVersionItem> Items { get; init; }
}

/// <summary>Callout tip with title and body.</summary>
public sealed class HelpTipBlock : HelpBlock
{
    /// <summary>Tip title.</summary>
    public required string Title { get; init; }

    /// <summary>Tip body text.</summary>
    public required string Body { get; init; }
}

/// <summary>Secondary text link that raises an action id.</summary>
public sealed class HelpLinkBlock : HelpBlock
{
    /// <summary>Link label.</summary>
    public required string Text { get; init; }

    /// <summary>Action identifier for the help host.</summary>
    public required string ActionId { get; init; }
}
