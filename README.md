# Markdown Viewer

A very small, extremely fast Windows desktop application for viewing Markdown
files. Built with C# / .NET 8 / WPF. A viewer only — no editing, no tabs, no
extras.

## Features (v0.1)

- Double-click / command-line open of `.md` and `.markdown` files
- GitHub-flavoured Markdown rendering (headings, lists, tables, blockquotes,
  code blocks, links, images, task lists, strikethrough)
- Native WPF `FlowDocument` rendering — no browser engine, reflows on resize
  without re-parsing
- Relative images resolve against the opened file's directory
- HTTP/HTTPS links open in the user's default browser
- Clean window: menu bar only (File → Open/Close, Help → About)
- Shortcuts: `Ctrl+O` (Open), `Ctrl+W` (Close File)
- Graceful handling of missing files, malformed Markdown, and missing images

## Architecture

See [`plans/architecture.md`](plans/architecture.md) for the full design and
the rationale for the native `FlowDocument` approach over WebView2.

Pipeline:

```
Markdown file → UTF-8 reader → Markdig parser (AST) → FlowDocumentBuilder → FlowDocument → viewer
```

## Project structure

```
src/MarkdownViewer/
    App.xaml / App.xaml.cs          # startup + command-line file open
    MainWindow.xaml / .xaml.cs      # window, menu, shortcuts, empty state
    Services/
        MarkdownService.cs          # read file (UTF-8 + fallback) + parse
        FlowDocumentBuilder.cs      # walk AST → FlowDocument
samples/sample.md                   # smoke-test document
SKILLS.MD                           # growing notes on solved issues
plans/architecture.md               # design document
```

## Dependencies

- `Markdig` (NuGet, v0.37.0) — the only third-party dependency. Chosen by the
  prompt and because it produces a clean, walkable AST. No syntax-highlighting
  library (out of scope for v0.1).

## Requirements

- Windows 10 / 11 (x64)
- .NET 8 SDK to build (the published single-file exe is self-contained and
  needs no runtime install)

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

## Windows file association (future)

The architecture already supports file association: the exe opens any `.md`
file passed as its first argument. Adding `.md`/`.markdown` registry entries
pointing at the published exe requires no code changes — intentionally not
implemented in v0.1.

## Known limitations (v0.1)

- No syntax highlighting in code blocks (intentional — avoids a large dep).
- One process per opened file (single-instance deferred).
- No installer.
- Remote (HTTP/HTTPS) images load but are not cached.
- Task-list checkboxes render as glyphs, not interactive controls.

## Quality gate

Builds clean (0 warnings) · starts · Open File · Close File · About ·
`Ctrl+O` · `Ctrl+W` · command-line open · Markdown rendering · scrolling ·
relative images · malformed-Markdown safe · missing-image safe.