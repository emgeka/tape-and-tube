# Gemeinsamer Verarbeitungskern — isolierter Prototyp

Etappe 3, Stand 17.11.2026. Dieser zunächst isolierte Versuch enthält jetzt auch den dauerhaften Worker für die lokale Windows-Integration. Die veröffentlichte Beta.2 bleibt unverändert. Kein Linux-App-Binärpaket.

Der Prototyp verbindet Finn McKentys MIT-lizenzierte Digitalvideo-Stufe und Rust-/wgpu-Engine aus **NTSCRT v0.29.2** mit der eigenen GPL-3.0-only-Host-Anbindung. Herkunft, Versionen und Prüfsummen stehen in [upstream-sources.json](upstream-sources.json).

## Was implementiert ist

- Zustandsbehaftete Digitalvideo-Sessions mit versionierter C-Schnittstelle: RGBA8, Stride, Framenummer, rationale Bildrate, echte frühere Quellframes, Reset und kontrollierte Fehler.
- Sechs Digitalformate und 20 Originalparameter: Kamera, DCT, Video 1, Paketverlust und Datamosh.
- Getrenntes Kommandozeilenprogramm `tapetube-core-probe`: Digitalvideo → NTSC → Downscale → CRT → Ausgabe. Es verwendet die Original-Engine und führt NTSC/CRT genau einmal aus.
- JSON-Protokoll 1 statt Bindung an eine bestimmte Oberfläche. Rohbilder werden auf Anfrage anhand ihrer ursprünglichen Frameindizes verarbeitet; Vorschauanforderungen und vollständiger Export verwenden dieselbe Verarbeitung.
- Prüfprogramm für tatsächliche MP4-Dekodierung, PNG, sieben Shader und MP4/AAC-Export mit FFmpeg. Der Medienversuch arbeitet mit einer Rohbilddatei auf der Festplatte, damit echte frühere Frames für Seek verfügbar sind. Das ist noch kein optimierter Streaming-/Cacheadapter.

Der C-Host und der vollständige Engine-Prozess sind zwei geprüfte Integrationswege. Die Engine verwendet ihren eigenen Original-Codec; sie lädt nicht zusätzlich den C-Host. Die Windows-App verwendet jetzt den Prozess mit `--worker`; `--job` bleibt als unabhängige Referenz verfügbar. Details: [Windows-Integration](../../docs/DIGITAL_VIDEO_INTEGRATION.md). Eine dynamische Plugin-Verwaltung ist weiterhin nicht umgesetzt.

VCR, Glitch, Screen Loop und Effekt-Keyframes sind hier noch nicht bedienbar. Die veröffentlichte App bekommt durch diesen Prototyp noch keine neuen Schalter oder Presets. Die bestehenden Looks und sieben CRT-Presets werden nicht umgeschrieben.

## Windows bauen und testen

Vorhandene projektlokale Rust-/MinGW-/CMake-/Ninja-/NET-Werkzeuge und FFmpeg verwenden:

Für den GPU-Build müssen CMake und Ninja auf PATH vorhanden sein; alternativ erkennt das Skript die lokale Werkzeugablage `.tools/w64devkit/bin`. Das bisherige Windows-`setup.ps1` richtet diese beiden zusätzlichen Werkzeuge nicht automatisch ein. Sie sind Entwicklungswerkzeuge und werden für die Ausführung der gebauten App nicht benötigt.

```powershell
./prototypes/processing-core/prepare.ps1
./prototypes/processing-core/build.ps1
./prototypes/processing-core/test.ps1 -SkipBuild
```

`prepare.ps1` übernimmt die mitgeführten NTSCRT-Quellen aus `vendor/` und wendet NTSCRTs librashader-Patch sowie unseren [Host-Lifecycle-Patch](engine-adapter/host-lifecycle.patch) an. Der Host-Patch ergänzt eine frühe GPU-Prüfung, ausdrückliche Softwarefreigabe und einen Zustandsreset ohne neues Grafikgerät. Das festgelegte librashader-Archiv wird bei Bedarf geladen und per SHA-256 geprüft. `Cargo.lock` fixiert die Rust-Abhängigkeiten.

Für Wiederholung ohne Downloads `-Offline` verwenden. Dafür müssen das librashader-Archiv und die übrigen Build-Abhängigkeiten bereits vorliegen. Zum gezielten Wechsel des NTSCRT-Stands das neue, gepinnte Originalarchiv in den Download-Cache legen und `scripts/copy-ntscrt-source.ps1 -FromOriginalArchive -Destination <Prüfverzeichnis>` ausführen. Die daraus übernommenen Dateien, Patches und Integrationstests vor der Freigabe gemeinsam prüfen.

## Linux Mint / VirtualBox

Das Skript läuft **innerhalb** der Linux-VM. Das Quell-Testpaket entpacken und auf das Linux-Dateisystem kopieren, z.B. in den persönlichen Ordner; C++-/Rust-Builds möglichst nicht direkt im VirtualBox-Freigabeordner ausführen.

Voraussetzungen: Internet für Cargo, Rust mindestens 1.99 (Windows-Referenz 1.99.1), C++-Compiler, CMake, Ninja, Git, Curl, Unzip, SHA256SUM, Node.js und FFmpeg/ffprobe. Zusätzlich ein funktionsfähiges OpenGL- oder Vulkan-Verfahren, auch Software-Rendering muss als solches dokumentiert werden. Das Skript installiert keine Systempakete und ändert keine VM-Einstellungen.

Typische Mint-Entwicklungswerkzeuge können bewusst über die Paketverwaltung eingerichtet werden:

```sh
sudo apt install build-essential cmake ninja-build git curl unzip nodejs ffmpeg libvulkan1 mesa-vulkan-drivers libegl1 libgl1
```

Rust über den [offiziellen rustup-Weg](https://rust-lang.org/tools/install/) einrichten und Rust 1.99.1 auswählen, falls noch nicht vorhanden. Für den zusätzlichen C-ABI-Verbrauchertest ist das .NET-10-SDK optional; sein Fehlen wird ausdrücklich aufgezeichnet. Rust-Tests und vollständige Render-/Video-Prüfungen laufen unabhängig davon.

Vom entpackten Projektordner aus:

```sh
bash prototypes/processing-core/linux-test.sh
```

Standard ist `NTSCRT_BACKEND=gl`; `NTSCRT_BACKEND=vulkan bash prototypes/processing-core/linux-test.sh` prüft gezielt Vulkan. Eigene FFmpeg-Werkzeuge lassen sich über `TAPETUBE_FFMPEG` und `TAPETUBE_FFPROBE` auswählen. Ein Fehler führt zum Abbruch, ein fehlender Grafikadapter wird nicht als bestandener Test übersprungen. Protokolle, GPU-Name, Testbilder und Video liegen unter `artifacts/core-prototype/linux-…`. `.NET`-ABI-Ausführung und GPU-Art separat beachten. Erst ein tatsächlich erfolgreicher Lauf ist ein Linux-Nachweis.

## Bereits ausgeführte Windows-Prüfungen

- Build mit Rust 1.99.1/GNU x64, ursprünglichem librashader-Pin und dessen Push-Constant-Patch erfolgreich.
- **12 Rust-Tests** der C-Hostgrenze bestanden, darunter Fehlererholung, Framehistorie, unabhängige Instanzen, Stride/Alpha, reproduzierbares Datamosh und begrenzter Wiederaufbau nach extremen Standbildsprüngen.
- **433 Prüfungen** des echten .NET-C-ABI-Verbrauchers bestanden; drei Fälle mit je 42 bytegenauen Framevergleichen und 14 erwarteten Fehlerfällen.
- **29 Integrationsprüfungen** bestanden: alle sieben CRT-Presets; DCT/Video 1/Vorhersage/Datamosh bei Seek/Loop/Auslassen; vollständige Kette mit easymode; PNG und H.264/AAC-Video; geschützte Quellen/Ausgaben und ungültige Jobs.
- Windows-Grafikgerät: NVIDIA GeForce RTX 5060 Ti, D3D12. Generierter Testclip: 160×120, 12 fps, 3 Sekunden; Ausgabe 320×240. Das kleine Material ist kein Echtzeitversprechen für HD/4K.
- Im Test lagen Bild-/Tonbeginn und -dauer innerhalb einer Videoframe-Toleranz; der Beginn eines Testtons blieb innerhalb 10 ms. Keine schwarzen Frames in diesem Testexport. Kein Nachweis zur Ursache des früheren VirtualBox-/CRTsim-Fehlers.

Ergebnisdateien: `artifacts/core-prototype/windows-final-verified/report.json`, `artifacts/core-prototype/digital-abi-tests.json`, `artifacts/core-prototype/digital-unit-tests.log`. Shaderzeiten enthalten den ersten Render samt Kompilierung; sie sind keine belastbaren durchschnittlichen Wiedergaberaten.

## Ausgeführte Linux-Prüfungen vom 17.11.2026

Nativ unter Linux Mint 22.3 x86-64 in VirtualBox gebaut: ebenfalls **12 Rust-Tests, 433 externe C-ABI-Prüfungen und 29 Integrationsprüfungen bestanden**. PNG und MP4/AAC mit synchronem Ton sind nachgewiesen. Die drei geprüften Digitalvideo-Bildfolgen stimmen bytegenau mit Windows überein.

Der Standard-VM-Treiber scheitert an fehlenden Compute-Shadern. Der erfolgreiche Lauf verwendet `NTSCRT_BACKEND=gl LIBGL_ALWAYS_SOFTWARE=1`, also Mesa llvmpipe auf der CPU. Dies verändert nur die Test-Prozessumgebung. Hardware-Linux-Leistung bleibt unbewiesen.

Zusätzlich dieselben unter Windows dekodierten RGBA-Eingaben für Frame 0 aller sieben CRT-Presets verglichen: mittlere absolute RGB-Abweichungen 0,096–0,736 auf der Skala 0–255, lokale Maxima bis 20, keine Alphaabweichungen. Die Shaderbilder sind nicht bytegleich. Details und Wiederholungsanleitung: [Linux-Testbericht](../../docs/LINUX_CORE_TEST.md).

Weitere offene Punkte sind Messungen mit größeren/echten Quellen, vollständige Farb-/Alphaübergänge, zeitliche CRT-Historie jenseits des easymode-Vergleichs, Streaming/Abbruch und die endgültige Produktions-/UI-Entscheidung. Die native Linux-Funktionsprobe für den Kern ist erbracht.

## Lizenzen und Herkunft

Eigener Host, Runner, Testcode und Skripte: **GPL-3.0-only**, siehe Projekt-LICENSE. NTSCRT-Code: **MIT, Copyright Finn McKenty 2026**; dessen Lizenz und Quellhinweise bleiben im Originalarchiv. librashader behält **MPL-2.0 OR GPL-3.0-only**; die veränderte Push-Constant-Datei ist durch den mitgelieferten Patch und vollständigen Originalquellstand nachvollziehbar. ntsc-rs behält **MIT OR ISC OR Apache-2.0**. Shader behalten ihre jeweiligen Datei-Lizenzen und Hinweise.

Die Originalarchive enthalten ihre Lizenztexte und Fremdhinweise. Die zusätzlichen Rust-Buildabhängigkeiten sind durch Cargo.lock nachvollziehbar, aber noch nicht für eine neue Binärveröffentlichung vollständig inventarisiert. Der Prototyp verändert keine Lizenzangaben oder Freigaben der veröffentlichten Beta.2.
