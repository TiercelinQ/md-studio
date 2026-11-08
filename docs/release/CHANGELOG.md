# Changelog

All notable changes to this project are documented in this file.
The format is based on Keep a Changelog, and this project adheres to Semantic Versioning.

## [Unreleased]
### Changed
- The application is renamed from MdReader to MdStudio: executable `MdStudio.exe`, data folder `%APPDATA%\MdStudio`, debug variable `MDSTUDIO_DEBUG`. On first launch, the preferences of `%APPDATA%\MdReader` are copied over (that folder is left untouched); logs and the WebView2 profile start fresh. Shortcuts and "Open with" associations must point to `MdStudio.exe`.

## [1.1.0] - 2026-11-08
### Added
- Source mode (`Ctrl+U`, "Source" button next to "Édition"): the raw Markdown text, readable and editable as plain text, with search; leaving the mode renders the typed text.
- "Créer un fichier" button (`Ctrl+N`), left of "Ouvrir un fichier": creates an empty Markdown file through a "Save as" dialog and opens it in editing mode; an existing file is opened, never emptied.

### Changed
- A loading screen (ring and text, both themes) replaces the blank page while a document renders.
- The tab and its loading screen appear as soon as a file is opened, before it is read: a large file no longer leaves the window unchanged for seconds. A file that cannot be read closes its tab.
- The document column uses the full width left by the panes and the window instead of a centered 760 px column.

### Fixed
- Opening a document no longer waits 2 seconds: WebView2 SmartScreen reputation checking, which looked up every page of the internal virtual host online, is turned off.
- In editing mode, a click anywhere below a short or empty document now places the caret in it.
- Screen readers announce the "Lecture", "Édition" and "Source" mode buttons by name.

## [1.0.0] - 2026-11-07
### Added
- Initial release.
- Application icon for the executable, the window and the taskbar, identical to the title bar icon.

### Fixed
- Command bar buttons are vertically centered in the bar instead of sticking to its top edge.
