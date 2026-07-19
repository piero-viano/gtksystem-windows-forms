# Architecture

This document describes the structure and internal architecture of the **GTKSystem.Windows.Forms** repository (the sample applications under `Samples/` are intentionally out of scope).

## Overview

GTKSystem.Windows.Forms is a reimplementation of the `System.Windows.Forms` (and part of the `System.Drawing`) API surface on top of **GTK3** via **GtkSharp**. The goal is source-level compatibility: an existing WinForms application — including its designer-generated `.Designer.cs` files — compiles unchanged against this library and runs on Windows, Linux, and macOS.

The core trick is **namespace replacement**: the library declares its public types directly in the `System.Windows.Forms`, `System.Drawing`, `System.Resources`, etc. namespaces. A consuming application sets `<UseWindowsForms>False</UseWindowsForms>` and references this library instead of Microsoft's WinForms, so all existing `using System.Windows.Forms;` code resolves to these types.

## Repository layout

| Path | Contents |
|---|---|
| `Source/GTKSystem.Windows.Forms/` | The main library (everything below is about this project). |
| `Source/System.Resources.Extensions/` | Standalone reimplementation of `System.Resources.Extensions` (binary resx deserialization without real WinForms). |
| `Libs/` | Prebuilt binaries: GtkSharp assemblies (`GtkSharp`, `GdkSharp`, `CairoSharp`, `GLibSharp`, `AtkSharp`, `GioSharp`, `PangoSharp`) and a prebuilt `System.Resources.Extensions.dll`. |
| `Dependencies/` | Pointer to the GTK runtime for Windows (Gitee `GTK-for-Windows` repository). |
| `VisualStudio插件/` | Visual Studio extension (project templates / designer tooling), shipped as a zip. |
| `GTKWinFormsApp.sln` | Solution: library + resources extensions + samples. |

The main library project multi-targets `netstandard2.0;net8.0;net10.0`, pins `GtkSharp` 3.24.24.x, and has `GeneratePackageOnBuild` enabled (NuGet package `GTKSystem.Windows.Forms`). Note that several folders are **excluded from compilation** in the csproj (`GTKControls/Interface/**`, `GTKControls/Struct/**`, plus an explicit `<Compile Remove>` list) — always check the csproj before assuming a file is built.

## The main library (`Source/GTKSystem.Windows.Forms`)

Top-level layout:

- `Application/` — `Application`, `ApplicationContext`, `FormCollection`, input-language stubs; app lifecycle and the GTK main loop.
- `GTKControls/` — every control and its supporting types. Public wrapper classes live directly in this folder; GTK widget implementations live in `ControlBase/`; complex controls have their own subfolders (`DataGridViewBase/`, `ListViewBase/`, `MenuStripBase/`, `TableLayoutBase/`, `TreeViewBase/`, `ComboBoxBase/`, `PropertyGridBase/`, `MonthCalendarBase/`, `TabControlBase/`, ...).
- `GTKControls/Control/` — the root `Control` class plus `ContainerControl`, `ScrollableControl`, `ListControl`, cursors, and the many small WinForms interfaces (`IWin32Window`, OLE interfaces, etc.).
- `GTKControls/Drawing/` — the `System.Drawing` compatibility layer (`Graphics`, `Pen`, `Brush`, `Font`, `Region`, `StringFormat`, `Icon`, `SystemColors`, `Drawing2D/`, `Text/`, `Printing/`).
- `GTKControls/Dialog/` — common dialogs (`OpenFileDialog`, `SaveFileDialog`, `ColorDialog`, `FontDialog`, `FolderBrowserDialog`, `MessageBox/`) mapped to GTK choosers.
- `GTKControls/Enum|EventArgs|EventHandlers/` — verbatim replicas of the WinForms enum/event API surface so delegate signatures match.
- `GtkSystem/` — non-control support code: `Screen`, `Resources/` (ResX reading, see below), `ComponentModel/` (`ComponentResourceManager`).
- `Resources/` — embedded cursors (`Resources/Cursors/*.cur`) and system images/icons (`Resources/System/*`), loaded at runtime by assembly manifest resource name.
- `Microsoft.Win32/`, `System.Diagnostics.CodeAnalysis/`, `System.Runtime.CompilerServices/` — small shims so WinForms-era code compiles on `netstandard2.0`.

### Application lifecycle and the main loop

`System.Windows.Forms.Application` ([Application/Application.cs](Source/GTKSystem.Windows.Forms/Application/Application.cs)) owns GTK initialization. Its static constructor calls `Init()`, which:

1. Calls `Gtk.Application.Init()` and creates a single `Gtk.Application` (`Application.App`).
2. Creates `Resources/` and `theme/` directories next to the executable if missing.
3. Loads a large **built-in CSS stylesheet** that restyles GTK widgets to look and behave like WinForms controls. Every control adds CSS classes (e.g. `.DefaultThemeStyle`, `.ListView`, `.ToolStrip`, `.NumericUpDown`) that this stylesheet targets.
4. Reads (or generates on first run) `theme/setup.theme`, an INI-style file letting an app opt out of the default style, force a GTK theme name (`AutoTheme`/`DefaultThemeName`), point at a custom theme folder (`GTK_DATA_PREFIX`), or append a user CSS file (`theme/style.css`).
5. Loads `icon.png` from the app directory as the default window icon, if present.

`Application.Run(mainForm)` shows the form and enters `Gtk.Application.Run()`; the main form's `Destroyed` signal quits the loop. `DoEvents()` drains the GLib/GTK event queues. `OpenForms` is reconstructed on demand by walking `Gtk.Window.ListToplevels()` and reading each window's `Data["Control"]` back-pointer (see below).

### The three-layer control model

Every control is split across three cooperating types:

```
System.Windows.Forms.Button          (public API — WinForms names/semantics)
        │  exposes `self`, `GtkControl`
        ▼
ButtonBase : Gtk.Button, IControlGtk (GTK widget subclass in GTKControls/ControlBase/)
        │  carries
        ▼
GtkControlOverride                   (per-widget paint/back-color/image state + Paint events)
```

1. **Public wrapper** (`GTKControls/<Name>.cs`, namespace `System.Windows.Forms`): derives from `Control` (which itself derives from `Component`, *not* from a GTK type), holds `public readonly <Name>Base self` and overrides `object GtkControl => self`. Properties translate WinForms semantics into GTK ones (e.g. `Button.TextAlign` → `Xalign`/`Yalign`, `RightToLeft` → `Gtk.TextDirection`).
2. **Widget base** (`GTKControls/ControlBase/<Name>Base.cs`): the actual `Gtk.Widget` subclass. Implements `IControlGtk`, exposing a `GtkControlOverride Override` instance. The wrapper's constructor sets `self.Override.sender = this` so paint events surface with the WinForms control as sender.
3. **`GtkControlOverride`** ([ControlBase/GtkControlOverride.cs](Source/GTKSystem.Windows.Forms/GTKControls/ControlBase/GtkControlOverride.cs)): shared drawing helper holding `BackColor`, `BackgroundImage(+Layout)`, `Image(+Align)` and raising two events from the widget's Cairo draw pass: `PaintGraphics(Cairo.Context, Rectangle)` (raw) and `Paint(PaintEventArgs)` (WinForms-compatible, wrapping the Cairo context in a `System.Drawing.Graphics`). Widget bases call `Override.OnDrawnBackground/OnDrawnImage/OnPaint` from their `Drawn` handlers.

The root `Control` class ([GTKControls/Control/Control.cs](Source/GTKSystem.Windows.Forms/GTKControls/Control/Control.cs)) wires everything up in its constructor: stores a back-pointer in `Widget.Data["Control"]` (used by `Application.OpenForms`, form close handling, and hit-testing), adds the `DefaultThemeStyle` CSS class, and subscribes to `Realized`, `SizeAllocated`, `ParentSet`, and `WidgetEvent`. WinForms `Load` fires on first GTK `Realized`.

### Event bridging

All input events funnel through one handler, `Control.Widget_WidgetEvent`, which pattern-matches raw `Gdk.Event` types and re-raises WinForms events with compatible args:

- `MotionNotify` → `MouseMove`; `ButtonPress/Release` → `MouseDown`, `Click`, `MouseClick`, `MouseUp` (button 3 also pops up the control's `ContextMenuStrip`); `TwoButtonPress` → `MouseDoubleClick`/`DoubleClick`; touch events are mapped to the equivalent mouse events. Coordinates are recomputed from root coordinates minus the widget window origin.
- `KeyPress/KeyRelease` → `KeyDown`/`KeyPress`/`KeyUp`. A static `keyboardMap` table translates `Gdk.Key` values to Win32 virtual-key codes so `Keys` enum values (and `Alt`/`Control`/`Shift` modifier flags) match real WinForms. `SuppressKeyPress`/`Handled` are honored by setting the GTK event's `RetVal`.

Beyond this generic path, each wrapper connects widget-specific GTK signals to the corresponding WinForms events (e.g. `Gtk.Button.Clicked`, `Gtk.Adjustment.ValueChanged` → `Scroll`).

### Layout: absolute positioning over GTK

WinForms uses absolute child coordinates; GTK does not. The bridge:

- **Containers are `Gtk.Overlay`s** (usually inside a `Gtk.ScrolledWindow`). Child widgets are overlay children, positioned by setting margins: `Control.Left/Top` map to `Widget.MarginStart/MarginTop`, and `Width/Height` map to `WidthRequest/HeightRequest`. A `Gtk.DrawingArea` is added as the overlay's main child to act as the background/paint surface.
- **`Dock` and `Anchor`** are computed manually (`Control.SetDockStyle` / `SetAnchorStyle`): on realize/parent-set/resize the code reads the parent overlay's allocation and derives `Halign`/`Valign` (`Fill`/`Start`/`End`) plus compensating end/bottom margins. `ScrollableBoxBase` (a `Gtk.ScrolledWindow` subclass) is the common base for scrollable containers and converts `Gtk.Adjustment` changes into WinForms `Scroll` events; `AutoScroll` toggles the scrollbar policies.
- Layout-managed containers (`TableLayoutPanel`, `FlowLayoutPanel`) instead map onto real GTK containers (`Gtk.Grid`, flow boxes) in their own base classes.

### Forms and dialogs

`FormBase` ([ControlBase/FormBase.cs](Source/GTKSystem.Windows.Forms/GTKControls/ControlBase/FormBase.cs)) is a **`Gtk.Dialog`** (not a plain window), which lets the same class serve `Form.Show()` and modal `Form.ShowDialog()` (`Run()`/`Respond()`). Structure: dialog content area → `ScrolledWindow` → `Gtk.Overlay` (+ background `DrawingArea`). Because GTK header bars are client-side, `FormBase` builds its own minimize/maximize buttons in the `HeaderBar` and tracks window-state events to swap the maximize/restore icon; `MaximizeBox`/`MinimizeBox` toggle their visibility. Closing is routed through a `CloseWindowEvent` delegate so the `Form` wrapper can run `FormClosing` (cancelable) before the GTK window is destroyed. `Application.Run` special-cases `ShowInTaskbar == false` by parenting the form to a hidden 1×1 toplevel.

### Painting and the System.Drawing layer

`GTKControls/Drawing/` reimplements `System.Drawing` on **Cairo**: `Graphics` wraps a `Cairo.Context` plus clip rectangle; pens/brushes translate to Cairo sources; fonts and text measurement go through Pango; `Icon`, `Region`, `Drawing2D` (paths, matrices, gradients) and `Printing` are implemented on the same substrate. `Control.CreateGraphics()` creates a similar surface from the widget's `Gdk.Window`, and `OnPaint`/`Paint` receive a `Graphics` built from the widget's live draw context. `Image`/`Bitmap` are backed by `Gdk.Pixbuf` (`GtkControlOverride` renders them for `Image`/`BackgroundImage` with all `ImageLayout`/`ContentAlignment` modes).

### Threading model

`Control.Invoke`/`BeginInvoke` are implemented over `Task.Factory.StartNew` — they do **not** marshal onto the GTK main thread the way real WinForms marshals to the UI thread, and `InvokeRequired` is effectively a stub. Code that must touch widgets from background threads needs GLib idle dispatch; be careful when porting logic that assumes real WinForms marshaling semantics.

### Resource pipeline

WinForms designers persist images/state into `.resx` → embedded `.resources` blobs that normally require `System.Resources.Extensions` + BinaryFormatter-era serialization. This repo replaces that whole stack:

- **`GtkSystem/Resources/`** contains a full ResX reader/writer (`ResXResourceReader/Writer`, `ResXDataNode`, `ResXFileRef`, type-resolution services) and `GTKSystem.Resources.ResourceManager`, which reads embedded `.resources` streams via `GTKSystem.Resources.Extensions.DeserializingResourceReader` and **falls back to loose files on disk**: a `Resources/` folder next to the executable (e.g. `Resources/<FormName>.resx`, plain image files for `ImageList` keys). This is why consuming apps must copy image assets to an output `Resources/` folder.
- **`GtkSystem/ComponentModel/ComponentResourceManager`** builds on it for designer `resources.GetObject(...)`/`ApplyResources` calls.
- Consuming apps activate this by declaring their own `System.Resources.ResourceManager` and `System.ComponentModel.ComponentResourceManager` classes inheriting the `GTKSystem.*` ones, shadowing the BCL types at compile time.
- **`Source/System.Resources.Extensions/`** is the sibling project providing the `DeserializingResourceReader`/`PreserializedResourceWriter` implementation compatible with designer-emitted binary resources; a prebuilt copy sits in `Libs/`.

### Theming/customization surface for apps

At runtime an application (or end user) can restyle everything without recompiling: `theme/setup.theme` selects GTK theme/custom CSS, `theme/style.css` overrides the built-in stylesheet (all controls carry stable CSS class names), and `icon.png` sets the default window icon.
