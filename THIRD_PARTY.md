# Herkunft und eingebundene Fremdprojekte

## Ausgangspunkt: NTSCRT von Finn McKenty

**Tape & Tube ist eine Windows-Adaption von [NTSCRT](https://github.com/finnmckenty/NTSCRT).** Das ursprüngliche macOS-Projekt liefert das Anwendungskonzept, den Aufbau der Signalkette und den grundlegenden Workflow. Diese Herkunft ist ein wesentlicher Bestandteil der Projektbeschreibung. Die Windows-Oberfläche und Host-Anbindung sind neu implementiert; der Swift-/Metal-Anwendungscode wird nicht mitgeliefert. Der aktuelle Funktionsumfang ist eine Teilmenge des Originals.

## Effektprozessoren und weitere Komponenten

Der Windows-Entwicklungsbuild verwendet auch NTSCRTs MIT-lizenzierte VCR- und TV-Empfangssimulation aus `port/engine/src/glitch`. Die Regler und Band-/Deckwerte werden aus den Originaldefinitionen dieses bereits gepinnten Engine-Quellstands übernommen.

Screen Loop nutzt aus demselben Quellstand die MIT-lizenzierte Rückkopplungs-Engine aus `port/engine/src/howl` und die fünf mitgelieferten NTSCRT-Presets aus `presets/Screen Loop`. Die Windows-Oberfläche und die Anbindung an Vorschau und MP4-Export gehören zum eigenen Host.

Die Basic-Bibliothek enthält die unveränderten JSON-Dateien **Clean VHS**, **Clean CRT** und **CRT aperture mask** von Finn McKenty. Sie stammen aus [NTSCRT 0.29.2](https://github.com/finnmckenty/NTSCRT/tree/889256dde173a89e475919be3d1bba34e6cc7355/presets) und behalten den [MIT-Originalhinweis](licenses/upstream/NTSCRT-MIT.txt). Die App kennzeichnet sie als NTSCRT-Originale. Das Windows-Paket liefert alle 44 Original-Looks und fünf Screen-Loop-Presets als unveränderte JSON-Dateien unter `presets/NTSCRT/` mit dem Original-Lizenztext aus. Die referenzierten CRT-Shader behalten ihre jeweiligen Lizenzen.

Der gemeinsame Effektkern verwendet Finn McKentys **MIT-lizenzierte Digitalvideo-Module und Rust-/wgpu-Engine aus NTSCRT 0.29.2** (Revision `889256dde173a89e475919be3d1bba34e6cc7355`). Der [Originalhinweis](licenses/upstream/NTSCRT-MIT.txt) gilt für diese übernommenen Module. Unser [Host-Patch](prototypes/processing-core/engine-adapter/host-lifecycle.patch) ergänzt Adapterprüfung und Zustandsreset. Die CRT-Rendergröße berechnet die Engine.

Der Tape-&-Tube-Entwicklungsstand und das Quellpaket enthalten unter `prototypes/processing-core/vendor/` die verwendeten NTSCRT-Quelldateien, die unveränderten Presets und die ursprünglichen Lizenzhinweise. Die Build-Vorbereitung übernimmt diese Dateien lokal und wendet die dokumentierten Patches an. Auf GitHub stehen der Installer und das passende Tape-&-Tube-Quellpaket gemeinsam beim jeweiligen Release. Das vollständige NTSCRT-Projekt bleibt über die in `upstream-sources.json` festgehaltene Revision zugänglich.

Der Worker verwendet librashader `76462c030b75c4f2d56e5386c3d4d7d1128318b8` mit Finns Push-Constant-Patch, getrennt von der bisherigen C-API-DLL 0.12.0. Quellen, SHA-256 und Änderungen stehen in [upstream-sources.json](prototypes/processing-core/upstream-sources.json). Unter Windows gehören seine drei MinGW-Laufzeit-DLLs zum Worker: libstdc++, libgcc und winpthreads. Deren Originalhinweise und GCC Runtime Library Exception werden beim lokalen Build mitkopiert. Das Paketverfahren erfasst zusätzlich die Worker-Cargo-Abhängigkeiten, Originalarchive und den Host-Patch. Eine vollständige neue Binärfreigabe einschließlich eingebetteter nativer Build-Abhängigkeiten bleibt vor der nächsten Veröffentlichung erforderlich. Die öffentliche Beta.2 enthält diese neue Integration noch nicht.

**Tape & Tube** liefert eigene Oberfläche, Ablaufsteuerung, Presets, Ausgabegeometrie und die Host-Anbindung. Die NTSC-/VHS-Simulation ist ntsc-rs; CRT-Effekte sind Arbeit der jeweiligen Shader-Autoren; librashader führt diese Shader aus; FFmpeg übernimmt Medienverarbeitung. Unsere GPL-3.0-only-Lizenz ersetzt keine dieser Fremdlizenzen.

| Bestandteil | Quelle | Lizenzangabe des Projekts |
|---|---|---|
| ntsc-rs | https://github.com/ntsc-rs/ntsc-rs | MIT OR ISC OR Apache-2.0 |
| librashader | https://github.com/SnowflakePowered/librashader | C-API 0.12.0: MPL-2.0 OR GPL-3.0-only laut Quellmanifest; Fremdabhängigkeiten separat prüfen |
| RetroArch slang-shaders | https://github.com/libretro/slang-shaders | Unterschiedliche Lizenzen je Shader; Originalhinweise im unveränderten Asset-Baum |
| Vortice.Windows / SharpGen.Runtime | Über NuGet, Versionen im Projekt und Paketcache | MIT laut Paket-/Projektangaben |
| FFmpeg / ffprobe | https://ffmpeg.org/ – getrennte Prozesse; optionaler Setup-Download von Gyan Doshi / gyan.dev oder eigene Installation | Referenzdownload 9.0.2 Essentials: GPL v3 laut Anbieter; andere Builds haben eigene Optionen und Lizenzen. Originalhinweise unter `licenses/ffmpeg` nach Setup-Download |

Die Shader werden mit ihren ursprünglichen Dateien und Hinweisen geladen. Bibliotheken und Werkzeuge sind getrennt vom eigenen Quellcode abgelegt. NTSCRT von Finn McKenty ist das Originalprojekt, dessen Kern-Workflow hier für Windows adaptiert wird.

Die lokalen Entwicklungswerkzeuge (.NET SDK, Rust-Compiler und MinGW-Compiler) sind Build-Werkzeuge und gehören nicht zum App-Paket. Benötigte Laufzeitdateien sind davon getrennt zu betrachten; insbesondere verwendet der neue Worker die oben genannten MinGW-Laufzeit-DLLs. Vor einer distributierbaren Ausgabe sind die für die tatsächlich enthaltenen Bibliotheken und Shader erforderlichen Lizenztexte, Hinweise und Quellcodeangebote zusammenzustellen.

Die Video-Vorschau ruft FFmpeg als getrennte Hilfsprozesse auf. Ab dem lokalen Entwicklungsstand 0.1.0-beta.2 kann der Installer einen festgelegten Build direkt beim Anbieter herunterladen, seine SHA-256-Prüfsumme prüfen und ihn nur für diese App installieren. Der Build wird nicht in unseren Installer eingebettet oder auf unserem Release gespiegelt. Originallizenz, README mit Build-Optionen/Quellreferenz und Dokumentation werden unter `licenses/ffmpeg` erhalten. Details und Grenzen: [FFmpeg-Integration](docs/FFMPEG_INSTALLATION.md). Eigene Builds bleiben über Umgebungsvariablen, Komponentenpfade, `runtime/media-tools.json` oder PATH verwendbar und behalten ihre jeweiligen Lizenzen.

Die portable lokale Prüf-ZIP enthält einen Abhängigkeitsbestand unter `licenses/dependency-inventory.json`, gesammelte Originalhinweise und das unveränderte Quellarchiv von librashader 0.12.0 unter `sources/`, sofern es bereitgestellt wurde. Das ist noch keine abgeschlossene Lizenzfreigabe für alle nativen Transitivabhängigkeiten oder Shader/Texturen. Insbesondere fehlen im gepinnten Glow-Shaderverzeichnis ausdrückliche Dateihinweise; siehe `RELEASE_REVIEW.md`.

Die Shader-Auswahl wird aus `components.json` beziehungsweise `components.local.json` geladen. Andere Sammlungen, zusätzliche Presets und externe Bibliotheks-/FFmpeg-Pfade sind ohne Quellcodeänderung konfigurierbar. Die App zeigt diese Herkunft unter **Komponenten & Credits** an.

Die vertiefte Prüfung vom 11.11.2026 ist in [DEPENDENCY_LICENSE_REVIEW.md](docs/DEPENDENCY_LICENSE_REVIEW.md) und [SHADER_LICENSE_REVIEW.md](docs/SHADER_LICENSE_REVIEW.md) dokumentiert. Sie enthält neue Originalbelege zu CRTSim und CRT-Royale sowie noch offene Fragen. Ergänzende Original-Lizenztexte stehen unter `licenses/upstream/`; ihre Quellen sind dort in `sources.json` verzeichnet. Alle sieben CRT-Presets bleiben erhalten. Die Prüfung der öffentlichen Binärweitergabe ist noch nicht abgeschlossen.

Der aktuelle librashader-Referenzbuild importiert zusätzlich Microsoft-Visual-C++-v14-Laufzeitdateien und die ältere DirectX-Datei `D3DX9_43.dll`. Die App nutzt selbst den D3D11-Pfad. Diese Microsoft-Dateien werden nicht aus dem System kopiert oder ohne eigene Prüfung weiterverteilt; offizielle Installationsquellen stehen in `RELEASE_README.md`.
