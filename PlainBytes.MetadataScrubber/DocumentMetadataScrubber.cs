// SPDX-FileCopyrightText: 2026 PlainBytes Studio
// SPDX-License-Identifier: GPL-3.0-only

using System.IO;
using System.IO.Compression;
using System.Xml.Linq;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using PlainBytes.MetadataScrubber.Resources;
using IoPath = System.IO.Path;

namespace PlainBytes.MetadataScrubber;

/// <summary>
/// Scrub scope flags aligned with the three strategy presets in strategy management.
/// </summary>
internal sealed class ScrubOptions
{
    /// <summary>Clear document Info / core properties (author, company, dates, etc.).</summary>
    public bool DocumentProperties { get; init; }

    /// <summary>Clear tracked revisions and comments.</summary>
    public bool RevisionsAndComments { get; init; }

    /// <summary>Clear residual paths, thumbnails, custom props, and similar leftovers.</summary>
    public bool HiddenPathsAndResiduals { get; init; }

    /// <summary>Full export preset: all three scopes enabled.</summary>
    public static ScrubOptions Export { get; } = new()
    {
        DocumentProperties = true,
        RevisionsAndComments = true,
        HiddenPathsAndResiduals = true,
    };

    /// <summary>Archive preset: properties and revisions; keep residuals.</summary>
    public static ScrubOptions Archive { get; } = new()
    {
        DocumentProperties = true,
        RevisionsAndComments = true,
        HiddenPathsAndResiduals = false,
    };

    /// <summary>Minimal preset: document properties only.</summary>
    public static ScrubOptions Minimal { get; } = new()
    {
        DocumentProperties = true,
        RevisionsAndComments = false,
        HiddenPathsAndResiduals = false,
    };

    /// <summary>Maps a strategy preset id to options; unknown ids fall back to <see cref="Export"/>.</summary>
    public static ScrubOptions FromPresetId(string? id) => id?.ToLowerInvariant() switch
    {
        "archive" => Archive,
        "minimal" => Minimal,
        _ => Export,
    };
}

/// <summary>Per-file scrub outcome for batch CLI or UI progress.</summary>
internal sealed record ScrubFileResult(string Path, bool Success, string? Error);

/// <summary>Progress snapshot while scrubbing a batch.</summary>
internal sealed record ScrubProgressReport(int Completed, int Total, string? CurrentFile);

/// <summary>
/// Offline scrub of Office Open XML and PDF metadata, writing results back to the local file.
/// </summary>
internal static class DocumentMetadataScrubber
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace Ct = "http://schemas.openxmlformats.org/package/2006/content-types";

    /// <summary>
    /// Scrubs each path sequentially; reports progress and continues after per-file failures.
    /// Returns early (partial results) when cancellation is requested.
    /// </summary>
    public static async Task<IReadOnlyList<ScrubFileResult>> ScrubManyAsync(
        IReadOnlyList<string> paths,
        ScrubOptions options,
        IProgress<ScrubProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<ScrubFileResult>(paths.Count);
        var total = paths.Count;

        for (var i = 0; i < paths.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
                return results;

            var path = paths[i];
            progress?.Report(new ScrubProgressReport(i, total, path));

            ScrubFileResult result;
            try
            {
                await Task.Run(() => ScrubFile(path, options), cancellationToken).ConfigureAwait(false);
                result = new ScrubFileResult(path, true, null);
            }
            catch (OperationCanceledException)
            {
                return results;
            }
            catch (Exception ex)
            {
                // File in use, access denied, etc.: record failure and continue with remaining files.
                var message = UnwrapException(ex).Message;
                result = new ScrubFileResult(path, false, message);
            }

            results.Add(result);
            progress?.Report(new ScrubProgressReport(i + 1, total, path));
        }

        return results;
    }

    /// <summary>Unwraps single-inner AggregateException from Task while preserving business IOException messages.</summary>
    private static Exception UnwrapException(Exception ex)
    {
        while (ex is AggregateException { InnerExceptions.Count: 1 } agg && agg.InnerException is not null)
            ex = agg.InnerException;
        return ex;
    }

    /// <summary>Scrubs one supported file in place according to <paramref name="options"/>.</summary>
    public static void ScrubFile(string path, ScrubOptions options)
    {
        if (!DocumentFileSupport.IsSupported(path))
            throw new NotSupportedException(Strings.Error_UnsupportedType);
        if (!File.Exists(path))
            throw new FileNotFoundException(Strings.Error_NotFound, path);

        EnsureFileAccessible(path);

        var ext = IoPath.GetExtension(path).ToLowerInvariant();
        if (ext == ".pdf")
            ScrubPdf(path, options);
        else
            ScrubOffice(path, ext, options);
    }

    /// <summary>
    /// Pre-checks read/write access; throws a localized IOException when Word (or another app) locks the file.
    /// </summary>
    private static void EnsureFileAccessible(string path)
    {
        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None);
        }
        catch (IOException ex)
        {
            throw new IOException(Strings.Error_Occupied, ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new IOException(Strings.Error_AccessDenied, ex);
        }
    }

    // ─── Office Open XML ─────────────────────────────────────────────

    /// <summary>
    /// Rebuilds the OOXML package to a temp ZIP then copies over the original.
    /// Avoids ZipArchiveMode.Update in-place edits that can produce Word-unreadable packages.
    /// </summary>
    private static void ScrubOffice(string path, string ext, ScrubOptions options)
    {
        var output = IoPath.Combine(IoPath.GetTempPath(), "pb-scrub-out-" + Guid.NewGuid().ToString("N") + ext);

        try
        {
            using (var source = ZipFile.OpenRead(path))
            {
                var edit = new OfficePackageEdit(source);

                if (options.DocumentProperties)
                    ClearOfficeDocumentProperties(edit);

                if (options.RevisionsAndComments)
                    ClearOfficeRevisionsAndComments(edit, ext);

                if (options.HiddenPathsAndResiduals)
                    ClearOfficeHiddenResiduals(edit);

                edit.WriteTo(output);
            }

            try
            {
                File.Copy(output, path, overwrite: true);
            }
            catch (IOException ex)
            {
                throw new IOException(Strings.Error_Occupied, ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new IOException(Strings.Error_WriteDenied, ex);
            }
        }
        finally
        {
            TryDelete(output);
        }
    }

    /// <summary>Clears core properties and selected app.xml fields without rewriting risky namespaces.</summary>
    private static void ClearOfficeDocumentProperties(OfficePackageEdit edit)
    {
        // Use a minimal core.xml Word accepts; full XDocument rewrite can break namespace validation.
        if (edit.HasEntry("docProps/core.xml"))
            edit.ReplaceBytes("docProps/core.xml", MinimalCorePropertiesXml);

        // Surgical deletes in app.xml preserve original namespaces and unrelated property nodes.
        edit.RemoveXmlElementsByLocalName(
            "docProps/app.xml",
            "Company", "Manager", "TotalTime");
    }

    private static readonly byte[] MinimalCorePropertiesXml = System.Text.Encoding.UTF8.GetBytes(
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties" xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:dcterms="http://purl.org/dc/terms/" xmlns:dcmitype="http://purl.org/dc/dcmitype/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"/>
        """);

    /// <summary>Removes comments, people parts, and tracked-change markup for Word/Excel/PowerPoint.</summary>
    private static void ClearOfficeRevisionsAndComments(OfficePackageEdit edit, string ext)
    {
        if (ext is ".docx" or ".docm")
        {
            foreach (var name in new[]
                     {
                         "word/comments.xml",
                         "word/commentsExtended.xml",
                         "word/commentsExtensible.xml",
                         "word/commentsIds.xml",
                         "word/people.xml",
                     })
            {
                edit.Delete(name);
            }

            edit.RemoveContentTypeOverrides(name =>
                name.Contains("/comments", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith("/people.xml", StringComparison.OrdinalIgnoreCase));

            edit.RemoveRelationships("word/_rels/document.xml.rels", target =>
                target.Contains("comments", StringComparison.OrdinalIgnoreCase)
                || target.Contains("people", StringComparison.OrdinalIgnoreCase));

            foreach (var entryName in edit.EntryNames
                         .Where(e => e.StartsWith("word/", StringComparison.OrdinalIgnoreCase)
                                     && e.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
                                     && !e.Contains("_rels", StringComparison.OrdinalIgnoreCase)
                                     && !e.Contains("media/", StringComparison.OrdinalIgnoreCase))
                         .ToList())
            {
                var xml = edit.TryLoadXml(entryName);
                if (xml?.Root is null)
                    continue;
                if (AcceptWordMarkup(xml.Root))
                    edit.ReplaceXml(entryName, xml);
            }
        }
        else if (ext is ".xlsx" or ".xlsm")
        {
            foreach (var part in edit.EntryNames
                         .Where(e => e.StartsWith("xl/", StringComparison.OrdinalIgnoreCase)
                                     && e.Contains("comment", StringComparison.OrdinalIgnoreCase)
                                     && e.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                         .ToList())
            {
                edit.Delete(part);
            }

            foreach (var rels in edit.EntryNames
                         .Where(e => e.StartsWith("xl/", StringComparison.OrdinalIgnoreCase)
                                     && e.EndsWith(".rels", StringComparison.OrdinalIgnoreCase))
                         .ToList())
            {
                edit.RemoveRelationships(rels, t => t.Contains("comment", StringComparison.OrdinalIgnoreCase));
            }

            edit.RemoveContentTypeOverrides(n => n.Contains("comment", StringComparison.OrdinalIgnoreCase));
        }
        else if (ext is ".pptx" or ".pptm")
        {
            foreach (var part in edit.EntryNames
                         .Where(e => e.Contains("comment", StringComparison.OrdinalIgnoreCase)
                                     && e.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                         .ToList())
            {
                edit.Delete(part);
            }

            foreach (var rels in edit.EntryNames
                         .Where(e => e.EndsWith(".rels", StringComparison.OrdinalIgnoreCase))
                         .ToList())
            {
                edit.RemoveRelationships(rels, t => t.Contains("comment", StringComparison.OrdinalIgnoreCase));
            }

            edit.RemoveContentTypeOverrides(n => n.Contains("comment", StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Accepts Word tracked changes in place: removes deletions/moveFrom, unwraps insertions/moveTo, strips comment and change markers.
    /// </summary>
    private static bool AcceptWordMarkup(XElement root)
    {
        var changed = false;
        static int Depth(XElement e) => e.Ancestors().Count();

        foreach (var del in root.Descendants(W + "del").OrderByDescending(Depth).ToList())
        {
            del.Remove();
            changed = true;
        }

        foreach (var moveFrom in root.Descendants(W + "moveFrom").OrderByDescending(Depth).ToList())
        {
            moveFrom.Remove();
            changed = true;
        }

        foreach (var ins in root.Descendants(W + "ins").OrderByDescending(Depth).ToList())
        {
            Unwrap(ins);
            changed = true;
        }

        foreach (var moveTo in root.Descendants(W + "moveTo").OrderByDescending(Depth).ToList())
        {
            Unwrap(moveTo);
            changed = true;
        }

        foreach (var name in new[]
                 {
                     "commentRangeStart", "commentRangeEnd", "commentReference",
                     "moveFromRangeStart", "moveFromRangeEnd",
                     "moveToRangeStart", "moveToRangeEnd",
                 })
        {
            foreach (var node in root.Descendants(W + name).ToList())
            {
                node.Remove();
                changed = true;
            }
        }

        foreach (var node in root.Descendants()
                     .Where(e => e.Name.Namespace == W && e.Name.LocalName.EndsWith("PrChange", StringComparison.Ordinal))
                     .OrderByDescending(Depth)
                     .ToList())
        {
            node.Remove();
            changed = true;
        }

        return changed;
    }

    /// <summary>Promotes children of a markup wrapper element then removes the wrapper.</summary>
    private static void Unwrap(XElement element)
    {
        foreach (var child in element.Elements().ToList())
            element.AddBeforeSelf(child);
        element.Remove();
    }

    /// <summary>Removes custom props, thumbnails, hyperlink base, and attached template residuals.</summary>
    private static void ClearOfficeHiddenResiduals(OfficePackageEdit edit)
    {
        edit.Delete("docProps/custom.xml");
        edit.Delete("docProps/thumbnail.jpeg");
        edit.Delete("docProps/thumbnail.emf");
        edit.Delete("docProps/thumbnail.wmf");

        edit.RemoveContentTypeOverrides(n =>
            n.EndsWith("/custom.xml", StringComparison.OrdinalIgnoreCase)
            || n.Contains("thumbnail", StringComparison.OrdinalIgnoreCase));

        edit.RemoveRelationships("_rels/.rels", t =>
            t.Contains("custom", StringComparison.OrdinalIgnoreCase)
            || t.Contains("thumbnail", StringComparison.OrdinalIgnoreCase));

        edit.RemoveXmlElementsByLocalName(
            "docProps/app.xml",
            "Template", "HyperlinkBase");

        var settings = edit.TryLoadXml("word/settings.xml");
        if (settings?.Root is not null)
        {
            var attached = settings.Root
                .Descendants()
                .Where(e => e.Name.LocalName == "attachedTemplate")
                .ToList();
            if (attached.Count > 0)
            {
                foreach (var node in attached)
                    node.Remove();
                // XDocument rewrite of settings.xml is usually safe; switch to text delete if needed.
                edit.ReplaceXml("word/settings.xml", settings);
                edit.RemoveRelationships("word/_rels/settings.xml.rels", t =>
                    t.Contains("template", StringComparison.OrdinalIgnoreCase)
                    || t.EndsWith(".dotx", StringComparison.OrdinalIgnoreCase)
                    || t.EndsWith(".dotm", StringComparison.OrdinalIgnoreCase)
                    || t.StartsWith("file:", StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    /// <summary>
    /// Collects edits against a read-only source package, then writes a brand-new ZIP to avoid Update-mode OPC corruption.
    /// </summary>
    private sealed class OfficePackageEdit
    {
        private readonly ZipArchive _source;
        private readonly Dictionary<string, byte[]> _replacements = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _deletes = new(StringComparer.OrdinalIgnoreCase);

        public OfficePackageEdit(ZipArchive source)
        {
            _source = source;
            EntryNames = source.Entries
                .Select(e => e.FullName)
                .Where(n => !string.IsNullOrEmpty(n) && !n.EndsWith('/'))
                .ToList();
        }

        /// <summary>Non-directory entry names from the source package.</summary>
        public IReadOnlyList<string> EntryNames { get; }

        /// <summary>True when the entry exists in the source and is not marked for delete.</summary>
        public bool HasEntry(string name) =>
            EntryNames.Any(n => n.Equals(name, StringComparison.OrdinalIgnoreCase))
            && !_deletes.Contains(name);

        /// <summary>Marks an entry for omission from the output package.</summary>
        public void Delete(string name) => _deletes.Add(name);

        /// <summary>Queues raw bytes to replace or add an entry.</summary>
        public void ReplaceBytes(string name, byte[] content)
        {
            _deletes.Remove(name);
            _replacements[name] = content;
        }

        /// <summary>Serializes <paramref name="doc"/> and queues it as an entry replacement.</summary>
        public void ReplaceXml(string name, XDocument doc) =>
            ReplaceBytes(name, EncodeXml(doc));

        /// <summary>
        /// Removes XML elements by local name via regex so original namespaces and sibling nodes stay intact.
        /// </summary>
        public void RemoveXmlElementsByLocalName(string entryName, params string[] localNames)
        {
            var original = TryReadBytes(entryName);
            if (original is null || localNames.Length == 0)
                return;

            var text = System.Text.Encoding.UTF8.GetString(original);
            var updated = text;
            foreach (var localName in localNames)
            {
                var name = System.Text.RegularExpressions.Regex.Escape(localName);
                updated = System.Text.RegularExpressions.Regex.Replace(
                    updated,
                    $@"<(?:\w+:)?{name}\b[^>]*>[\s\S]*?</(?:\w+:)?{name}>",
                    string.Empty,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                updated = System.Text.RegularExpressions.Regex.Replace(
                    updated,
                    $@"<(?:\w+:)?{name}\b[^>]*/\s*>",
                    string.Empty,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            if (!string.Equals(updated, text, StringComparison.Ordinal))
                ReplaceBytes(entryName, System.Text.Encoding.UTF8.GetBytes(updated));
        }

        /// <summary>Loads an XML part from pending replacements or the source archive.</summary>
        public XDocument? TryLoadXml(string entryName)
        {
            var bytes = TryReadBytes(entryName);
            if (bytes is null)
                return null;
            using var ms = new MemoryStream(bytes);
            return XDocument.Load(ms, LoadOptions.PreserveWhitespace);
        }

        private byte[]? TryReadBytes(string entryName)
        {
            if (_deletes.Contains(entryName))
                return null;

            if (_replacements.TryGetValue(entryName, out var bytes))
                return bytes;

            var entry = _source.GetEntry(entryName)
                        ?? _source.Entries.FirstOrDefault(e =>
                            e.FullName.Equals(entryName, StringComparison.OrdinalIgnoreCase));
            if (entry is null)
                return null;

            using var stream = entry.Open();
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }

        /// <summary>Removes relationships whose Target matches the predicate.</summary>
        public void RemoveRelationships(string relsPath, Func<string, bool> targetMatch)
        {
            var doc = TryLoadXml(relsPath);
            if (doc?.Root is null)
                return;

            var removed = false;
            foreach (var rel in doc.Root.Elements(Rel + "Relationship").ToList())
            {
                var target = (string?)rel.Attribute("Target") ?? "";
                if (targetMatch(target))
                {
                    rel.Remove();
                    removed = true;
                }
            }

            if (removed)
                ReplaceXml(relsPath, doc);
        }

        /// <summary>Removes Content_Types Override entries whose PartName matches the predicate.</summary>
        public void RemoveContentTypeOverrides(Func<string, bool> partNameMatch)
        {
            var doc = TryLoadXml("[Content_Types].xml");
            if (doc?.Root is null)
                return;

            var removed = false;
            foreach (var ov in doc.Root.Elements(Ct + "Override").ToList())
            {
                var partName = (string?)ov.Attribute("PartName") ?? "";
                if (partNameMatch(partName))
                {
                    ov.Remove();
                    removed = true;
                }
            }

            if (removed)
                ReplaceXml("[Content_Types].xml", doc);
        }

        /// <summary>Writes the edited package to a new ZIP file at <paramref name="outputPath"/>.</summary>
        public void WriteTo(string outputPath)
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);

            using var fs = new FileStream(outputPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            using var zip = new ZipArchive(fs, ZipArchiveMode.Create);

            foreach (var entry in _source.Entries)
            {
                var name = entry.FullName;
                if (string.IsNullOrEmpty(name) || name.EndsWith('/'))
                    continue;
                if (_deletes.Contains(name))
                    continue;

                var outEntry = zip.CreateEntry(name, CompressionLevel.Optimal);
                using var outStream = outEntry.Open();
                if (_replacements.TryGetValue(name, out var bytes))
                {
                    outStream.Write(bytes, 0, bytes.Length);
                }
                else
                {
                    using var inStream = entry.Open();
                    inStream.CopyTo(outStream);
                }
            }

            // Write replacements for paths that did not exist in the source (unusual).
            foreach (var (name, bytes) in _replacements)
            {
                if (_deletes.Contains(name))
                    continue;
                if (_source.Entries.Any(e => e.FullName.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    continue;

                var outEntry = zip.CreateEntry(name, CompressionLevel.Optimal);
                using var outStream = outEntry.Open();
                outStream.Write(bytes, 0, bytes.Length);
            }
        }

        private static byte[] EncodeXml(XDocument doc)
        {
            using var ms = new MemoryStream();
            var settings = new System.Xml.XmlWriterSettings
            {
                Encoding = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                OmitXmlDeclaration = false,
                NewLineHandling = System.Xml.NewLineHandling.None,
            };
            using (var writer = System.Xml.XmlWriter.Create(ms, settings))
                doc.Save(writer);
            return ms.ToArray();
        }
    }

    // ─── PDF ─────────────────────────────────────────────────────────

    /// <summary>Scrubs PDF Info/XMP and optional residuals via a temp file, then overwrites the original.</summary>
    private static void ScrubPdf(string path, ScrubOptions options)
    {
        var temp = IoPath.Combine(IoPath.GetTempPath(), "pb-scrub-" + Guid.NewGuid().ToString("N") + ".pdf");

        try
        {
            using (var document = PdfReader.Open(path, PdfDocumentOpenMode.Modify))
            {
                if (options.DocumentProperties)
                    ClearPdfInfoAndXmp(document);

                if (options.HiddenPathsAndResiduals)
                    ClearPdfResiduals(document);

                // PDFs lack a separate revisions structure; export strategy still clears Info/XMP/residuals.
                document.Save(temp);
            }

            // PdfSharpCore writes its own Producer/Creator on Save; same-length erase avoids breaking xref.
            if (options.DocumentProperties)
                StripPdfWriterIdentity(temp);

            File.Copy(temp, path, overwrite: true);
        }
        finally
        {
            TryDelete(temp);
        }
    }

    /// <summary>Clears Info dictionary privacy fields and removes catalog Metadata (XMP).</summary>
    private static void ClearPdfInfoAndXmp(PdfDocument document)
    {
        var info = document.Info;
        info.Author = string.Empty;
        info.Creator = string.Empty;
        info.Title = string.Empty;
        info.Subject = string.Empty;
        info.Keywords = string.Empty;

        // Producer and similar are read-only on the typed API; remove privacy keys from the dictionary.
        foreach (var key in info.Elements.Keys.ToList())
        {
            if (key is "/Trapped")
                continue;
            info.Elements.Remove(key);
        }

        document.Internals.Catalog.Elements.Remove("/Metadata");
    }

    /// <summary>Removes embedded-file name trees and related catalog leftovers.</summary>
    private static void ClearPdfResiduals(PdfDocument document)
    {
        document.Internals.Catalog.Elements.Remove("/Metadata");

        // Names → EmbeddedFiles
        if (document.Internals.Catalog.Elements.ContainsKey("/Names"))
        {
            var names = document.Internals.Catalog.Elements.GetDictionary("/Names");
            names?.Elements.Remove("/EmbeddedFiles");
            if (names is not null && names.Elements.Count == 0)
                document.Internals.Catalog.Elements.Remove("/Names");
        }

        document.Internals.Catalog.Elements.Remove("/AF");
        document.Internals.Catalog.Elements.Remove("/PieceInfo");
    }

    /// <summary>
    /// After Save, same-length blanks /Producer and /Creator entries in Info so viewers no longer show writer identity.
    /// </summary>
    private static void StripPdfWriterIdentity(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var changed = ErasePdfInfoEntry(bytes, "/Producer"u8)
                      | ErasePdfInfoEntry(bytes, "/Creator"u8);
        if (changed)
            File.WriteAllBytes(path, bytes);
    }

    /// <summary>
    /// Replaces each <c>/Name (...)</c> or <c>/Name &lt;...&gt;</c> occurrence (including the key) with spaces so the key disappears.
    /// </summary>
    private static bool ErasePdfInfoEntry(byte[] data, ReadOnlySpan<byte> name)
    {
        var changed = false;
        var searchFrom = 0;
        while (searchFrom < data.Length)
        {
            var idx = IndexOfBytes(data, name, searchFrom);
            if (idx < 0)
                break;

            if (!IsPdfNameTokenAt(data, idx, name.Length))
            {
                searchFrom = idx + 1;
                continue;
            }

            var p = idx + name.Length;
            while (p < data.Length && IsPdfWhitespace(data[p]))
                p++;

            if (p >= data.Length)
            {
                searchFrom = idx + name.Length;
                continue;
            }

            int valueEnd;
            if (data[p] == (byte)'(')
            {
                valueEnd = FindPdfLiteralStringEnd(data, p);
            }
            else if (data[p] == (byte)'<')
            {
                valueEnd = FindPdfHexStringEnd(data, p);
            }
            else
            {
                searchFrom = idx + name.Length;
                continue;
            }

            if (valueEnd < 0)
            {
                searchFrom = idx + name.Length;
                continue;
            }

            // Blank from /Name through the value terminator so Chrome and similar stop reading Application/Producer.
            changed |= BlankByteRange(data, idx, valueEnd + 1, (byte)' ');
            searchFrom = valueEnd + 1;
        }

        return changed;
    }

    /// <summary>Fills [start, end) with <paramref name="fill"/>; returns true if any byte changed.</summary>
    private static bool BlankByteRange(byte[] data, int startInclusive, int endExclusive, byte fill)
    {
        var changed = false;
        for (var j = startInclusive; j < endExclusive; j++)
        {
            if (data[j] == fill)
                continue;
            data[j] = fill;
            changed = true;
        }

        return changed;
    }

    /// <summary>True when <paramref name="index"/> is a whole PDF name token of the given length.</summary>
    private static bool IsPdfNameTokenAt(byte[] data, int index, int nameLength)
    {
        if (index > 0 && IsPdfNameContinue(data[index - 1]))
            return false;

        var after = index + nameLength;
        if (after < data.Length && IsPdfNameContinue(data[after]))
            return false;

        return true;
    }

    private static bool IsPdfNameContinue(byte b) =>
        (b >= (byte)'A' && b <= (byte)'Z')
        || (b >= (byte)'a' && b <= (byte)'z')
        || (b >= (byte)'0' && b <= (byte)'9')
        || b == (byte)'+' || b == (byte)'-' || b == (byte)'_'
        || b == (byte)'.' || b == (byte)'#';

    private static bool IsPdfWhitespace(byte b) =>
        b == (byte)' ' || b == (byte)'\t' || b == (byte)'\r'
        || b == (byte)'\n' || b == (byte)'\f' || b == 0;

    private static int FindPdfHexStringEnd(byte[] data, int openAngleIndex)
    {
        for (var i = openAngleIndex + 1; i < data.Length; i++)
        {
            if (data[i] == (byte)'>')
                return i;
        }

        return -1;
    }

    private static int FindPdfLiteralStringEnd(byte[] data, int openParenIndex)
    {
        var depth = 0;
        for (var i = openParenIndex; i < data.Length; i++)
        {
            var b = data[i];
            if (b == (byte)'\\')
            {
                i++;
                continue;
            }

            if (b == (byte)'(')
            {
                depth++;
                continue;
            }

            if (b == (byte)')')
            {
                depth--;
                if (depth == 0)
                    return i;
            }
        }

        return -1;
    }

    private static int IndexOfBytes(byte[] data, ReadOnlySpan<byte> pattern, int start)
    {
        if (pattern.Length == 0 || start > data.Length - pattern.Length)
            return -1;

        var last = data.Length - pattern.Length;
        for (var i = start; i <= last; i++)
        {
            var match = true;
            for (var j = 0; j < pattern.Length; j++)
            {
                if (data[i + j] != pattern[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
                return i;
        }

        return -1;
    }

    // ─── Zip / XML helpers ───────────────────────────────────────────

    /// <summary>Best-effort delete of a temp path; failures are ignored.</summary>
    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // ignore temp cleanup
        }
    }
}
