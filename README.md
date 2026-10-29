<p align="center"><img src="Resources/AppIcon-1024.png" width="128" alt="ILIAS Sync"></p>

# ILIAS Sync

Kleine macOS-Menüleisten-App, die deine Kurse aus einem [ILIAS](https://github.com/ILIAS-eLearning/ILIAS)-System
anzeigt und neue oder geänderte Kursdateien automatisch in einen Ordner deiner Wahl herunterlädt.

> **Status:** frühe Version (0.1). Bisher nur gegen eine ILIAS-Installation mit dem neueren UI-Framework getestet
> Andere Versionen und Themes können Anpassungen brauchen.

## Funktionen

- Einrichtung über ein kleines Fenster: Server-Adresse, Benutzername, Passwort, Sync-Ordner, Intervall
- Danach läuft die App nur noch in der Menüleiste (kein Dock-Symbol)
- Menü: **Meine Kurse** (öffnet den Kurs im Browser), **Jetzt synchronisieren**, **Sync-Ordner öffnen**, **Einstellungen**
- Lädt Dateien aus Kursen und Unterordnern und bildet die Ordnerstruktur lokal nach
- Einzelne Kurse lassen sich vom Sync ausnehmen
- Optional: Start beim Anmelden, Benachrichtigung bei neuen Dateien
- Passwort liegt im macOS-Schlüsselbund

## Sicherheit der lokalen Daten

- Die App **löscht nie** Dateien im Sync-Ordner und **lädt nie etwas hoch**. Sie stellt nur lesende Anfragen an ILIAS
  (außer dem Login).
- Vorhandene Dateien werden per **SHA-256** verglichen. Ist der Inhalt identisch, passiert nichts.
- Ist eine Datei in ILIAS neu hochgeladen worden und weicht von der lokalen ab, wird sie **nicht überschrieben**,
  sondern als `Name (neue Version JJJJ-MM-TT HH-mm).ext` daneben gespeichert.
- Wurde eine Datei in ILIAS gelöscht, bleibt die lokale Kopie erhalten.

## Voraussetzungen

- macOS 13 oder neuer
- Zum Bauen: Xcode (inkl. akzeptierter Lizenz) – die SwiftUI-Makros brauchen die Xcode-Toolchain

## Bauen

```bash
git clone https://github.com/dev-prophet-code/ILIASsynchMac.git
cd ILIASsynchMac
./build.sh
open build/ILIASSync.app
```

`build.sh` baut das Swift-Package im Release-Modus, erzeugt `build/ILIASSync.app` (mit Icon, `LSUIElement`)
und signiert es ad hoc. Liegt Xcode nicht unter `/Applications/Xcode.app`, setze `DEVELOPER_DIR` entsprechend.

### Weitergeben

Die App ist nicht notarisiert. Auf anderen Macs blockiert Gatekeeper sie beim ersten Start:
Rechtsklick auf die App → **Öffnen** (oder Systemeinstellungen → Datenschutz & Sicherheit → **Trotzdem öffnen**).
Für eine Verteilung ohne Warnung braucht es eine Apple-Developer-ID und Notarisierung.

## Benutzung

1. App starten – beim ersten Mal öffnet sich das Einstellungsfenster.
2. ILIAS-Adresse (z. B. `https://ilias.uni-beispiel.de`), Benutzername und Passwort eintragen, Sync-Ordner wählen.
3. **Speichern & Verbinden**. Die App meldet sich an, zeigt die Zahl der Kurse und startet den ersten Sync.
4. Fenster schließen. Das Symbol in der Menüleiste bleibt; Einstellungen erreichst du über das Menü.

## Wie es funktioniert

ILIAS bietet ohne Admin-Setup keine einfache Schnittstelle für Nutzer. Die App meldet sich deshalb wie ein Browser
über das Login-Formular an, liest „Meine Kurse" (`ilmembershipoverviewgui`, Fallback Dashboard) aus und durchläuft
Kurse und Ordner über `goto.php?target=…`. Dateien werden über `goto.php?target=file_<id>_download` geladen.
Die Änderungserkennung nutzt `Last-Modified`/`Content-Length` und den SHA-256-Hash.

Der lokale Sync-Status liegt in `~/Library/Application Support/ILIASSync/state.json`.

## Bekannte Einschränkungen

- Kein Single-Sign-on (Shibboleth, CAS, …), nur Anmeldung mit Benutzername und Passwort
- Es werden nur Dateien geladen, keine Lernmodule, Foren, Links oder Gruppenordner
- Das Auslesen der HTML-Seiten kann bei anderen ILIAS-Versionen/Themes abweichen
- Nur Apple-Silicon-Build im Standard (`swift build` auf einem Intel-Mac baut für Intel)

## Lizenz

[Apache License 2.0](LICENSE)
