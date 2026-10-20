// SPDX-FileCopyrightText: 2026 PlainBytes Studio
// SPDX-License-Identifier: GPL-3.0-only

using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using PlainBytes.MetadataScrubber.Resources;
using IoPath = System.IO.Path;

namespace PlainBytes.MetadataScrubber;

/// <summary>
/// Recognizes supported document types and helpers for labels, icons, and size formatting (offline).
/// </summary>
internal static class DocumentFileSupport
{
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        ".docx", ".docm",
        ".xlsx", ".xlsm",
        ".pptx", ".pptm",
        ".pdf",
    };

    /// <summary>Supported extensions including the leading dot (e.g. .docx).</summary>
    public static IReadOnlyCollection<string> SupportedExtensions => Allowed;

    /// <summary>True when the path has a supported document extension.</summary>
    public static bool IsSupported(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;
        return Allowed.Contains(IoPath.GetExtension(path));
    }

    /// <summary>Filters a path sequence to supported documents only.</summary>
    public static IEnumerable<string> FilterSupported(IEnumerable<string>? paths) =>
        paths?.Where(IsSupported) ?? [];

    /// <summary>Localized type label for the file extension (Word, Excel, etc.).</summary>
    public static string GetTypeLabel(string path) => IoPath.GetExtension(path).ToLowerInvariant() switch
    {
        ".docx" or ".docm" => Strings.Type_Word,
        ".xlsx" or ".xlsm" => Strings.Type_Excel,
        ".pptx" or ".pptm" => Strings.Type_PowerPoint,
        ".pdf" => Strings.Type_Pdf,
        _ => Strings.Type_File,
    };

    /// <summary>Asset file name for the type icon under the app Images folder.</summary>
    public static string GetIconFileName(string path) => IoPath.GetExtension(path).ToLowerInvariant() switch
    {
        ".docx" or ".docm" => "word.png",
        ".xlsx" or ".xlsm" => "excel.png",
        ".pptx" or ".pptm" => "ppt.png",
        ".pdf" => "pdf.png",
        _ => "folder.png",
    };

    /// <summary>Formats a byte length as B, KB, or MB for UI display.</summary>
    public static string FormatSize(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024d:0.##} KB";
        return $"{bytes / (1024d * 1024d):0.##} MB";
    }
}

/// <summary>
/// Reads displayable metadata groups from local Office Open XML and PDF files (offline, no network).
/// </summary>
internal static class DocumentMetadataReader
{
    private static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";
    private static readonly XNamespace Cp = "http://schemas.openxmlformats.org/package/2006/metadata/core-properties";
    private static readonly XNamespace DcTerms = "http://purl.org/dc/terms/";
    private static readonly XNamespace Ep = "http://schemas.openxmlformats.org/officeDocument/2006/extended-properties";
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly Regex WindowsPathRegex = new(
        @"[A-Za-z]:\\(?:[^<>:""|?*\r\n]+\\)*[^<>:""|?*\r\n]+",
        RegexOptions.Compiled);
    private static readonly Regex FileUriRegex = new(
        @"file:///[^\s""'<>]+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Returns metadata groups for <paramref name="path"/>, or an empty list on unsupported type or read failure.
    /// </summary>
    public static IReadOnlyList<MetaGroup> Read(string path)
    {
        try
        {
            var ext = IoPath.GetExtension(path).ToLowerInvariant();
            return ext switch
            {
                ".docx" or ".docm" or ".xlsx" or ".xlsm" or ".pptx" or ".pptm"
                    => ReadOffice(path, ext),
                ".pdf" => ReadPdf(path),
                _ => [],
            };
        }
        catch
        {
            return [];
        }
    }

    /// <summary>Reads document properties, revisions/comments, and residual paths from an OOXML package.</summary>
    private static IReadOnlyList<MetaGroup> ReadOffice(string path, string ext)
    {
        using var zip = ZipFile.OpenRead(path);
        var groups = new List<MetaGroup>();

        var docProps = ReadOfficeDocumentProperties(zip);
        if (docProps.Count > 0)
            groups.Add(new MetaGroup(Strings.Meta_Group_DocumentProperties, docProps));

        var revisions = ReadOfficeRevisionsAndComments(zip, ext);
        if (revisions.Count > 0)
            groups.Add(new MetaGroup(Strings.Meta_Group_Revisions, revisions));

        var residuals = ReadOfficeHiddenPaths(zip);
        if (residuals.Count > 0)
            groups.Add(new MetaGroup(Strings.Meta_Group_Hidden, residuals));

        return groups;
    }

    /// <summary>Extracts core and app document properties from docProps XML parts.</summary>
    private static List<MetaField> ReadOfficeDocumentProperties(ZipArchive zip)
    {
        var fields = new List<MetaField>();
        var core = TryLoadXml(zip, "docProps/core.xml");
        if (core is not null)
        {
            AddIfPresent(fields, Strings.Meta_Field_Author, core.Root?.Element(Dc + "creator")?.Value);
            AddIfPresent(fields, Strings.Meta_Field_LastModifiedBy, core.Root?.Element(Cp + "lastModifiedBy")?.Value);
            AddIfPresent(fields, Strings.Meta_Field_Created, FormatOfficeDate(core.Root?.Element(DcTerms + "created")?.Value));
            AddIfPresent(fields, Strings.Meta_Field_Modified, FormatOfficeDate(core.Root?.Element(DcTerms + "modified")?.Value));
        }

        var app = TryLoadXml(zip, "docProps/app.xml");
        if (app is not null)
        {
            AddIfPresent(fields, Strings.Meta_Field_Company, app.Root?.Element(Ep + "Company")?.Value);
            var totalTime = app.Root?.Element(Ep + "TotalTime")?.Value;
            if (!string.IsNullOrWhiteSpace(totalTime) && int.TryParse(totalTime, out var minutes) && minutes > 0)
                fields.Add(new MetaField(Strings.Meta_Field_TotalEditTime, FormatEditMinutes(minutes)));
        }

        return fields;
    }

    /// <summary>Counts revisions and comments (with authors) for Word, Excel, or PowerPoint packages.</summary>
    private static List<MetaField> ReadOfficeRevisionsAndComments(ZipArchive zip, string ext)
    {
        var fields = new List<MetaField>();

        if (ext is ".docx" or ".docm")
        {
            var revisionCount = CountLocalNameOccurrences(zip, "word/document.xml", ["ins", "del", "moveFrom", "moveTo"]);
            foreach (var header in zip.Entries.Where(e =>
                         e.FullName.StartsWith("word/header", StringComparison.OrdinalIgnoreCase)
                         || e.FullName.StartsWith("word/footer", StringComparison.OrdinalIgnoreCase)))
            {
                revisionCount += CountLocalNameOccurrences(zip, header.FullName, ["ins", "del", "moveFrom", "moveTo"]);
            }

            if (revisionCount > 0)
                fields.Add(new MetaField(Strings.Meta_Field_Revisions, Strings.Format(Strings.Meta_Count, revisionCount)));

            var commentsXml = TryLoadXml(zip, "word/comments.xml");
            if (commentsXml is not null)
            {
                var comments = commentsXml.Descendants(W + "comment").ToList();
                if (comments.Count > 0)
                {
                    fields.Add(new MetaField(Strings.Meta_Field_Comments, Strings.Format(Strings.Meta_Count, comments.Count)));
                    var authors = comments
                        .Select(c => (string?)c.Attribute(W + "author") ?? c.Attribute("author")?.Value)
                        .Where(a => !string.IsNullOrWhiteSpace(a))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    if (authors.Count > 0)
                        fields.Add(new MetaField(Strings.Meta_Field_CommentAuthors, string.Join(Strings.Meta_ListSeparator, authors!)));
                }
            }
        }
        else if (ext is ".xlsx" or ".xlsm")
        {
            var commentParts = zip.Entries
                .Where(e => e.FullName.StartsWith("xl/", StringComparison.OrdinalIgnoreCase)
                            && e.FullName.Contains("comment", StringComparison.OrdinalIgnoreCase)
                            && e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var commentCount = 0;
            var authors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var part in commentParts)
            {
                var xml = TryLoadXml(zip, part.FullName);
                if (xml is null)
                    continue;
                foreach (var node in xml.Descendants().Where(n => n.Name.LocalName is "comment" or "person"))
                {
                    if (node.Name.LocalName == "comment")
                        commentCount++;
                    var author = node.Attribute("author")?.Value
                                 ?? node.Attribute("displayname")?.Value
                                 ?? node.Elements().FirstOrDefault(e => e.Name.LocalName == "displayname")?.Value;
                    if (!string.IsNullOrWhiteSpace(author))
                        authors.Add(author);
                }
            }

            if (commentCount > 0)
                fields.Add(new MetaField(Strings.Meta_Field_Comments, Strings.Format(Strings.Meta_Count, commentCount)));
            if (authors.Count > 0)
                fields.Add(new MetaField(Strings.Meta_Field_CommentAuthors, string.Join(Strings.Meta_ListSeparator, authors)));
        }
        else if (ext is ".pptx" or ".pptm")
        {
            var commentParts = zip.Entries
                .Where(e => e.FullName.Contains("comment", StringComparison.OrdinalIgnoreCase)
                            && e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var commentCount = 0;
            var authors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var part in commentParts)
            {
                var xml = TryLoadXml(zip, part.FullName);
                if (xml is null)
                    continue;
                foreach (var node in xml.Descendants().Where(n => n.Name.LocalName is "cm" or "comment"))
                {
                    commentCount++;
                    var author = node.Attribute("author")?.Value
                                 ?? node.Elements().FirstOrDefault(e => e.Name.LocalName == "author")?.Value;
                    if (!string.IsNullOrWhiteSpace(author))
                        authors.Add(author);
                }
            }

            if (commentCount > 0)
                fields.Add(new MetaField(Strings.Meta_Field_Comments, Strings.Format(Strings.Meta_Count, commentCount)));
            if (authors.Count > 0)
                fields.Add(new MetaField(Strings.Meta_Field_CommentAuthors, string.Join(Strings.Meta_ListSeparator, authors)));
        }

        return fields;
    }

    /// <summary>
    /// Scans package XML/rels for embedded Windows paths and file:// URIs (up to 8 samples).
    /// Skips large parts and media to keep the UI responsive.
    /// </summary>
    private static List<MetaField> ReadOfficeHiddenPaths(ZipArchive zip)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in zip.Entries.Where(e =>
                     e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
                     || e.FullName.EndsWith(".rels", StringComparison.OrdinalIgnoreCase)))
        {
            // Skip oversized document parts so the UI stays responsive.
            if (entry.Length > 2_000_000)
                continue;
            if (entry.FullName.Contains("media/", StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                using var stream = entry.Open();
                using var reader = new StreamReader(stream);
                var text = reader.ReadToEnd();
                foreach (Match m in FileUriRegex.Matches(text))
                    paths.Add(Uri.UnescapeDataString(m.Value));
                foreach (Match m in WindowsPathRegex.Matches(text))
                {
                    var p = m.Value;
                    if (p.Contains('\\') && !p.StartsWith(@"C:\Windows", StringComparison.OrdinalIgnoreCase))
                        paths.Add(p);
                }
            }
            catch
            {
                // ignore part
            }
        }

        return paths.Count == 0
            ? []
            : paths.Take(8).Select(p => new MetaField(Strings.Meta_Field_PathRef, p)).ToList();
    }

    /// <summary>Reads PDF Info dictionary, common XMP fields, and attachment-related fields.</summary>
    private static IReadOnlyList<MetaGroup> ReadPdf(string path)
    {
        var groups = new List<MetaGroup>();
        using var document = PdfDocument.Open(path);

        var infoFields = new List<MetaField>();
        var info = document.Information;
        AddIfPresent(infoFields, "Author", info.Author);
        AddIfPresent(infoFields, "Creator", info.Creator);
        AddIfPresent(infoFields, "Producer", info.Producer);
        AddIfPresent(infoFields, "CreationDate", FormatPdfDate(info.GetCreatedDateTimeOffset(), info.CreationDate));
        AddIfPresent(infoFields, "ModDate", FormatPdfDate(info.GetModifiedDateTimeOffset(), info.ModifiedDate));
        if (infoFields.Count > 0)
            groups.Add(new MetaGroup("PDF Info", infoFields));

        if (document.TryGetXmpMetadata(out XmpMetadata? xmp) && xmp is not null)
        {
            var xmpFields = ReadXmpFields(xmp);
            if (xmpFields.Count > 0)
                groups.Add(new MetaGroup("XMP Metadata", xmpFields));
        }

        var attachmentFields = ReadPdfAttachmentsAndPaths(document);
        if (attachmentFields.Count > 0)
            groups.Add(new MetaGroup(Strings.Meta_Group_Attachments, attachmentFields));

        return groups;
    }

    /// <summary>Extracts a small set of common XMP properties, keeping PDF Info and XMP separate.</summary>
    private static List<MetaField> ReadXmpFields(XmpMetadata xmp)
    {
        var fields = new List<MetaField>();
        try
        {
            var doc = xmp.GetXDocument();
            if (doc.Root is null)
                return fields;

            // Only surface fields that actually appear; keep PDF Info / XMP separate.
            TryAddXmp(fields, doc, "dc:creator");
            TryAddXmp(fields, doc, "xmp:CreatorTool");
            TryAddXmp(fields, doc, "xmp:CreateDate");
            TryAddXmp(fields, doc, "xmp:ModifyDate");
            TryAddXmp(fields, doc, "pdf:Producer");
            TryAddXmp(fields, doc, "xmpMM:DocumentID");
        }
        catch
        {
            // ignore malformed XMP
        }

        return fields;
    }

    /// <summary>Adds one XMP property by local name, including rdf:Bag / rdf:Seq list values.</summary>
    private static void TryAddXmp(List<MetaField> fields, XDocument doc, string compactName)
    {
        var parts = compactName.Split(':', 2);
        if (parts.Length != 2)
            return;

        var local = parts[1];
        var nodes = doc.Descendants().Where(e => e.Name.LocalName == local).ToList();
        if (nodes.Count == 0)
            return;

        string? value = null;
        foreach (var node in nodes)
        {
            if (!string.IsNullOrWhiteSpace(node.Value) && !node.HasElements)
            {
                value = node.Value.Trim();
                break;
            }

            // rdf:Bag / rdf:Seq
            var li = node.Descendants().FirstOrDefault(e => e.Name.LocalName == "li");
            if (li is not null && !string.IsNullOrWhiteSpace(li.Value))
            {
                value = string.Join("、", node.Descendants().Where(e => e.Name.LocalName == "li")
                    .Select(e => e.Value.Trim())
                    .Where(v => v.Length > 0));
                break;
            }
        }

        AddIfPresent(fields, compactName, value);
    }

    /// <summary>
    /// Placeholder for PDF embedded-file enumeration; PdfPig support is limited today.
    /// </summary>
    private static List<MetaField> ReadPdfAttachmentsAndPaths(PdfDocument document)
    {
        var fields = new List<MetaField>();
        try
        {
            // PdfPig has limited embedded-file support; scanning the catalog text is unreliable.
            // Extend here when a fuller attachment enumerator is available.
            _ = document;
        }
        catch
        {
            // ignore
        }

        return fields;
    }

    /// <summary>Loads an XML part from the ZIP by exact or case-insensitive entry name.</summary>
    private static XDocument? TryLoadXml(ZipArchive zip, string entryName)
    {
        var entry = zip.GetEntry(entryName)
                    ?? zip.Entries.FirstOrDefault(e =>
                        e.FullName.Equals(entryName, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
            return null;

        using var stream = entry.Open();
        return XDocument.Load(stream);
    }

    /// <summary>Counts XML elements whose local names match <paramref name="localNames"/> in a package part.</summary>
    private static int CountLocalNameOccurrences(ZipArchive zip, string entryName, string[] localNames)
    {
        var entry = zip.GetEntry(entryName)
                    ?? zip.Entries.FirstOrDefault(e =>
                        e.FullName.Equals(entryName, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
            return 0;

        var set = new HashSet<string>(localNames, StringComparer.Ordinal);
        var count = 0;
        try
        {
            using var stream = entry.Open();
            using var reader = System.Xml.XmlReader.Create(stream, new System.Xml.XmlReaderSettings
            {
                IgnoreComments = true,
                IgnoreWhitespace = true,
                DtdProcessing = System.Xml.DtdProcessing.Prohibit,
            });
            while (reader.Read())
            {
                if (reader.NodeType == System.Xml.XmlNodeType.Element && set.Contains(reader.LocalName))
                    count++;
            }
        }
        catch
        {
            return count;
        }

        return count;
    }

    /// <summary>Adds a field when <paramref name="value"/> is non-empty after trim.</summary>
    private static void AddIfPresent(List<MetaField> fields, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;
        fields.Add(new MetaField(name, value.Trim()));
    }

    /// <summary>Formats an OOXML date string for local display.</summary>
    private static string? FormatOfficeDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto))
            return dto.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        return raw;
    }

    /// <summary>Formats a PDF date from parsed offset or raw Info string.</summary>
    private static string? FormatPdfDate(DateTimeOffset? dto, string? raw)
    {
        if (dto is not null)
            return dto.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        return string.IsNullOrWhiteSpace(raw) ? null : raw;
    }

    /// <summary>Formats TotalTime minutes as localized hours/minutes text.</summary>
    private static string FormatEditMinutes(int minutes)
    {
        if (minutes < 60)
            return Strings.Format(Strings.Meta_Minutes, minutes);
        var h = minutes / 60;
        var m = minutes % 60;
        return m == 0
            ? Strings.Format(Strings.Meta_Hours, h)
            : Strings.Format(Strings.Meta_HoursMinutes, h, m);
    }
}
