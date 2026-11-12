# Herkunft und eingebundene Fremdprojekte

## Ausgangspunkt: NTSCRT von Finn McKenty

**Tape & Tube ist eine Windows-Adaption von [NTSCRT](https://github.com/finnmckenty/NTSCRT).** Das ursprüngliche macOS-Projekt liefert das Anwendungskonzept, den Aufbau der Signalkette und den grundlegenden Workflow. Diese Herkunft ist ein wesentlicher Bestandteil der Projektbeschreibung. Die Windows-Oberfläche und Host-Anbindung sind neu implementiert; der Swift-/Metal-Anwendungscode wird nicht mitgeliefert. Der aktuelle Funktionsumfang ist eine Teilmenge des Originals.

## Effektprozessoren und weitere Komponenten

**Tape & Tube** liefert eigene Oberfläche, Ablaufsteuerung, Presets, Ausgabegeometrie und die Host-Anbindung. Die NTSC-/VHS-Simulation ist ntsc-rs; CRT-Effekte sind Arbeit der jeweiligen Shader-Autoren; librashader führt diese Shader aus; FFmpeg übernimmt Medienverarbeitung. Unsere GPL-3.0-only-Lizenz ersetzt keine dieser Fremdlizenzen.

| Bestandteil | Quelle | Lizenzangabe des Projekts |
|---|---|---|
| ntsc-rs | https://github.com/ntsc-rs/ntsc-rs | MIT OR ISC OR Apache-2.0 |
| librashader | https://github.com/SnowflakePowered/librashader | C-API 0.12.0: MPL-2.0 OR GPL-3.0-only laut Quellmanifest; Fremdabhängigkeiten separat prüfen |
| RetroArch slang-shaders | https://github.com/libretro/slang-shaders | Unterschiedliche Lizenzen je Shader; Originalhinweise im unveränderten Asset-Baum |
| Vortice.Windows / SharpGen.Runtime | Über NuGet, Versionen im Projekt und Paketcache | MIT laut Paket-/Projektangaben |
| FFmpeg / ffprobe | https://ffmpeg.org/ – bereits vorhandene lokale Installation | Lizenz und Build-Optionen hängen vom verwendeten Build ab; Ausgabe von `ffmpeg -version` prüfen |

Die Shader werden mit ihren ursprünglichen Dateien und Hinweisen geladen. Bibliotheken und Werkzeuge sind getrennt vom eigenen Quellcode abgelegt. NTSCRT von Finn McKenty ist das Originalprojekt, dessen Kern-Workflow hier für Windows adaptiert wird.

Die lokalen Entwicklungswerkzeuge (.NET SDK, Rust und MinGW) sind Build-Werkzeuge und gehören nicht zu einem späteren App-Paket. Vor einer distributierbaren Ausgabe sind die für die tatsächlich enthaltenen Bibliotheken und Shader erforderlichen Lizenztexte, Hinweise und Quellcodeangebote zusammenzustellen.

Die Video-Vorschau ruft die lokale FFmpeg-Installation als getrennte Hilfsprozesse auf. Dieser Stand lädt und verteilt keinen FFmpeg-Build. Die verwendete lokale Konfiguration liegt in der nicht versionierten Datei `runtime/media-tools.json`.

Die portable lokale Prüf-ZIP enthält einen Abhängigkeitsbestand unter `licenses/dependency-inventory.json`, gesammelte Originalhinweise und das unveränderte Quellarchiv von librashader 0.12.0 unter `sources/`, sofern es bereitgestellt wurde. Das ist noch keine abgeschlossene Lizenzfreigabe für alle nativen Transitivabhängigkeiten oder Shader/Texturen. Insbesondere fehlen im gepinnten Glow-Shaderverzeichnis ausdrückliche Dateihinweise; siehe `RELEASE_REVIEW.md`.

Die Shader-Auswahl wird aus `components.json` beziehungsweise `components.local.json` geladen. Andere Sammlungen, zusätzliche Presets und externe Bibliotheks-/FFmpeg-Pfade sind ohne Quellcodeänderung konfigurierbar. Die App zeigt diese Herkunft unter **Komponenten & Credits** an.

Die vertiefte Prüfung vom 11.11.2026 ist in [DEPENDENCY_LICENSE_REVIEW.md](docs/DEPENDENCY_LICENSE_REVIEW.md) und [SHADER_LICENSE_REVIEW.md](docs/SHADER_LICENSE_REVIEW.md) dokumentiert. Sie enthält neue Originalbelege zu CRTSim und CRT-Royale sowie noch offene Fragen. Ergänzende Original-Lizenztexte stehen unter `licenses/upstream/`; ihre Quellen sind dort in `sources.json` verzeichnet. Alle sieben CRT-Presets bleiben erhalten. Die Prüfung der öffentlichen Binärweitergabe ist noch nicht abgeschlossen.

Der aktuelle librashader-Referenzbuild importiert zusätzlich Microsoft-Visual-C++-v14-Laufzeitdateien und die ältere DirectX-Datei `D3DX9_43.dll`. Die App nutzt selbst den D3D11-Pfad. Diese Microsoft-Dateien werden nicht aus dem System kopiert oder ohne eigene Prüfung weiterverteilt; offizielle Installationsquellen stehen in `RELEASE_README.md`.
