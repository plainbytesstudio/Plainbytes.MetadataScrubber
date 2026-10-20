# Building PlainBytes MetadataScrubber

This guide covers building the application and the Explorer COM shell extension from source on Windows. Official Microsoft Store packages are published separately; this repository does not distribute official installers.

## Prerequisites

- Windows 10 or Windows 11, **64-bit**
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (includes the Windows Desktop / WPF workload for `net8.0-windows`)
- [Visual Studio 2022](https://visualstudio.microsoft.com/) with:
  - **.NET desktop development** (recommended for the WPF app)
  - **Desktop development with C++** (required to build `PlainBytes.MetadataScrubber.Shell`; toolset **v143**)

## Repository layout

```text
PlainBytes.Core/                      Shared shell, Store helpers, shared assets
PlainBytes.MetadataScrubber/          Application project
PlainBytes.MetadataScrubber.Shell/    Explorer COM / IExplorerCommand DLL
PlainBytes.MetadataScrubber.sln       Solution (app + Core + Shell)
LICENSE
README.md
BUILDING.md
CHANGELOG.md
CONTRIBUTING.md
TRADEMARKS.md
THIRD_PARTY_NOTICES.md
assets/                               README screenshots / logo (if present)
```

`PlainBytes.MetadataScrubber` references `PlainBytes.Core` via a project reference.  
`PlainBytes.MetadataScrubber.Shell` is a native **x64-only** DLL. Its resource script loads the icon at:

```text
..\PlainBytes.Core\Assets\Icons\logo_MetadataScrubber.ico
```

Keep `PlainBytes.Core`, `PlainBytes.MetadataScrubber`, and `PlainBytes.MetadataScrubber.Shell` as **siblings** under the repository root so that relative paths resolve.

Microsoft Store / MSIX packaging projects are **not** part of this repository. Official installers are distributed via the Store only.

## Restore and build (CLI) — WPF app

From the repository root (the folder that contains `PlainBytes.Core` and `PlainBytes.MetadataScrubber`):

```powershell
dotnet restore .\PlainBytes.MetadataScrubber\PlainBytes.MetadataScrubber.csproj
dotnet build   .\PlainBytes.MetadataScrubber\PlainBytes.MetadataScrubber.csproj -c Release -p:Platform=x64
```

Output (typical):

```text
PlainBytes.MetadataScrubber\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\
```

Run `PlainBytes.MetadataScrubber.exe` from that folder (unpackaged). Unpackaged builds are not gated by the Microsoft Store trial or in-app purchase check.

### Visual Studio

1. Open `PlainBytes.MetadataScrubber.sln` (or the app `.csproj` together with `PlainBytes.Core`).
2. Set configuration to **Release**, platform to **x64**.
3. Set `PlainBytes.MetadataScrubber` as the startup project, then build and run.

## Build Explorer COM (Shell)

Build the C++ project from a **Visual Studio Developer Command Prompt** (or open the solution in Visual Studio):

```powershell
msbuild .\PlainBytes.MetadataScrubber.Shell\PlainBytes.MetadataScrubber.Shell.vcxproj /p:Configuration=Release /p:Platform=x64
```

Output (typical):

```text
PlainBytes.MetadataScrubber.Shell\bin\x64\Release\PlainBytes.MetadataScrubber.Shell.dll
```

The official Store package registers this DLL as a `com:SurrogateServer` Explorer command. That packaging project is **not** in this public drop.

For unpackaged debugging without the COM DLL, the app also supports classic Explorer verb helpers under HKCU:

- `--register-shell-verb` / `--unregister-shell-verb`
- Default scrub CLI: `--scrub-default`

Rebuilding and packaging the COM DLL yourself is optional for local Explorer integration experiments.

## Dependencies

| Package | Role |
| --- | --- |
| UglyToad.PdfPig (`1.7.0-custom-5`) | PDF helpers |
| PdfSharpCore | PDF rewrite / save (transitively restores SharpZipLib / SixLabors.Fonts / ImageSharp) |
| Microsoft.Toolkit.Uwp.Notifications | Desktop toasts |
| WPF-UI | Fluent-style controls |

### PdfPig package version

The project references:

```text
UglyToad.PdfPig Version="1.7.0-custom-5"
```

This is a normal NuGet `PackageReference`. Version `1.7.0-custom-5` is published on [nuget.org](https://www.nuget.org/packages/UglyToad.PdfPig/1.7.0-custom-5) and restores from the public feed like the other dependencies. The `custom-5` suffix is a pre-release / customized package version id, not a local-only package.

See also `THIRD_PARTY_NOTICES.md`.

## What this guide does not cover

- Creating or signing **MSIX / Microsoft Store** packages (Partner Center and local certificates)
- Publishing Store add-on / purchase configuration
- Building other PlainBytes Suite products that are not part of this repository drop

## Troubleshooting

| Issue | What to check |
| --- | --- |
| Restore fails on NuGet | Network access to nuget.org; correct .NET 8 SDK; retry `dotnet restore` |
| WPF / Windows TFM errors | Install .NET 8 SDK with Windows Desktop support |
| Shell project / `.vcxproj` fails | Install VS **Desktop development with C++** (v143); build **x64** only |
| Shell.rc cannot find icon | Confirm sibling layout and that `PlainBytes.Core\Assets\Icons\logo_MetadataScrubber.ico` exists |
| Project reference not found | Keep `PlainBytes.Core`, `PlainBytes.MetadataScrubber`, and `PlainBytes.MetadataScrubber.Shell` as siblings |

## License

Source code is licensed under **GNU GPLv3**, including the Shell COM sources. See [LICENSE](./LICENSE).
