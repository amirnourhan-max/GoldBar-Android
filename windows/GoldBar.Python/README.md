# Gold Bar Python + CSS Desktop v1.6.0

Windows desktop client using PySide6/QWebEngine for a local HTML/CSS interface and Python for calculations, persistence, reports, and RS-232 scale communication.

- Fixed black/gold dashboard matching the approved reference.
- Quick registration card is HTML/CSS, not resizable.
- Python serial reader runs off the UI thread.
- Auto Read defaults to off; stable filtering is available when enabled.
- Reuses `%LOCALAPPDATA%\GoldBar\entries.json` and `settings.json` for migration from the C# build.
- Inno Setup produces the normal `GoldBar-Setup-v1.6.0.exe` installer.
