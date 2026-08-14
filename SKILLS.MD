# SKILLS.MD — Growing knowledge base

This file records lessons learned while building Markdown Viewer, as required by
the developer prompt. Each solved issue is added here.

## 1. Markdig AST → WPF FlowDocument: name collisions

Both `System.Windows.Documents` and `Markdig.Syntax` define types named `Block`,
`Inline`, `Table`, and `TableCell`. Importing both namespaces at once causes
"ambiguous reference" compiler errors.

**Fix:** alias every Markdig type we touch at the top of the builder file, e.g.
`using MBlock = Markdig.Syntax.Block;`, and use the unqualified `Block`/`Inline`
for the WPF types. Do **not** add `using Markdig.Syntax;` / `using Markdig.Syntax.Inlines;`
directly when `System.Windows.Documents` is also imported.

## 2. Markdig `LinkReferenceDefinition` is a Block, not an Inline

`LinkReferenceDefinition` lives in `Markdig.Syntax`, not `Markdig.Syntax.Inlines`,
so it cannot be matched in a switch over inline nodes. Markdig resolves reference
links into `LinkInline` objects during parsing, so a separate inline case is
unnecessary. Handle it (or skip it) in the block switch only.

## 3. Markdig `StringLineGroup` is not indexable with `[]`

`CodeBlock.Lines` is a `StringLineGroup`. You cannot do `spans[i]`. The simplest
way to get the full code text is `c.Lines.ToString()`, then trim a single trailing
newline. Split on `\n` after normalizing `\r\n` to `\n`.

## 4. Markdig `Table` and `TableCell` need extension aliases

`Table`/`TableCell` come from `Markdig.Extensions.Tables`, which is enabled by
`UseAdvancedExtensions()`. Alias them as `MTable`/`MTableCell`. A `Table` row is
a `ContainerBlock`; cast `table[r]` to `ContainerBlock` to iterate cells, and
iterate each `TableCell` (also a `ContainerBlock`) for its child blocks.

## 5. WPF `FlowDocumentScrollViewer` has no `LineHeight` property

`LineHeight` belongs to `FlowDocument` (and `TextBlock`), not to the viewer.
Setting it on the viewer in XAML causes `MC3072`. Either omit it or set it on
the `FlowDocument` in code.

## 6. Empty `StartupUri=""` breaks App.xaml compilation

`<Application StartupUri="">` triggers `MC1000` ("Value cannot be null").
When starting the main window from `App.OnStartup` in code-behind, simply omit
the `StartupUri` attribute — do not set it to an empty string.

## 7. XAML static command binding

Custom `RoutedCommand` instances exposed as `public static` fields cannot be
bound with `{Binding OpenCommand}`. Use `{x:Static local:MainWindow.OpenCommand}`
and add the matching `CommandBinding` entries in the window's `CommandBindings`.

## 8. BitmapImage loads must be guarded

Loading an image via `BitmapImage` can throw for missing/unsupported files.
Always wrap in try/catch and return a placeholder `Run("[image not found: ...]")`
rather than letting the exception propagate — a single bad image must never crash
the viewer.

## 9. Self-contained single-file publish for WPF

WPF apps publish cleanly with:
`dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true`.
The result is a single `MarkdownViewer.exe` with no separate runtime install
requirement. (See `README.md` for the full command.)