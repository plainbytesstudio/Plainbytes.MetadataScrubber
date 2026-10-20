# Third-Party Notices

PlainBytes MetadataScrubber includes the following third-party components.
Their licenses are retained; this notice does not relicense them under GPLv3.

Direct NuGet `PackageReference` entries in `PlainBytes.MetadataScrubber.csproj`:

| Package | License | Notes |
| --- | --- | --- |
| UglyToad.PdfPig | Apache-2.0 (typical upstream) | PDF metadata helpers; NuGet version `1.7.0-custom-5` from nuget.org; keep upstream attribution |
| PdfSharpCore | MIT | PDF rewrite / save path used in scrubbing |
| Microsoft.Toolkit.Uwp.Notifications | MIT | Desktop toast notifications |
| WPF-UI (Lepo) | MIT | Fluent UI controls (`WPF-UI` / `WPF-UI.Abstractions`) |

Notable transitive packages restored with the above (not a complete closure):

| Package | License | Notes |
| --- | --- | --- |
| SharpZipLib | MIT | Pulled by PdfSharpCore |
| SixLabors.Fonts | Six Labors Split License | Pulled by PdfSharpCore |
| SixLabors.ImageSharp | Six Labors Split License | Pulled by PdfSharpCore only (not a direct app reference). As a transitive dependency, Apache-2.0 applies under the Split License criteria in the package `LICENSE` |

## First-party components (GPLv3)

These are part of the MetadataScrubber Corresponding Source under the same GPLv3 terms as the application (unless a file states otherwise):

| Component | Role |
| --- | --- |
| `PlainBytes.Core` | Shared WPF shell, Store/trial helpers, shared assets (also references WPF-UI) |
| `PlainBytes.MetadataScrubber` | Main WPF application |
| `PlainBytes.MetadataScrubber.Shell` | Explorer context-menu COM surrogate (`IExplorerCommand` DLL) shipped in the Store package |

Microsoft Store / MSIX packaging projects are not published in the public source drop; the Shell COM sources above are.

Brand names, logos, and product marks of PlainBytes Studio remain trademarked and are not licensed under GPLv3. See `TRADEMARKS.md` at the root of the public source drop (staged as `github/TRADEMARKS.md` in the Suite monorepo).
