# MdReader - v1.0.0

Desktop Markdown reader and WYSIWYG editor for Windows. Open a `.md` / `.markdown` file or a whole folder, read it rendered (tables, code highlighting, task lists, Mermaid diagrams), find your way with tabs, an outline, in-document search and zoom, and correct it without writing Markdown syntax. Saving rewrites **only the blocks you changed**: everything else stays byte-for-byte identical.

Interface in French.

## Features

- Open a file (`Ctrl+O`), a folder with all its subfolders (`Ctrl+Maj+O`), drag and drop, command-line argument, "Open with".
- Single instance: a second launch hands its file or folder to the running window.
- Explorer tree: Markdown files only, empty folders hidden, natural sort, filter, live refresh on disk changes, resizable (180 to 420).
- Tabs: modified dot, close button or middle click, `Ctrl+W`, `Ctrl+Tab` / `Ctrl+Maj+Tab`, edit history and scroll position kept per tab.
- Rendering: CommonMark + GitHub extensions (tables, task lists, strikethrough), headings 1 to 6, syntax highlighting, local images, internal links open a tab, web links open the default browser, anchors scroll.
- Mermaid diagrams rendered in both themes; invalid sources show an error without breaking the document.
- Outline (headings 1 to 3) following the scroll; search with highlighting and `n sur N` counter; zoom 70 to 200 %.
- WYSIWYG editing (`Ctrl+E`): formatting bar with 17 commands (undo/redo, paragraph styles, bold, italic, strikethrough, inline code, lists, task list, quote, link, image, table, code block, Mermaid diagram, rule), Mermaid source editor.
- Saving (`Ctrl+S`) keeps the original encoding (UTF-8, UTF-8 BOM, UTF-16) and line endings; confirmation when closing a modified tab or the window.
- Banner when an open file changes or disappears on disk (reload or keep your version).
- Light and dark themes, following Windows until a manual choice; narrow-window layout (overlay panes at 820 px and below).

## Stack

| Item | Value |
| --- | --- |
| Runtime | .NET 10 (LTS), `net10.0-windows10.0.19041.0` |
| Language | C# 14, nullable enabled, warnings as errors |
| UI | WPF + [WPF UI](https://github.com/lepoco/wpfui) (Fluent controls), MVVM with CommunityToolkit.Mvvm |
| Document | WebView2 (composition control) hosting one iframe per tab |
| Composition | `IHost` + dependency injection, Serilog file logging |
| Tests | xUnit v3 on Microsoft.Testing.Platform, NSubstitute |

### NuGet packages (versions in `Directory.Packages.props`)

| Package | Version | Role |
| --- | --- | --- |
| WPF-UI | 4.3.0 | Fluent controls and theme |
| CommunityToolkit.Mvvm | 8.4.2 | Observable properties and commands |
| Microsoft.Extensions.Hosting | 10.0.14 | Host and dependency injection |
| Serilog.Extensions.Hosting / Sinks.File / Sinks.Debug | 10.0.0 / 7.0.0 / 3.0.0 | Logging |
| Microsoft.Web.WebView2 | 1.0.4314.35 | Document page host |
| Markdig | 1.6.0 | Markdown parsing with source positions |
| HtmlSanitizer | 11.0.16 | Sanitizing raw HTML of documents |
| xunit.v3, xunit.runner.visualstudio, Microsoft.NET.Test.Sdk, NSubstitute | 4.0.2, 4.0.1, 18.11.0, 6.2.0 | Tests |

### Embedded JavaScript (`src/MdReader/Web/vendor`, no CDN)

mermaid 12.3.0, highlight.js 11.12.0 (`@highlightjs/cdn-assets` build), turndown 7.2.5, turndown-plugin-gfm 1.0.2.

## Prerequisites

- Windows 10 version 2004 or later, or Windows 11.
- Microsoft Edge WebView2 Runtime (Evergreen). It ships with Windows 11; on Windows 10 install it from Microsoft if missing.
- .NET 10 SDK, only to build from source.

## Build and run

From the repository root:

```
dotnet restore
dotnet build MdReader.sln -c Debug
dotnet run --project src/MdReader
dotnet run --project src/MdReader -- "C:\path\to\file.md"
dotnet format MdReader.sln --verify-no-changes
dotnet test --solution MdReader.sln
dotnet list package --vulnerable --include-transitive
```

`global.json` switches `dotnet test` to Microsoft.Testing.Platform, which xUnit v3 4.x requires on the .NET 10 SDK; hence `--solution`.

Debug logging: set the environment variable `MDREADER_DEBUG=1` before launching.

## Publish (portable executable)

Close the application first (a running instance locks its own executable), then:

```
dotnet publish src/MdReader -c Release -p:PublishProfile=win-x64
```

Output: `publish/win-x64/MdReader.exe`, a single self-contained file (about 100 MB, no .NET runtime to install). Copy it anywhere and run it.

- **SmartScreen**: the executable is not signed. On first launch of a downloaded copy, Windows SmartScreen shows a warning; choose "More info" then "Run anyway".
- **Open `.md` files with MdReader**: right-click a `.md` file, "Open with", "Choose another app", browse to `MdReader.exe`.

## Data

Everything lives under `%APPDATA%\MdReader`:

| Path | Content |
| --- | --- |
| `preferences.json` | Last folder, pane visibility and width, zoom, manual theme, window placement |
| `logs\app-*.log` | Daily log files, 1 MB each, 7 kept |
| `WebView2\` | WebView2 profile |

This folder is shared with the previous Markdown reader of the same name: MdReader replaces it and takes over its theme, zoom, explorer width and last folder.

## Project structure

```
md-reader/
├── MdReader.sln · Directory.Build.props · Directory.Packages.props · global.json · .editorconfig
├── docs/release/CHANGELOG.md
├── src/MdReader/
│   ├── Assets/                icon.ico (executable, window, taskbar), rendered from the title bar drawing
│   ├── App.xaml(.cs)          composition root: logging, global handlers, single instance, theme, window
│   ├── AppConfig.cs           shared constants
│   ├── Models/                records, enums, Result<T>, page protocol, preferences
│   ├── Services/              files, rendering, recomposition, folder scan, watchers, theme, dialogs, notifications
│   ├── ViewModels/            Shell, Explorer, Document (one per tab), FileNode, LinkDialog, IDocumentSurface
│   ├── Views/                 MainWindow, Layout/ (tabs, bars), Explorer, Outline, Document, EmptyState, Controls/DocumentHost, Dialogs/
│   ├── Converters/ · Helpers/ · Resources/Strings.resx
│   ├── Themes/                Tokens(.Light/.Dark).xaml, Icons.xaml, Styles.xaml
│   ├── Web/                   host.html, doc.html, doc.css, page.js, editor.js, vendor/
│   └── Properties/PublishProfiles/win-x64.pubxml
├── tests/MdReader.Tests/      Services/, ViewModels/, Helpers/, Converters/
└── tools/stop-format.ps1      Claude Code Stop hook guard
```

## Services and commands

| Command | Service | Notes |
| --- | --- | --- |
| Open file / folder / paths | `ITextFileService`, `IMarkdownRenderService`, `IFolderScanService`, `IFileWatchService` | Size warning above 5 MB; binary and denied files reported |
| Save | `IDocumentSurface.SerializeAsync`, `IMarkdownComposeService`, `ITextFileService.WriteAsync` | Untouched blocks copied from the source, atomic write |
| Close tab / window | `IDialogService.ConfirmSaveAsync` | Save, Don't save, Cancel |
| Mode, zoom, panes, theme | `IPreferencesService`, `IAppThemeService` | Persisted settings |
| Format (17 commands), link, image | `IDocumentSurface.ApplyFormat`, `IDialogService` | Link dialog validates the address |
| Search, outline navigation | `IDocumentSurface` | Literal, case-insensitive search |
| Web link | `IExternalLauncher` | Absolute `http` / `https` only |
| Page resources | `IPageAssetProvider` | Embedded page files and document images on `https://mdreader.local/` |

## How saving keeps your file intact

Markdig parses the document with source positions; each top-level block is rendered inside a wrapper carrying its source index. In the page, a `MutationObserver` marks the blocks you touch. On save, untouched blocks are sent back as their index and copied verbatim, with their original separators; only modified or new blocks are converted to Markdown (Turndown) and written with the file's line ending.

## Security

- Raw HTML in documents is sanitized (HtmlSanitizer) and the page runs under a strict Content-Security-Policy: no network access, no inline script, only the virtual host.
- Navigation outside the virtual host is cancelled; web links go to the default browser after validation.
- WebView2 developer tools are disabled in Release builds; no host object is exposed to the page.

## Conventions

- Strict MVVM: views bind, ViewModels hold state and commands, services do the work behind interfaces.
- Zero visual literal in XAML or C#: every color, size and spacing is a resource of `Themes/Tokens*.xaml`.
- Expected failures are returned as `Result<T>` and shown as notifications; unexpected ones reach the three global handlers and the log.
- `.claude/settings.json` adds a Stop hook running `dotnet format --verify-no-changes` when C# or XAML files changed (`tools/stop-format.ps1`); edit or remove it there.

## Changelog

See [docs/release/CHANGELOG.md](docs/release/CHANGELOG.md).
