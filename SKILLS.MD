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

## 10. WPF SDK auto-includes XAML pages — do not double-list them

The `Microsoft.NET.Sdk.WindowsDesktop` SDK auto-globbs every `*.xaml` file
under the project root as a `Page` item. Adding explicit `<Page
Include="Themes\Light.xaml" />` entries triggers `NETSDK1022` ("duplicate
'Page' item"). Remove the manual entries and let the SDK do it.

## 11. XAML attributes: unescaped `&` is a hard parse error

A literal `&` in a XAML attribute (e.g. `Header="Privacy & Updates"`) causes
`MC3000`. The XML parser sees an unterminated entity. Either write `&`
or, simpler, rephrase to the word "and". The apply_diff tool normalises
`&` to `&` in both search and replace blocks, which makes a
search/replace that touches an `&` look like a no-op ("29% similar"). When
this happens, use `write_to_file` for a full rewrite instead of `apply_diff`.

## 12. `ExecutedRoutedEventArgs` has no public constructor

You cannot synthesise a `RoutedCommand` execution from code-behind by
constructing `new ExecutedRoutedEventArgs(...)`. The type has no public
two-arg constructor. When a non-command event handler (e.g. a Button
`Click`) needs to do the same work as a `CommandBinding` handler, extract
the body into a plain helper that takes a domain object (e.g.
`DoCloseTab(DocumentTab doc)`) and call it from both handlers.

## 13. XAML field name collides with a type name

Giving a control `x:Name="SearchBar"` inside a window that also has
`using MarkdownViewer.Controls;` creates a generated field named `SearchBar`
that shadows the type `SearchBar`. References to `SearchBar.Open()` then
fail to resolve as the compiler reads them as the field (which is
uninitialised in a static context) or the type ambiguously. Rename the
field — e.g. `x:Name="SearchBarCtrl"` — and reference `SearchBarCtrl`
everywhere in code-behind.

## 14. Markdig `MarkdownDocument` collides with a domain class in the same namespace

A class named `MarkdownDocument` in the project namespace makes
`using MDoc = Markdig.Syntax.MarkdownDocument;` aliases brittle: a
`using` alias placed inside a namespace body is also illegal
("using clause must precede all other elements"). Cleanest fix is to
rename the domain class (e.g. to `DocumentTab`) so no alias is needed.

## 15. FlowDocument must be cloned before printing/PDF export

A `FlowDocument` displayed in the UI is parented to a viewer. Calling
`PrintDocument` on it throws "element already has a parent". Clone via
a XAML round-trip: `string x = XamlWriter.Save(doc); var clone =
(FlowDocument)XamlReader.Parse(x);`. Images and data-bound content do not
survive this clone, but plain Markdown content does.