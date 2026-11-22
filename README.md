# BG3 Pak Viewer

A Windows desktop tool for browsing, previewing, and exporting assets from Baldur's Gate 3 `.pak` archives.

Built with WPF and .NET 10, it opens a package, shows its full file tree, previews files by type, and exports a single file, a folder, or a specific resource kind (icon atlases, virtual textures, localized text, and more) to disk.

## Features

- Open and browse `.pak` packages, including drag-and-drop and recent files
- Search the package tree by name; the title-bar search box filters the active preview
- Preview support for:
  - Plain text
  - Images (DDS / bitmap), with automatic icon-atlas detection and grid slicing
  - 3D models (HelixToolkit SharpDX)
  - Localization tables
  - Larian resource trees
  - Story scripts (goal list plus decompiled script)
  - Virtual textures (cross-file index and per-layer preview)
- Export:
  - Export the selected file or a whole folder
  - Type-specific export strategies (images, models, audio, localization, Larian resources, virtual textures, raw files)
  - Per-icon atlas export and per-layer virtual texture export
- Virtual texture toolbox: indexes every `.gts` in the package, previews layers, and batch-exports `.dds`
- UI in English, Simplified Chinese, Traditional Chinese, and Italian
- Settings, log viewer, recent files, about, and update check

## Requirements

- Windows 10 version 1803 or later (target framework `net10.0-windows10.0.18362.0`)
- .NET 10 SDK
- **x64 only**: every project targets `Platforms=x64`, and the native dependencies are x64-only

## External dependencies

The following files are not supplied by NuGet and must be provided locally:

| Dependency | Notes |
|---|---|
| `LSLib.dll` | Package and game-asset parsing library. The `HintPath` is currently hard-coded to `D:\Tools\Packed\LSLib.dll` in `BG3PakViewer`, `BG3PakViewer.Controls`, `BG3PakViewer.Loader`, and `BG3PakViewer.VirtualTextures`; update those csproj files if LSLib lives elsewhere |
| `granny2.dll` | Native library required for 3D model parsing, currently copied from `D:\Tools\Packed\granny2.dll` |
| `Syncfusion.lic` | Syncfusion license file, embedded from `BG3PakViewer/Syncfusion.lic` |
| `sixlabors.lic` | ImageSharp license file, placed in `BG3PakViewer.Loader`, `BG3PakViewer.Shared`, and `BG3PakViewer.IconAtlas`; missing licenses fail the Release build |

All other dependencies (ImageSharp, OpenCvSharp, BCnEncoder.Net, Pfim, HelixToolkit, CommunityToolkit.Mvvm, Serilog, and so on) are restored from NuGet.

## Build and run

```powershell
# Restore and build (the x64 platform is required)
dotnet build BG3PakViewer.slnx -p:Platform=x64

# Run
dotnet run --project BG3PakViewer/BG3PakViewer.csproj --property:Platform=x64

# Release build
dotnet build BG3PakViewer.slnx -c Release -p:Platform=x64
```

Notes:

- The solution uses the newer `.slnx` format
- The Release configuration enables `TreatWarningsAsErrors`, so any compiler warning fails the build
- The version is generated from Git history by Nerdbank.GitVersioning

## Project layout

| Project | Responsibility |
|---|---|
| `BG3PakViewer` | WPF application: startup and dependency injection, main window, preview handlers, export strategies, settings and logging |
| `BG3PakViewer.Contracts` | Cross-project interfaces and data models (for example `IPackageService`, `IPreviewHandler`, `IExportService`) |
| `BG3PakViewer.Controls` | Preview controls, data templates, preview view models, and their export logic |
| `BG3PakViewer.Dialogs` | Modal dialogs: settings, recent files, log, about, and the virtual texture toolbox |
| `BG3PakViewer.IconAtlas` | OpenCV-based icon-atlas grid detection and icon slicing |
| `BG3PakViewer.Loader` | Package loading and asset parsing (images, models, audio, text, and so on) |
| `BG3PakViewer.Locales` | Localized resource files |
| `BG3PakViewer.NexusIntegration` | Nexus Mods integration (ZeroQL GraphQL client) |
| `BG3PakViewer.Shared` | Shared models, behaviors, extension methods, and base view models |
| `BG3PakViewer.Utils` | File-extension, text-processing, path, and other general-purpose helpers |
| `BG3PakViewer.VirtualTextures` | Virtual texture (GTS/GTP) indexing, layer extraction, and DDS output |

## Technology stack

- .NET 10 / WPF
- CommunityToolkit.Mvvm (MVVM and source generators)
- LSLib (package and game-asset formats)
- SixLabors.ImageSharp, BCnEncoder.Net, Pfim, OpenCvSharp (image processing)
- HelixToolkit.Wpf.SharpDX (3D preview)
- Syncfusion SfGrid (localization grid)
- iNKORE.UI.WPF.Modern (Fluent-style controls and themes)
- Serilog (logging)

## Localization

UI strings live in `BG3PakViewer.Locales`:

- `Strings.resx` (default, English)
- `Strings.zh-hans.resx` (Simplified Chinese)
- `Strings.zh-hant.resx` (Traditional Chinese)
- `Strings.it.resx` (Italian)

When adding or changing text, update every language file together with `Strings.Designer.cs`.
