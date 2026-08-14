# Architecture — Ultra-fast Windows Markdown Viewer

## Goal

A minimal, native WPF desktop application that opens a Markdown file and displays
it almost instantly. A viewer only — no editing, no tabs, no extras.

## Decisions (confirmed with user)

- **Language / framework:** C# / .NET 8 (latest LTS) / WPF
- **Markdown parser:** Markdig, using the advanced (GitHub-flavoured) pipeline — `new MarkdownPipelineBuilder().UseAdvancedExtensions().Build()`
- **Rendering:** Native WPF `FlowDocument`, built by walking the Markdig AST (`MarkdownDocument`). No browser engine, no WebView2.
- **Host:** `FlowDocumentScrollViewer` (built-in scrolling, reflow on resize, no re-parse).
- **Shell:** Windows-only. Single-instance intentionally deferred (v0.1 = one process per file).

## Pipeline

```text
Markdown file (.md / .markdown)
    ↓  File.ReadAllText (UTF-8, with fallback)
Markdig parser  (Markdown.ToDocument(text, pipeline))
    ↓  MarkdownDocument AST
FlowDocumentBuilder  (walks AST → Block/Inline WPF objects)
    ↓
FlowDocument
    ↓
FlowDocumentScrollViewer  (MainWindow.xaml)
```

This matches the prompt's preferred direction exactly. Reflow on resize is free
because the parsed AST is reused; only WPF layout re-runs.

## Why FlowDocument over alternatives

| Option | Startup | Complexity | Re-parse on resize | Verdict |
|---|---|---|---|---|
| Markdig AST → FlowDocument | minimal | moderate (AST walker) | no | **chosen** |
| Markdig → HTML → WebView2 | heavier (browser host) | lower per-node | no | rejected by prompt |
| Custom canvas drawing | minimal | very high | manual | overkill |

## Project structure

```text
src/MarkdownViewer/
    MarkdownViewer.csproj
    App.xaml
    App.xaml.cs
    MainWindow.xaml
    MainWindow.xaml.cs
    Services/
        MarkdownService.cs      # read file + parse to MarkdownDocument
        FlowDocumentBuilder.cs  # walk AST -> FlowDocument
    SKILLS.MD                   # growing notes on solved issues
```

Plus top-level:
```text
plans/architecture.md   (this file)
```

No Domain/Application/Infrastructure/CQRS layers — explicitly forbidden by prompt.

## Dependencies

- `Markdig` (NuGet) — the only third-party dependency. Chosen because the prompt
  names it and it produces a clean, walkable AST. No syntax-highlighting library
  (v0.1 does not require it; prompt forbids large deps just for highlighting).

## UI

- Single `Window`. No toolbar, sidebar, status bar, tabs, editor pane.
- Menu bar with two top-level menus:
  - **File** → Open File, Close File
  - **Help** → About
- Content area: `FlowDocumentScrollViewer` filling the window, with horizontal
  padding; vertical scroll.
- Empty state: centered text `Open a Markdown file to view it.`
- Window title: `<filename>.md — Markdown Viewer` when a file is open;
  `Markdown Viewer` when empty.

## Shortcuts

- `Ctrl+O` → Open File
- `Ctrl+W` → Close File

## Command-line opening

`App.xaml.cs` inspects `args[0]`. If it points to an existing `.md`/`.markdown`
file, the document is loaded on startup before the window is shown — no welcome
screen. Otherwise the window opens in the empty state.

## Markdown feature mapping (AST → FlowDocument)

| Markdown | Markdig node | FlowDocument element |
|---|---|---|
| H1–H6 | `HeadingBlock` | `Paragraph` with sized `Run` (FontSize per level) |
| paragraph | `ParagraphBlock` | `Paragraph` |
| bold/italic/strike | `EmphasisInline` | `Run` with FontWeight/Italic/TextDecorations |
| inline code | `CodeInline` | `Run` monospace + light background via `Span` |
| fenced code | `FencedCodeBlock` | monospace `Paragraph`, preserve whitespace, border `Border` |
| ordered/unordered list | `ListBlock` + `ListItemBlock` | `List` (MarkerStyle) |
| nested lists | recursive `ListBlock` | nested `List` |
| blockquote | `QuoteBlock` | `Section` with left border + padding |
| horizontal rule | `ThematicBreakBlock` | `Paragraph` with bottom border |
| links | `LinkInline` | `Hyperlink` (open via `Process.Start`) |
| tables | `Table` | WPF `Table` (TableGrid) |
| images | `LinkInline` with `IsImage` | `InlineUIContainer` with `Image`; resolve relative to file dir |
| task lists | `TaskList` extensions | checkbox glyph + text |

## Images

- Relative paths resolved against the directory of the currently open file.
- Absolute local paths used directly.
- HTTP/HTTPS images supported only if simple — loaded into a `BitmapImage` with
  a failure path that shows a placeholder and never crashes.
- All image loads are wrapped so a missing/unreadable image shows a small
  `TextBlock` placeholder ("[image not found]") instead of throwing.

## Error handling

- Missing/inaccessible file → `MessageBox` with short message, stay in current state.
- Malformed Markdown → Markdig is fault-tolerant; never throws on input.
- Unsupported encoding → read as UTF-8 with detection fallback; on hard failure,
  show a short `MessageBox`.
- Missing image → placeholder, no crash.
- No stack traces shown to users.

## Publishing

Recommended (per prompt investigation):

```powershell
dotnet publish src/MarkdownViewer/MarkdownViewer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Self-contained + single-file gives a no-dependency `.exe`. ReadyToRun is left off
to avoid bloating startup via disk read; the app is small enough that JIT is fast.
(If startup becomes a concern later, `<PublishReadyToRun>true</PublishReadyToRun>`
can be evaluated.)

No installer for v0.1.

## File association (future)

The architecture is shaped so file association can be added later by writing
registry entries for `.md`/`.markdown` pointing at the published exe — no code
changes required, since `args[0]` already drives startup. Not implemented in v0.1.

## Quality gate (from prompt)

Builds clean · starts · Open File · Close File · About · Ctrl+O · Ctrl+W ·
command-line open · rendering · scrolling · relative images · malformed Markdown
safe · missing images safe.

## Out of scope (v0.1)

Editing, save, tabs, recents, search, themes, plugins, spell check, linting, git,
file browser, autosave, sync, printing, PDF/HTML export, updates, telemetry,
single-instance, installer, syntax highlighting.