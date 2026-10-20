# Contributing to PlainBytes MetadataScrubber

Thanks for your interest in improving PlainBytes MetadataScrubber. This project is licensed under the **GNU General Public License v3.0 (GPLv3)**. Contributions are licensed under GPLv3 as well. You retain copyright in your contributions; submitting a contribution does not assign that copyright to PlainBytes Studio.

## How to contribute

### Bug reports

Open a GitHub issue and include, when possible:

- App version (About page) and whether you used the **Microsoft Store** build or a **self-built** unpackaged binary
- Windows version (10/11) and architecture (x64)
- Steps to reproduce (in-app scrub vs File Explorer **default cleanup**, if relevant)
- Expected vs actual behavior
- Document type(s) and extension (for example `.docx`, `.docm`, `.xlsx`, `.xlsm`, `.pptx`, `.pptm`, `.pdf`) and which cleanup strategy was used
- Do **not** attach confidential documents unless you can share a redacted sample

### Feature requests

Open an issue describing the workflow, the formats involved, and why existing strategies or Explorer default cleanup are insufficient. Keep the product scope in mind:

- **In product scope:** local metadata review and cleanup for supported Office and PDF files
- **Out of product scope:** cloud processing, OCR pipelines, or guaranteeing anonymization of **visible** document content

Explorer context-menu integration in this product is the **default cleanup** path only (not arbitrary custom strategies from Explorer).

### Pull requests

1. Discuss larger changes in an issue first.
2. Keep changes focused; prefer small, reviewable PRs.
3. Follow existing code style (C# / WPF code-behind and `INotifyPropertyChanged` patterns, WPF-UI controls; C++17 for `PlainBytes.MetadataScrubber.Shell`).
4. Do not add network calls for metadata inspection or scrubbing. Processing must remain **local**.
5. Do not commit secrets, certificates (`.pfx`), Partner Center credentials, or private Store signing material.
6. Update or add docs when behavior users rely on changes (`README.md`, `BUILDING.md`, `CHANGELOG.md` as appropriate).
7. Add an entry under the `Unreleased` section of `CHANGELOG.md` for user-visible changes.

## Development setup

See [BUILDING.md](./BUILDING.md) for prerequisites and build steps (WPF app and optional Shell COM).

## Project boundaries

| In scope | Out of scope (for typical contributions) |
| --- | --- |
| Scrub UX, strategies, Explorer default cleanup, help copy, localization | Turning the app into a cloud service |
| Bug fixes in Office/PDF metadata read and scrub paths | Guaranteeing complete anonymization of visible document content |
| Build docs for open-source builds (incl. Shell COM) | Requiring purchase to exercise GPLv3 rights on self-built copies |
| | Shipping Microsoft Store / MSIX packaging projects in this public drop |

Brand names, logos, and product marks of **PlainBytes Studio** remain trademarked and are not licensed under GPLv3. Forks must not present themselves as official PlainBytes Studio / Microsoft Store releases. This does not restrict accurate statements of origin, such as naming PlainBytes MetadataScrubber when describing that a build is based on or derived from this project. See [TRADEMARKS.md](./TRADEMARKS.md).

## License and Store builds

- PlainBytes MetadataScrubber is licensed under GPLv3. Users may modify, build, and redistribute the source code under the terms of the license, including modifying or removing trial and purchase checks in their own builds.
- The official Microsoft Store version offers a 7-day free trial followed by a one-time purchase (U.S. list price $3.50 USD; pricing may vary by region). These commercial distribution terms do not restrict the rights granted under GPLv3.

## Code of conduct (short)

Be respectful in issues and reviews. Do not submit malicious content, credentials, or others’ private data.

## Questions

- Product / website: [https://plainbytes.studio](https://plainbytes.studio)
- Store listing: [Microsoft Store](https://apps.microsoft.com/detail/9NPG7C2ZKHWS)
- Email: plainbytes.studio@gmail.com
