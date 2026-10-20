# Changelog

All notable changes to PlainBytes MetadataScrubber are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project aims to follow [Semantic Versioning](https://semver.org/) for application releases.

### Corresponding Source

When an official Microsoft Store package is published, tag this repository (and preferably create a GitHub Release) with the complete four-part Store package version—for example Store `1.0.4.0` → `v1.0.4.0`. A tag alone does not establish GPLv3 Corresponding Source; the tagged tree must include the materials required to build and modify the application (including `PlainBytes.Core`, the app project, and `PlainBytes.MetadataScrubber.Shell`). The in-app tool version shown in About may use a shorter form; use the Store package version when matching tags.

See the [Corresponding Source](./README.md#corresponding-source-store-builds) note in `README.md`.

## [Unreleased]

## [1.0.0] — 2026-10-02

Initial public application release.

### Added

- Inspect supported metadata and clear it according to a cleanup strategy for Word (`.docx` / `.docm`), Excel (`.xlsx` / `.xlsm`), PowerPoint (`.pptx` / `.pptm`), and PDF (`.pdf`)
- Built-in cleanup presets (export / archive / minimal) and custom strategies persisted locally in the app
- Batch processing with per-file results
- File Explorer context-menu **default cleanup** via COM shell extension (`PlainBytes.MetadataScrubber.Shell`)
- Local, offline processing on Windows
- In-app help center
- Microsoft Store distribution with a 7-day trial and one-time purchase unlock for the official packaged build
- Multilingual UI resources (including en, zh-Hans, zh-Hant, ja, ko, de, fr, es, it, pt, nl, ru, ar)

### Notes

- Metadata cleaning does not guarantee complete anonymization; visible document content is not automatically anonymized
- Cleaning may overwrite the original files; back up important documents before running a cleanup
- Unpackaged builds from this source tree are not gated by Store trial or purchase checks

---

Link tags in this file to GitHub releases when those tags are published, for example:

- `[1.0.0]: https://github.com/plainbytesstudio/PlainBytes.MetadataScrubber/releases/tag/v1.0.0.0`
