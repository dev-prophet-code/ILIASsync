<p align="center"><img src="Resources/AppIcon-1024.png" width="128" alt="ILIAS Sync"></p>

# ILIAS Sync

[Deutsch](README.de.md) · **English**

A small macOS menu bar app that shows your courses from an [ILIAS](https://github.com/ILIAS-eLearning/ILIAS)
system and automatically downloads new or changed course files into a folder of your choice.

> **Status:** early version (0.1). So far only tested against an ILIAS installation using the newer UI framework.
> Other versions and themes may need adjustments.
> **Note:** the app's user interface is in German.

**Keywords:** ILIAS sync, ILIAS downloader, ILIAS client for macOS, synchronize ILIAS courses, ILIAS course files offline,
ILIAS menu bar app, e-learning, LMS, Swift, SwiftUI.

## Screenshot

<p align="center"><img src="docs/screenshot-einstellungen.png" width="360" alt="Settings window (demo data)"></p>

## Features

- Setup through a small window: server address, username, password, sync folder, interval
- Afterwards the app lives only in the menu bar (no Dock icon)
- Menu: **Meine Kurse** (my courses, opens the course in your browser), **Jetzt synchronisieren** (sync now),
  **Sync-Ordner öffnen** (open sync folder), **Einstellungen** (settings)
- Downloads files from courses and subfolders and mirrors the folder structure locally
- Individual courses can be excluded from syncing
- Optional: launch at login, notification when new files arrive
- The password is stored in the macOS Keychain

## Safety of your local data

- The app **never deletes** files in the sync folder and **never uploads** anything. It only makes read requests to
  ILIAS (apart from logging in).
- Existing files are compared by **SHA-256**. If the content is identical, nothing happens.
- If a file was re-uploaded in ILIAS and differs from your local copy, it is **not overwritten**; the new version is
  saved next to it as `Name (neue Version YYYY-MM-DD HH-mm).ext`.
- If a file is removed in ILIAS, your local copy stays.

## Download

A ready-to-run app (universal: Apple Silicon and Intel) is available under **[Releases](https://github.com/dev-prophet-code/ILIASsyncMac/releases/latest)**:
download `ILIASSync-…-macOS.zip`, unzip it and drag `ILIASSync.app` to Applications.
On first launch: right-click → **Open** (the app is not notarized, see below).

## Requirements

- macOS 13 or newer
- To build it yourself: Xcode (with the license accepted). The SwiftUI macros need the Xcode toolchain.

## Build

```bash
git clone https://github.com/dev-prophet-code/ILIASsyncMac.git
cd ILIASsyncMac
./build.sh
open build/ILIASSync.app
```

`build.sh` builds the Swift package in release mode, creates `build/ILIASSync.app` (with icon and `LSUIElement`) and
signs it ad hoc. The build is universal (arm64 + x86_64). If Xcode is not at `/Applications/Xcode.app`, set `DEVELOPER_DIR` accordingly.

### Distribution

The app is not notarized. On other Macs Gatekeeper blocks it on first launch: right-click the app → **Open**
(or System Settings → Privacy & Security → **Open Anyway**). Distributing without a warning requires an Apple
Developer ID and notarization.

## Usage

1. Start the app. On first launch the settings window opens.
2. Enter the ILIAS address (e.g. `https://ilias.uni-beispiel.de`), username and password, and choose a sync folder.
3. Click **Speichern & Verbinden** (save & connect). The app logs in, shows the number of courses and starts the first sync.
4. Close the window. The menu bar icon stays; open the settings again from its menu.

## How it works

Without admin setup, ILIAS offers no simple user-level API. The app therefore logs in like a browser through the
login form, reads "My courses" (`ilmembershipoverviewgui`, falling back to the dashboard) and walks through
courses and folders via `goto.php?target=…`. Files are fetched via `goto.php?target=file_<id>_download`.
Change detection uses `Last-Modified`/`Content-Length` and the SHA-256 hash.

The local sync state is kept in `~/Library/Application Support/ILIASSync/state.json`.

## Known limitations

- No single sign-on (Shibboleth, CAS, …), only username and password login
- Only files are downloaded, not learning modules, forums, links or group folders
- Parsing the HTML pages may differ on other ILIAS versions or themes

## License

[Apache License 2.0](LICENSE)
