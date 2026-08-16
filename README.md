# Markdown Viewer




A fast, lightweight Windows desktop application for **viewing and editing**
Markdown files. Built with C# / .NET 8 / WPF. Native rendering — no browser
engine.
sss
## Features

### v0.1 — viewer core

- Double-click / command-line open of `.md` and 
markdown` files
- GitHub-flavoured Markdown rendering (headings, lists, tables, blockquotes,
  code blocks, links, images, task lists, strikethrough)
- Native WPF `FlowDocument` rendering — reflows on resize without re-parsing
- Relative images resolve against the opened file's directory
- HTTP/HTTPS links open in the user's default browser
- Graceful handling of missing files, malformed Markdown, and missing images

### v0.2 — editor & productivity

- **Markdown editing** — side-by-side editor + live preview with spell
  checking (native WPF `SpellCheck`), word-wrap, configurable font
- **Save / Save As / Autosave** — timer-based autosave to disk, `Ctrl+S` /
  `Ctrl+Shift+S`
- **Tabs** — multiple documents in one window, close buttons, dirty
  indicators, `Ctrl+N` new tab
- **Recent documents** — persisted in `%APPDATA%`, surfaced in the File menu
- **Favourites** — pin files, persisted, quick-open from the Favourites menu
- **In-document search** — `Ctrl+F` search bar with match navigation
- **Preferences window** — theme, editor options, autosave, panels,
  telemetry/update opt-in; live theme switch
- **Themes** — Light / Dark via `ResourceDictionary` swap, persisted
- **Markdown linting** — lightweight regex linter (MD009/010/012/018/026/
  034/098) with a clickable issues panel
- **Git integration** — read-only status of the current file's repository
  (branch, modified/added/deleted entries) via the `git` CLI
- **File browser sidebar** — TreeView of the current directory, double-click
  to open
- **Printing** — `Ctrl+P` via `PrintDialog` + `FlowDocument`
- **PDF export** — "Microsoft Print to PDF" routed through the print dialog
- **HTML export** — standalone HTML file via Markdig `ToHtml`
- **Update check** — opt-in HTTP version check against GitHub releases
- **Telemetry** — local-only, opt-in JSONL event log in `%APPDATA%`; nothing
  is ever sent over the network
- **Plugins** — simple `IMarkdownViewerPlugin` contract; load DLLs from
  `%APPDATA%\MarkdownViewer\plugins\`
- **About dialog** — extracted to its own window

## Architecture

See [`plans/architecture.md`](plans/architecture.md) for the full design and
the rationale for the native `FlowDocument` approach over WebView2.

Pipeline:

```
Markdown file → UTF-8 reader → Markdig parser (AST) → FlowDocumentBuilder → FlowDocument → viewer
```

The editor pane is a plain WPF `TextBox`; on each change the content is
re-parsed through the shared `MarkdownService.Pipeline` and the preview
`FlowDocument` is rebuilt.

## Project structure

```
src/MarkdownViewer/
    App.xaml / App.xaml.cs              # startup: settings, plugins, args
    MainWindow.xaml / .xaml.cs          # shell: menu, tabs, editor, preview, sidebars, status bar
    Controls/
        SearchBar.xaml / .xaml.cs       # in-document search bar UserControl
    Windows/
        AboutWindow.xaml / .xaml.cs     # About dialog
        PreferencesWindow.xaml / .cs    # Preferences dialog (live theme switch)
    Services/
        MarkdownService.cs              # read file (UTF-8 + fallback) + shared Markdig pipeline
        FlowDocumentBuilder.cs          # walk AST → FlowDocument
        DocumentManager.cs              # DocumentTab model + tab/dirty/autosave management
        SettingsService.cs              # JSON settings in %APPDATA%
        ThemeService.cs                 # ResourceDictionary theme swap
        MarkdownLinter.cs               # lightweight regex linter
        GitService.cs                   # read-only git status via CLI
        ExportService.cs                # HTML export (Markdig ToHtml)
        PrintService.cs                 # print + PDF (Print to PDF)
        UpdateService.cs                # HTTP version check (GitHub releases)
        TelemetryService.cs             # local-only opt-in JSONL log
        PluginHost.cs                   # IMarkdownViewerPlugin + Assembly.LoadFrom
    Themes/
        Light.xaml                      # Light theme ResourceDictionary
        Dark.xaml                       # Dark theme ResourceDictionary
samples/sample.md                       # smoke-test document
SKILLS.MD                               # growing notes on solved issues
plans/architecture.md                   # design document
```

## Dependencies

- `Markdig` (NuGet, v0.37.0) — the only third-party dependency. Chosen by the
  prompt and because it produces a clean, walkable AST. No syntax-highlighting
  library.

## Requirements

- Windows 10 / 11 (x64)
- .NET 8 SDK to build (the published single-file exe is self-contained and
  needs no runtime install)
- Optional: `git` on `PATH` for the read-only Git status panel

## Build

```powershell
dotnet build src/MarkdownViewer/MarkdownViewer.csproj -c Release
```

## Run (from source)

```powershell
dotnet run --project src/MarkdownViewer/MarkdownViewer.csproj -c Release
```

Open a specific file:

```powershell
dotnet run --project src/MarkdownViewer/MarkdownViewer.csproj -c Release -- "samples\sample.md"
```

## Publish (recommended: self-contained, single-file)

```powershell
dotnet publish src/MarkdownViewer/MarkdownViewer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Output: `src/MarkdownViewer/bin/Release/net8.0-windows/win-x64/publish/MarkdownViewer.exe`

Then open a file:

```powershell
MarkdownViewer.exe "C:\Docs\README.md"
```

## Keyboard shortcuts

| Shortcut | Action |
| --- | --- |
| `Ctrl+N` | New tab |
| `Ctrl+O` | Open file |
| `Ctrl+S` | Save |
| `Ctrl+Shift+S` | Save As |
| `Ctrl+W` | Close tab |
| `Ctrl+P` | Print |
| `Ctrl+F` | Find in document |
| `Ctrl+B` | Toggle file browser sidebar |
| `Ctrl+E` | Toggle editor pane |

## Settings & privacy

All preferences, recent documents, favourites, telemetry, and plugin loads are
stored locally under `%APPDATA%\MarkdownViewer\`. **No data ever leaves the
machine.** Telemetry and update checks are both **off by default** and can be
enabled from the Preferences window.

## Windows file association (future)

The architecture already supports file association: the exe opens any `.md`
file passed as its first argument. Adding `.md`/`.markdown` registry entries
pointing at the published exe requires no code changes.

## Known limitations

- No syntax highlighting in code blocks (intentional — avoids a large dep).
- One process per opened file (single-instance deferred).
- No installer.
- Remote (HTTP/HTTPS) images load but are not cached.
- Task-list checkboxes render as glyphs, not interactive controls.
- "Cloud sync" is not network sync: settings/telemetry are local only by
  design (the original prompt forbids network telemetry).
- PDF export uses the OS "Microsoft Print to PDF" virtual printer; the dialog
  is shown so the user picks the output path.

## Quality gate

Builds clean (0 warnings) · starts · Open File · Close File · Save · Save As ·
New Tab · tab switching · live preview · Find · Print · HTML export ·
Preferences · theme switch · lint panel · Git status · file browser ·
command-line open · Markdown rendering · scrolling · relative images ·
malformed-Markdown safe · missing-image safe.