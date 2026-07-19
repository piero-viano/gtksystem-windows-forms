# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this repository is

`GTKSystem.Windows.Forms` (EasyWebFactory, MIT) is a WinForms-compatibility framework built on GTK3 via GtkSharp: existing C# WinForms apps compile once and run cross-platform (Windows, Linux, macOS) while still using the native Visual Studio WinForms designer. Ships on NuGet as `GTKSystem.Windows.Forms`. Primary upstream is Gitee (`FetchGitee.cmd` fetches from a `gitee` remote); comments and docs are largely in Chinese.

**Note:** this repository is also vendored as the `gtksystem-windows-forms` submodule of the `Net4x.GtkWindowsForms` fork (parent directory). When working from the parent workspace, changes made here are the "upstream logic" layer that the parent's prepare pipeline regenerates its library from — see the parent's CLAUDE.md.

## Commands

```sh
# Build everything (library + samples)
dotnet build GTKWinFormsApp.sln

# Run the main sample app (edit Program.cs to choose which Form to launch)
dotnet run --project Samples/GTKWinFormsApp/GTKWinFormsApp.csproj
```

There are no test projects in this solution. Running apps requires a GTK3 runtime (on Windows, see Dependencies/README.md → the Gitee GTK-for-Windows repo; prebuilt GtkSharp DLLs are in `Libs/`).

## Architecture

### The namespace-replacement trick

The library declares its public types directly in the **`System.Windows.Forms`** (and related `System.*`) namespaces. Consuming apps set `<UseWindowsForms>False</UseWindowsForms>` and reference `GTKSystem.Windows.Forms` instead of Microsoft's WinForms — so unmodified WinForms source (including `.Designer.cs` files) compiles against this library.

Designer support uses a **second csproj over the same sources**: `Samples/GTKWinFormsApp/GTKWinFormsApp.csproj` is the real (GTK) app, while `GTKWinFormsApp_Designer.csproj` sets `UseWindowsForms=True` so Visual Studio's WinForms designer works. The designer csproj excludes the GTK-only override files (`System.ComponentModel.cs`, `System.Resources.ResourceManager.cs`, `App.config`).

### Control wrapping pattern (Source/GTKSystem.Windows.Forms)

Every WinForms control is a thin public class in `GTKControls/` (namespace `System.Windows.Forms`) wrapping a GTK-backed widget from `GTKControls/ControlBase/`:

- `GTKControls/<Name>.cs` — public API surface, e.g. `Button` exposes `public readonly ButtonBase self` and `public override object GtkControl => self;`. Properties translate WinForms semantics to GTK (e.g. `ContentAlignment` → `Xalign`/`Yalign`).
- `GTKControls/ControlBase/<Name>Base.cs` — the actual `Gtk.Widget` subclass. Each implements `IControlGtk` and carries a `GtkControlOverride` (`self.Override.sender = this` in the wrapper ctor), which handles custom drawing/paint-event bridging back to WinForms `OnPaint` etc.
- Complex controls get their own subfolder of collaborating types (`DataGridViewBase/`, `ListViewBase/`, `MenuStripBase/`, `TableLayoutBase/`, ...). `GTKControls/Drawing/` holds the `System.Drawing`-compatible types (Graphics over Cairo).

Other key areas of the library:
- `Application/` — `Application`, `ApplicationContext`, message loop over GTK.
- `GtkSystem/Resources/`, `GtkSystem/ComponentModel/` — `GTKSystem.Resources.ResourceManager` / `GTKSystem.ComponentModel.ComponentResourceManager`, reimplementations that can read project resx resources and image files.
- `Resources/` — embedded cursors/icons loaded at runtime by assembly resource name.
- Some folders are excluded from compilation in the csproj (`GTKControls/Interface/**`, `GTKControls/Struct/**`, plus an explicit `<Compile Remove>` list) — check the csproj before assuming a file is built.

The library multi-targets `netstandard2.0;net8.0;net10.0`, has `GeneratePackageOnBuild` enabled, and pins `GtkSharp` 3.24.24.x.

### Source/System.Resources.Extensions

A standalone reimplementation of `System.Resources.Extensions` (resx binary deserialization, `ImageListStreamer`, etc.) used so designer-generated resx content loads without real WinForms. A prebuilt copy is checked into `Libs/`.

### Resource conventions for consuming apps (Readme_Resources.md)

- Image resources cannot be read from compiled resx directly; apps must copy the image files into a `Resources/` folder next to the executable (the sample csproj does this with `CopyToOutputDirectory`).
- To use `Properties/Resources.resx` or per-form `Form.resx` images, the app defines its own `System.Resources.ResourceManager` and `System.ComponentModel.ComponentResourceManager` classes inheriting the `GTKSystem.*` equivalents, shadowing the BCL classes (see `Samples/GTKWinFormsApp/System.Resources.ResourceManager.cs` and `System.ComponentModel.cs` for the canonical implementation to copy).
- `ImageList` images referenced by key in `.Designer.cs` must exist as files under `Resources/` (or `Resources/<FormName>/`).
