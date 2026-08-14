# Project: Ultra-fast Windows Markdown File Viewer

We are going to build a very small, extremely fast Windows desktop application for viewing Markdown files.

The primary objective is:

> Double-click or open a Markdown file and display it almost immediately.

This is a viewer, not a Markdown editor.

## Technology

Use:

- C#
- .NET
- WPF
- Markdig for Markdown parsing where appropriate

Target Windows only.

Do NOT use:

- Electron
- Node.js
- React
- Vue
- Angular
- Tauri unless there is a very strong technical reason
- Web servers
- Databases
- Docker
- unnecessary dependency injection frameworks
- unnecessary MVVM frameworks
- telemetry
- cloud services

Keep the application as lightweight and native as reasonably possible.

Do crate a SKILLS.MD file and everytime you solve an issue, add this to your skillset

## First task

Before implementation, briefly inspect the requirements and propose the smallest appropriate architecture.

Pay particular attention to application startup time and Markdown rendering performance.

Do not overengineer the solution.

After the short architecture assessment, implement the application.

# Functional requirements

## Main window

Create one primary application window.

The window displays the currently opened Markdown document.

Initially, when no file is open, show a simple neutral empty state.

Example:

`Open a Markdown file to view it.`

Do not create a dashboard, welcome screen, recent-files screen or other unnecessary UI.

## Menu

The menu must contain ONLY the following user actions:

### File

- Open File
- Close File

### Help

- About

Do not add other menu functions.

Keyboard shortcuts:

- Ctrl+O = Open File
- Ctrl+W = Close File

## Open File

"Open File" opens a normal Windows file picker.

Supported extensions:

- `.md`
- `.markdown`

After selecting a file:

1. Read the file.
2. Parse the Markdown.
3. Render it in the main window.
4. Update the window title.

Example window title:

`README.md — Markdown Viewer`

The application must correctly handle UTF-8 Markdown files.

## Close File

"Close File" closes the currently displayed document.

It must NOT close the application itself.

Return to the empty state.

## About

Display a very small About dialog containing:

- application name
- version
- short description

Example:

Markdown Viewer  
Version 0.1.0

Fast lightweight Markdown viewer for Windows.

No additional settings or configuration are required.

# Markdown support

At minimum support:

- headings H1-H6
- paragraphs
- bold
- italic
- strikethrough
- ordered lists
- unordered lists
- nested lists
- blockquotes
- inline code
- fenced code blocks
- horizontal rules
- hyperlinks
- tables
- images
- task lists

Use GitHub-flavoured Markdown behaviour where practical.

Relative image references should resolve relative to the directory containing the opened Markdown file.

For example:

```markdown
![Architecture](images/architecture.png)
```

When opening:

```text
C:\Projects\Test\README.md
```

the image should resolve from:

```text
C:\Projects\Test\images\architecture.png
```

# Rendering architecture

Performance is important.

Do NOT automatically solve Markdown rendering by embedding a complete browser engine.

First investigate whether Markdig output can be efficiently mapped/rendered into native WPF controls or a FlowDocument.

Preferred direction:

```text
Markdown file
    ↓
File reader
    ↓
Markdig parser
    ↓
native WPF representation
    ↓
viewer
```

If direct native rendering creates excessive complexity, document the trade-off before choosing another approach.

Avoid WebView2 unless it provides a significant implementation advantage that outweighs startup/runtime overhead.

# UI design

The application should have a clean Windows desktop appearance.

Priorities:

1. document readability
2. startup speed
3. simplicity
4. low memory consumption

The document area should:

- occupy essentially the complete window
- scroll vertically
- have reasonable horizontal padding
- use readable typography
- clearly distinguish headings
- clearly distinguish code blocks
- render tables cleanly
- scale correctly when the window is resized

No sidebar.

No toolbar.

No status bar unless technically necessary.

No tabs.

No editor pane.

No preview/editor split.

No file explorer.

# Code blocks

Code blocks must visually differ from normal text.

Preserve:

- whitespace
- line breaks
- indentation

Syntax highlighting is NOT required for the first version.

Do not introduce a large dependency solely for syntax highlighting.

# Links

Links should be visually recognizable.

Clicking an HTTP or HTTPS hyperlink should open it using the user's default browser.

Do not implement an internal browser.

# Images

Support:

- relative local images
- absolute local images where Windows permits access
- HTTP/HTTPS images only if implementation remains simple and secure

Local image handling is more important than remote image support.

Image loading failures must not crash the application.

# Error handling

The application must gracefully handle:

- missing files
- inaccessible files
- invalid paths
- unsupported encoding where possible
- malformed Markdown
- missing images

Do not show stack traces to normal users.

Provide a short Windows-style error dialog when necessary.

# Command-line file opening

The application must support:

```powershell
MarkdownViewer.exe README.md
```

If a valid Markdown filepath is passed as the first command-line argument, open that document immediately.

This is important for later Windows `.md` file association.

Do not show an intermediate welcome screen first.

# Single-instance behaviour

Do NOT spend significant time on single-instance handling in the first implementation.

A separate process per opened file is acceptable for version 0.1.

We can optimize this later if necessary.

# Performance

Startup performance is one of the most important acceptance criteria.

Avoid expensive initialization during startup.

Do not initialize components before they are needed.

Avoid reflection-heavy frameworks and unnecessary runtime services.

Measure/debug obvious startup bottlenecks if they occur.

The app should feel effectively instantaneous when opening ordinary Markdown files.

Files containing several thousand lines should remain responsive.

Do not unnecessarily re-parse the complete document when the window resizes.

# Application structure

Keep the source structure small.

Something approximately like:

```text
src/MarkdownViewer/
    App.xaml
    App.xaml.cs
    MainWindow.xaml
    MainWindow.xaml.cs
    Services/
        MarkdownService.cs
```

Additional classes are allowed when they have a clear responsibility.

Do not create architectural layers merely for architectural purity.

In particular, avoid structures such as:

```text
Domain/
Application/
Infrastructure/
Repositories/
CQRS/
Commands/
Queries/
Mediators/
```

They are unnecessary for this application.

# File associations

Prepare the application architecture so Windows file association can be added later.

Do not make registry manipulation a core requirement of the first implementation unless it is trivial.

The important requirement now is that this works:

```powershell
MarkdownViewer.exe "C:\Docs\README.md"
```

# Publishing

The project must be publishable as a normal Windows executable.

Prefer a release configuration suitable for:

```powershell
dotnet publish
```

Investigate whether the application can reasonably be published as:

- self-contained
- win-x64
- single-file

without compromising startup performance.

Document the recommended publish command.

Do not introduce an installer yet.

# Quality requirements

Before considering the task complete:

- solution builds without warnings/errors relevant to our code
- application starts correctly
- Open File works
- Close File works
- About works
- Ctrl+O works
- Ctrl+W works
- command-line file opening works
- Markdown rendering works
- scrolling works
- local relative images work
- malformed Markdown does not crash the application
- missing images do not crash the application

# Scope control

Version 0.1 is intentionally minimal.

Do NOT implement:

- Markdown editing
- save
- save as
- tabs
- recent documents
- favourites
- search
- preferences
- themes
- plugins
- spell checking
- Markdown linting
- Git integration
- file browser
- autosave
- cloud sync
- printing
- PDF export
- HTML export
- update mechanism
- telemetry

Unless something is technically required for the stated viewer functionality, leave it out.

# Development approach

Work incrementally.

First establish:

1. minimal WPF application
2. window and menu
3. file loading
4. Markdown parsing
5. Markdown rendering
6. image/path handling
7. command-line opening
8. error handling
9. release publishing
10. final performance/code review

At completion, provide:

- concise summary of the implementation
- files created/changed
- important architectural decisions
- dependencies used and why
- build command
- run command
- publish command
- known limitations

Do not expand the project scope beyond these requirements.