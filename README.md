<p align="center"><img src="macOS/Resources/AppIcon-1024.png" width="128" alt="ILIAS Sync"></p>

# ILIAS Sync

[Deutsch](README.de.md) · **English**

A small menu bar / system tray app for **macOS** and **Windows** that shows your courses from an
[ILIAS](https://github.com/ILIAS-eLearning/ILIAS) system and automatically downloads new or changed course files
into a folder of your choice.

> **Status:** early version (0.1). So far only tested against an ILIAS installation using the newer UI framework.
> Other versions and themes may need adjustments.
> Tested and working on **macOS** and **Windows**.
> **Note:** the user interface of both apps is in German.

**Keywords:** ILIAS sync, ILIAS downloader, ILIAS client for macOS and Windows, synchronize ILIAS courses,
ILIAS course files offline, ILIAS menu bar app, ILIAS tray app, e-learning, LMS, Swift, SwiftUI, C#, WPF.

## Repository layout

| Folder | Contents |
| --- | --- |
| [`macOS/`](macOS) | macOS app (Swift, SwiftUI) – menu bar icon |
| [`Windows/`](Windows) | Windows app (C#, .NET 8, WPF) – system tray icon |
| [`docs/`](docs) | Screenshots |

Both apps do the same thing and share the same behavior and safety rules.

## Download

Ready-to-run apps are available under **[Releases](https://github.com/dev-prophet-code/ILIASsyncMac/releases/latest)**:

| Platform | File | Notes |
| --- | --- | --- |
| macOS 13+ (Apple Silicon & Intel) | `ILIASSync-…-macOS.zip` | Unzip, drag `ILIASSync.app` to Applications. First launch: right-click → **Open** (not notarized). |
| Windows 10/11 (x64) | `ILIASSync-…-Windows-x64.zip` | Unzip and run `ILIASSync.exe` (no installation, .NET included). SmartScreen may warn because the file is unsigned: **More info → Run anyway**. |

## Screenshot

<p align="center"><img src="docs/screenshot-macos-einstellungen.png" width="360" alt="Settings window on macOS (demo data)"></p>

## Features

- Setup through a small window: server address, username, password, sync folder, interval
- Afterwards the app lives only in the menu bar (macOS) or system tray (Windows), no Dock icon / taskbar button
- Menu: **Meine Kurse** (my courses, opens the course in your browser), **Jetzt synchronisieren** (sync now),
  **Sync-Ordner öffnen** (open sync folder), **Einstellungen** (settings)
- Downloads files from courses and subfolders and mirrors the folder structure locally
- Individual courses can be excluded from syncing
- Optional: launch at login, notification when new files arrive
- The password is stored securely: macOS Keychain / Windows DPAPI (encrypted for your user account)

## Safety of your local data

- The app **never deletes** files in the sync folder and **never uploads** anything. It only makes read requests to
  ILIAS (apart from logging in).
- Existing files are compared by **SHA-256**. If the content is identical, nothing happens.
- If a file was re-uploaded in ILIAS and differs from your local copy, it is **not overwritten**; the new version is
  saved next to it as `Name (neue Version YYYY-MM-DD HH-mm).ext`.
- If a file is removed in ILIAS, your local copy stays.

## Usage

1. Start the app. On first launch the settings window opens.
2. Enter the ILIAS address (e.g. `https://ilias.uni-beispiel.de`), username and password, and choose a sync folder.
3. Click **Speichern & Verbinden** (save & connect). The app logs in, shows the number of courses and starts the first sync.
4. Close the window. The icon stays in the menu bar / tray; open the settings again from its menu
   (macOS: also by launching the app again; Windows: double-click the tray icon).

## Build it yourself

### macOS

Requires macOS 13+ and Xcode (license accepted; the SwiftUI macros need the Xcode toolchain).

```bash
git clone https://github.com/dev-prophet-code/ILIASsyncMac.git
cd ILIASsyncMac/macOS
./build.sh
open build/ILIASSync.app
```

`build.sh` builds a universal (arm64 + x86_64) release build, creates `build/ILIASSync.app` (with icon and
`LSUIElement`) and signs it ad hoc. If Xcode is not at `/Applications/Xcode.app`, set `DEVELOPER_DIR` accordingly.
The app is not notarized; distributing it without a Gatekeeper warning requires an Apple Developer ID.

### Windows

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download) on Windows 10/11.

```powershell
git clone https://github.com/dev-prophet-code/ILIASsyncMac.git
cd ILIASsyncMac\Windows
.\build.ps1
.\build\ILIASSync.exe
```

`build.ps1` publishes a single self-contained `ILIASSync.exe` (win-x64). For development, `dotnet run` in the
`Windows` folder works as well. The exe is unsigned, so SmartScreen shows a warning on other machines.

## How it works

Without admin setup, ILIAS offers no simple user-level API. The apps therefore log in like a browser through the
login form, read "My courses" (`ilmembershipoverviewgui`, falling back to the dashboard) and walk through
courses and folders via `goto.php?target=…`. Files are fetched via `goto.php?target=file_<id>_download`.
Change detection uses `Last-Modified`/`Content-Length` and the SHA-256 hash.

Local sync state: macOS `~/Library/Application Support/ILIASSync/state.json`,
Windows `%APPDATA%\ILIASSync\state.json`.

## Known limitations

- No single sign-on (Shibboleth, CAS, …), only username and password login
- Only files are downloaded, not learning modules, forums, links or group folders
- Parsing the HTML pages may differ on other ILIAS versions or themes
- Windows: very long paths (> 260 characters) may fail unless long paths are enabled in Windows

## License

[Apache License 2.0](LICENSE)
