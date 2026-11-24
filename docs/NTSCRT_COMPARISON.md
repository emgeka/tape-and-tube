# NTSCRT und Tape & Tube — Funktions- und Architekturvergleich

Stand: 16.11.2026. Ergebnis der zweiten Etappe des [Sechs-Punkte-Plans](ROADMAP.md).

## Referenz und Aussagegrenzen

- **Tape & Tube:** veröffentlichte Windows-Version [0.1.0-beta.2](https://github.com/emgeka/tape-and-tube/releases/tag/v0.1.0-beta.2), Veröffentlichungskommit `fac7bacf50c8821c98dfdc7e612ec2f4c8286fe3`; aktuelle lokale Anwendungsquellen untersucht.
- **NTSCRT:** beim Abruf neueste Veröffentlichung [v0.26.0](https://github.com/finnmckenty/NTSCRT/releases/tag/v0.26.0), veröffentlicht am 16.11.2026. Tag und `main` zeigten auf `cfde8457e34eee55ab6900655efaaba8b2e59c18`. Die Veröffentlichung enthält ein Windows-x64-ZIP und ein macOS-DMG. Die folgenden Quelllinks sind auf diesen Commit festgelegt.
- Geprüft wurden Dokumentation, Rust-Module, Schnittstellen, Abhängigkeiten und vorhandene Testquellen. Fremde Programme wurden für diese Analyse nicht ausgeführt; weder deren aktuelle CI-Ergebnisse noch Bildqualität, Geschwindigkeit oder Linuxbetrieb wurden von uns praktisch bestätigt.
- „Vorhanden“ beschreibt belegten Code/Funktionsumfang. Gleiche Namen oder sieben gleiche Shaderfamilien bedeuten keine identischen Pixel. Technische Aufwandseinschätzungen und Prioritäten sind unsere Schlussfolgerungen.

## Funktionsmatrix

Priorität: **P1** zum Schließen der Hauptlücken; **P2** anschließend oder als gezielte Ergänzung; **P3** später. „Grundlage“ muss im gemeinsamen Kern berücksichtigt werden.

| Bereich | NTSCRT Windows v0.26.0 | Tape & Tube beta.2 | Konsequenz und Priorität |
| --- | --- | --- | --- |
| Standbilder und Videos | Bildimport sowie Media-Foundation-Video; zusätzlich WIC für z.B. HEIC | WPF-Bildimport und FFmpeg-/ffprobe-Videos | Gemeinsame FFmpeg-Ein-/Ausgabe behalten und für Linux anpassen; Formatunterstützung konkret testen. Grundlage. |
| Quellbeschnitt | Seitenverhältnis, verschiebbarer Ausschnitt und Zoom **vor** der Effektkette | Fit oder zentrierter Crop **bei der Ausgabe** | Eigene Funktion erforderlich; Ausgabe-Crop ist kein gleichwertiger Ersatz. P2. |
| Retro-Auflösung | Sechs Verfahren: Nearest, stabilisiertes Nearest, Bilinear, Bicubic, Lanczos, Area; One-field-Verarbeitung | Retrobreiten 256/320/640/1280 und WPF-Resize vor CRT | Downscale vom CRT-Schalter entkoppeln; Methoden und Zeilenmodell vergleichen. Grundlage/P1 für Digitallooks. |
| Digitalvideo: Kamera | Sensor-/Schwachlichtrauschen, Moiré, Glättung, CCD-Ton, Weißabgleich, Tint, Sättigung, Schärfung/Halobreite, Capturerate und Fields | Keine entsprechende eigene Stufe | `port/codec` ist erster Übernahmekandidat. P1. |
| Digitalvideo: Codec | DCT mit Bewegungsvorhersage sowie Video-1-Blockfarben; Qualität, Farbabtastung, Bitratenverhalten, Keyframeintervall, Deblocking | Keine entsprechende eigene Stufe | Echte zeitliche Codecverarbeitung verwenden; kein bloßer Block-/Rauschfilter. P1. |
| Digitalvideo: Fehler/Formate | Datenverlust/Datamosh; sechs Formate: Webcam, Video call, MiniDV, Early web video, CD-ROM, Datamosh | Fehlt | Historie, Quellframes und Parameterverlauf für Sprünge nötig. P1. |
| NTSC/VHS-Signal | ntsc-rs mit Parametern, Haus-Defaults und Alt-Look-Behandlung | ntsc-rs über eigene C-Schnittstelle; dessen VHS-Parameter vorhanden | Grundfunktion vorhanden; Versionen, Defaults, Farbauslegung und Presetmigration gesondert vergleichen. |
| Physikalische VCR-Stufe | Bandoberfläche/Köpfe/Transport/Tracking, SP/LP/EP, acht Band- und fünf Deckvorgaben sowie Kopiergenerationen | VHS-Anteile aus ntsc-rs, keine getrennte Band-/Decksimulation | Größte analoge Effektlücke. Rust-Simulation und GPU-Rekonstruktion gemeinsam untersuchen. P1. |
| TV-Glitch | Empfängersimulation u.a. für Bildrollen, Synchronisationsfehler, Ghosting, Hum und Signalverlust | Keine entsprechende eigene Stufe | Technisch mit VCR verbunden; nicht als beliebige Einzelshader planen. P1. |
| CRT | Dieselben sieben angebotenen Shaderfamilien; Parameter und Abhängigkeitsregeln | Sieben getestete Presets mit Parametern über librashader/D3D11 | Alle erhalten. Unterschiedliche librashader-/Shaderstände verhindern ungeprüfte Gleichheitsbehauptung. Grundlage. |
| Screen Loop | Rückkopplung, Kamera, Texturring, Vorlauf, Bewegung, Luma-Key und eigene Presets | Fehlt | Hoher Aufwand; gesonderte Renderbetriebsart nach normaler Filmkette. **Auch NTSCRT deaktiviert dabei Digitalvideo.** P1 nach VCR/Glitch. |
| Effekt-Keyframes | Zeitleiste für NTSC, Digitalvideo, Glitch/VCR und Shader; fünf Easingarten | Keine Zeitleiste; Effektrauschen kann über Framenummer variieren | Zeit-/Historienvertrag jetzt vorsehen, UI in Etappe 5. Grundlage/P1. |
| Vollständige Looks | Mac-/Windows-JSON mit Migration, Keyframes und erhaltener unbekannter Information | Eigenes JSON v1 für NTSC/CRT/Ausgabe, kein vollständiger NTSCRT-Lookimport | Import und Migration ausdrücklich entwickeln. Teilweise importierte Looks nie als vollständig darstellen. P1, UI Etappe 5. |
| Vergleich und Transport | Verschiebbarer Vergleich, Abspielen und framegenaues Scrubbing | Vergleich, Wiedergabe, Seek, Einzelframes und Vorschau-Loop | Kernbedienung vorhanden; zeitliche Effektzustände beim Seek/Loop erweitern. Grundlage. |
| Vorschaukomfort | Zoom/Pan, Integer scale, Live noise sowie Vorberechnung und Cache | Eingepasste Vorschau; kein entsprechender Zoom/Pan-/Render-ahead-Arbeitsfluss | Nach Kernmessung priorisieren. P2. |
| PNG | Gerendertes Bild bzw. Videoframe; zusätzlich Viewport-Ausgabe | Gerendertes Bild bzw. Videoframe | Viewportmodus optional ergänzen. P2. |
| MP4 und Ton | H.264/HEVC und AAC-Audio; wiederholter Inhalt innerhalb des Exports | H.264 mit erster originaler Audiospur als AAC, feste Exportparameter | HEVC/Qualitätswahl und Exportwiederholungen ergänzen. Unser Loop-Schalter betrifft nur Vorschau. P2. |
| GIF und animierte Standbilder | GIF; Standbild als zeitabhängiges Video, mit/ohne Effekt-Keyframes | Kein GIF und kein Standbild-Videoexport | Gemeinsame Zeitleiste/Exportuhr verwenden. P2. |
| Ausgabegröße/Scanlines | Freie Exportgrößen, Scanline-grid-Anpassung und gerenderte Übergröße | Automatisch, 720p/1080p/4K/eigene Größe, Fit/Crop; CRT-Rendergrößenbegrenzung | Unsere Größenwahl bleibt. Scanline-Raster und Hoch-/Herunterskalierung gezielt mit Hochformat vergleichen. P2. |
| Installation und Updates | Portables Windows-ZIP, Updatehinweis | Installer mit Komponentenwahl, FFmpeg-Auswahl/Download und Release-/Beta-Kanälen | Bereits eigenständiger Arbeitsfluss; für künftige Plattformen weiterführen. Kein gemessener Gesamtqualitätsvorsprung. |
| Sprachen | Für diese Untersuchung kein vollständiges Sprachaudit erstellt | Zehn UI-Sprachen und Automatik | Mehrsprachigkeit beim Ausbau erhalten. |
| Linux | Kein Linux-Paket in der untersuchten Veröffentlichung; port/media hat Nicht-Windows-Fehlerpfade | Keine native Linux-Ausgabe | Gemeinsamer Kern ist plausibel, Linuxbetrieb bleibt nachzuweisen. Etappen 3/6. |

Aktualisierung vom 20.11.2026. NTSCRT dokumentiert Kommandozeile und MCP-Server für den Windows-Port. Beide gehören damit zum vollständigen Windows-Vergleich. Der Schwerpunkt liegt zuerst auf Effekten und Bedienung. ProRes ist beim Windows-Port ausdrücklich ausgenommen. [Windows-Dokumentation von NTSCRT](https://github.com/finnmckenty/NTSCRT#download)

## Verbleibende Windows-Lücken am 23.11.2026

Der folgende Abgleich vergleicht den lokalen Tape-&-Tube-Build `20261123-143424-650` mit der festgehaltenen Windows-Referenz NTSCRT 0.28.1. Grundlage sind die vorhandenen Windows-Quellen und die lokalen Testergebnisse. Laufzeit und Darstellungsqualität beider Programme wurden für diesen Abgleich nicht gegeneinander gemessen.

| Bereich | Stand und verbleibende Lücke |
| --- | --- |
| **Videovorschau** | Tape & Tube dekodiert fortlaufend und hält das zuletzt gelesene Bild. Bei Pause und Positionssprung berechnet es das gewählte Effektbild. NTSCRT dekodiert auf einem eigenen Thread bis zu zwölf Bilder voraus und bereitet bei pausierter Vorschau nach rund 250 ms zusätzlich die Verarbeitung vor Glitch und CRT vor. NTSCRT speichert diese Zwischenbilder mit einem Speicherbudget und nutzt sie beim Fortsetzen und Springen. [Tape-&-Tube-Videovorschau](../src/Ntscrt.Windows/MainWindow.Video.cs), [NTSCRT-Decoder](https://github.com/finnmckenty/NTSCRT/blob/0ab60bf952b09a0c41ea4290979f23dadd963614/port/app/src/prefetch.rs), [NTSCRT-Vorschauworker](https://github.com/finnmckenty/NTSCRT/blob/0ab60bf952b09a0c41ea4290979f23dadd963614/port/app/src/worker.rs) |
| **Screen Loop und Standbild-Vorschau** | Die Screen-Loop-Parameter und Animation sind integriert. NTSCRT lässt den Fluchtpunkt und das Ziel seiner Bewegung direkt im Bild ziehen. Tape & Tube stellt die Werte über Regler ein. NTSCRT kann außerdem Zufallseffekte auf einem Standbild direkt in der normalen Vorschau laufen lassen. Tape & Tube nutzt dafür eine Standbild-Animation oder Screen Loop. [Tape-&-Tube-Screen-Loop](../src/Ntscrt.Windows/MainWindow.ScreenLoop.cs), [NTSCRT-Screen-Loop](https://github.com/finnmckenty/NTSCRT/blob/0ab60bf952b09a0c41ea4290979f23dadd963614/port/app/src/screen_loop.rs), [NTSCRT-Vorschau](https://github.com/finnmckenty/NTSCRT/blob/0ab60bf952b09a0c41ea4290979f23dadd963614/port/app/src/main.rs) |
| **Interne Auflösung** | NTSCRT bietet einen eigenen Schalter und eine Auswahl der Downscale-Methode. Tape & Tube bietet die interne Breite, verwendet beim gemeinsamen Renderer für eigene Einstellungen Area und übernimmt die Methode aus importierten NTSCRT-Looks. Die direkte Wahl für eigene Looks fehlt. [Tape-&-Tube-Effektpipeline](../src/Ntscrt.Windows/EffectPipeline.cs), [Gemeinsamer Worker](../prototypes/processing-core/runner/src/worker.rs), [NTSCRT-Windows-Oberfläche](https://github.com/finnmckenty/NTSCRT/blob/0ab60bf952b09a0c41ea4290979f23dadd963614/port/app/src/main.rs) |
| **Automatisierung** | Tape & Tube rendert über CLI und MCP aus einer Quelle und einem optionalen Look. NTSCRT bietet in der Windows-CLI zusätzlich direkte Effektparameter, mehrere PNG-Zeitpunkte, Nahaufnahmen, eine Auflösung ohne Rendern (`--dry-run`) und Fortschrittsmeldungen. Der MCP-Server ergänzt unter anderem Look-Liste, Parameterbeschreibungen, einen Vorschauaufruf und Statusabfragen. [Tape-&-Tube-CLI](../src/TapeTube.Cli/Program.cs), [Tape-&-Tube-MCP](../src/TapeTube.Cli/McpServer.cs), [NTSCRT-CLI-Hilfe](https://github.com/finnmckenty/NTSCRT/blob/0ab60bf952b09a0c41ea4290979f23dadd963614/port/app/src/cli/help.rs), [NTSCRT-MCP](https://github.com/finnmckenty/NTSCRT/blob/0ab60bf952b09a0c41ea4290979f23dadd963614/port/app/src/cli/mcp.rs) |

H.264 und HEVC mit Qualitätswahl, MP4-Durchläufe, GIF-Breite und -Bildrate, GIF-Dateigrößenschätzung und das Scanline-Raster sind im lokalen Stand vorhanden. Für die Windows-GUI listet NTSCRT 0.28.1 PNG, H.264, HEVC und GIF. ProRes und der im allgemeinen README beschriebene eigene Viewport-Export erscheinen in diesem Windows-Speicherdialog nicht.

### Nachtrag zur Videovorschau

Der neue Tape-&-Tube-Testbuild bereitet in der Pause Folgebilder für die aktuelle Quelle und den aktuellen Look vor. Bis zu zwölf Quell- und Effektbilder belegen zusammen höchstens 96 MiB. Der Vorschaupuffer wird beim Fortsetzen verwendet und bei Positions- oder Einstellungsänderungen geleert. Der Windows-Abgleich oben beschreibt den Stand vor dieser Änderung.

Belege: [README](https://github.com/finnmckenty/NTSCRT/blob/cfde8457e34eee55ab6900655efaaba8b2e59c18/README.md), [Port-Überblick](https://github.com/finnmckenty/NTSCRT/blob/cfde8457e34eee55ab6900655efaaba8b2e59c18/port/README.md), [Windows-Plan und Prüfberichte des Autors](https://github.com/finnmckenty/NTSCRT/blob/cfde8457e34eee55ab6900655efaaba8b2e59c18/docs/windows-port-plan.md), [Windows-Exportcode](https://github.com/finnmckenty/NTSCRT/blob/cfde8457e34eee55ab6900655efaaba8b2e59c18/port/app/src/export.rs). Angaben des Autors über Testergebnisse sind keine von uns wiederholten Messungen.

## Wiederverwendung: konkrete Module

| Quelle | Abgrenzung und Lizenzbefund | Plattform und Aufwand | Empfehlung |
| --- | --- | --- | --- |
| NTSCRT `port/codec` | Separates Rust-Paket `ntscrt-codec`, MIT, als normale Abhängigkeit nur Rayon. 20 Parameter, sechs Formate; RGBA8, `DigitalVideoRenderer::process`, `process_unseen`, Historie und Checkpoints | CPU-Code ohne Windows-/WPF-/GPU-Bindung in der untersuchten Schnittstelle. Integration **mittel**, vollständige Zeit-/Seek-/Exportanbindung **mittel bis hoch**. Linux-Build noch nicht geprüft | **Erster Prototyp.** Eigenen schmalen Host vorsehen; bestehende DLL besitzt diese API noch nicht. |
| NTSCRT `port/engine` | MIT-Rust-Kern um ntsc-rs, Downscale, Codec, VCR/Glitch, Screen Loop und librashader/wgpu. Keine vorhandene stabile C-Plugin-Schnittstelle | Eigenes wgpu-28-Gerät, RGBA8-Rückgabe. Vulkan/GL als Linux-Kandidaten; Windows nutzt andere Grafik-API als unser D3D11-Host. **Hoch**, aber breitester Wiederverwendungsnutzen | Kandidat für gemeinsamen langfristigen Effektkern. Host-Probe vor UI-Umbau; keine dauerhaft parallel nachgebauten VCR-/Glitchsimulationen. |
| VCR + Glitch innerhalb der Engine | CPU-Simulation plus WGSL-Pässe; 31 gemeinsame Parameter. VCR läuft vor Fernsehempfänger | Zustandsbehaftet; voneinander abhängige Band-/Empfänger-/GPU-Anteile. **Mittel bis hoch** zusätzlich zur GPU-Hostgrundlage | Zusammen übernehmen/adaptieren, Wirkung separat bedienbar machen. |
| Screen Loop innerhalb der Engine | Rückkopplungsring, Vorlauf und 23 Parameter; eigener Presettyp `ntscrt-video-feedback` v2 | Framehistorie/Anlauf bestimmen Ergebnis. **Hoch**; Digitalvideo aktuell bewusst ausgeschlossen | Nach normaler Filmkette; Einschränkung in UI und Presets sichtbar machen. |
| `look.rs` / `timeline.rs` | MIT; Formatlesen/-schreiben, Migration, Easing und Parameterauswertung | Daten-/Logikcode gut wiederverwendbar, UI und unser Bestandsformat brauchen eigene Adapter. **Mittel bis hoch** | Datenvertrag früh berücksichtigen, vollständige Gestaltung in Etappe 5. |
| NTSCRT `port/media` | MIT-Code, aber Media Foundation/WIC; Nicht-Windows-Zweige liefern Fehler | Kein verwendbarer Linux-Videoweg. **Hoch**, wenn separat portiert | Stattdessen unsere vorhandene FFmpeg-Prozessanbindung plattformneutral machen und mit dem Kern verbinden. |

MIT-Befund: [Original-Lizenz](https://github.com/finnmckenty/NTSCRT/blob/cfde8457e34eee55ab6900655efaaba8b2e59c18/LICENSE), [Codec-Abhängigkeiten](https://github.com/finnmckenty/NTSCRT/blob/cfde8457e34eee55ab6900655efaaba8b2e59c18/port/codec/Cargo.toml), [Engine-Abhängigkeiten](https://github.com/finnmckenty/NTSCRT/blob/cfde8457e34eee55ab6900655efaaba8b2e59c18/port/engine/Cargo.toml). Übernommene NTSCRT-Dateien behalten Finn McKentys MIT-/Copyright-Hinweise. Unser eigener Anwendungscode und Host bleiben GPL-3.0-only; Fremdmodule und deren Abhängigkeiten behalten ihre jeweiligen Lizenzen. Keine pauschale Umdeklarierung der gesamten Kette als MIT oder GPL.

Schnittstellen und Zustand: [Codec-Renderer](https://github.com/finnmckenty/NTSCRT/blob/cfde8457e34eee55ab6900655efaaba8b2e59c18/port/codec/src/renderer.rs#L112), [Parameter und Formate](https://github.com/finnmckenty/NTSCRT/blob/cfde8457e34eee55ab6900655efaaba8b2e59c18/port/codec/src/params.rs#L73), [Engine](https://github.com/finnmckenty/NTSCRT/blob/cfde8457e34eee55ab6900655efaaba8b2e59c18/port/engine/src/lib.rs#L35), [VCR-/Glitch-Historie](https://github.com/finnmckenty/NTSCRT/blob/cfde8457e34eee55ab6900655efaaba8b2e59c18/port/engine/src/glitch/stage.rs#L14), [Rückkopplung und Vorlauf](https://github.com/finnmckenty/NTSCRT/blob/cfde8457e34eee55ab6900655efaaba8b2e59c18/port/engine/src/howl/feedback.rs#L160), [Screen Loop ohne Digitalvideo](https://github.com/finnmckenty/NTSCRT/blob/cfde8457e34eee55ab6900655efaaba8b2e59c18/port/app/src/screen_loop.rs#L175), [Medienblocker unter Linux](https://github.com/finnmckenty/NTSCRT/blob/cfde8457e34eee55ab6900655efaaba8b2e59c18/port/media/src/lib.rs#L88).

## Ergänzende Open-Source-Effekte

Diese Auswahl schafft zusätzliche Gestaltungsmöglichkeiten. Sie schließt die fehlenden NTSCRT-Hauptstufen nicht allein.

| Kandidat | Nutzen / Priorität | Aufwand und zeitliche Besonderheit | Lizenz-/Verfügbarkeitsbefund |
| --- | --- | --- | --- |
| FFmpeg `chromashift` / `rgbashift` | Bewusst steuerbarer Farbkanalversatz, P2 | Klein bis mittel; ohne Bildhistorie gut isolierbar. Dieselbe Position in Vorschau/Export vorgeben | Im geprüften Gyan-9.0.2-Essentials-Build vorhanden. Filterdatei LGPL-2.1-or-later; die Lizenz des gesamten ausgelieferten Builds bleibt gesondert maßgeblich. |
| FFmpeg `lagfun`, eventuell `tmix` | Lichtspuren, Nachziehen und zeitliche Mischung, P2 | Mittel; Zustand beim Seek/Loop zurücksetzen und definiert vorlaufen lassen | Im geprüften Build vorhanden; kein Ersatz für physikalische Band-/Decksimulation. |
| RetroArch `film/film-grain.slangp` | Luminanzabhängiges Filmkorn, P2 | Klein bis mittel; nutzt `FrameCount`, keine Bildhistorie. Als kombinierbare zusätzliche Stufe planen, alle sieben CRT-Presets erhalten | Im bereits gepinnten Shaderstand vorhanden. Shader nennt CC-BY-3.0 und Urheberhinweise; einschließlich Perlin-Anteil erfassen. |
| frei0r `Baltan` | Deutliche Mehrfachbilder/Bewegungsspuren, P3; kleiner Test eines Plugin-Hosts | Mittel; 32 Framepuffer, bei 1080p etwa 253 MiB nur dafür. Zählt verarbeitete Frames und ignoriert Zeitargument; Neuinstanz/Vorlauf nötig | Geprüfte Datei GPL-3.0-or-later. Upstream bietet Win64-DLLs; unser FFmpeg-Essentials-Build hat keinen frei0r-Host. Eigener Host oder bewusst anderer Build nötig. |
| frei0r `Glitch0r` | Zusätzlicher digitaler Glitchlook, P3, zurückstellen | Mittel bis hoch für reproduzierbaren Betrieb: globaler RNG, Seed aus Uhrzeit; parallele Instanzen und Export stimmen ungeändert nicht verlässlich überein | Geprüfte Datei GPL-2.0-or-later. Kein schneller Ersatz für NTSCRTs Empfängersimulation. |
| G'MIC/libgmic `quantize`, zunächst feste gleichmäßige Stufen | Grafische Posterlooks, P3 | Mittel bis hoch; neue C++-Anbindung und planare Floatpuffer. Instanz wiederverwenden; keine neue Instanz je Frame, keine pro Frame wechselnde adaptive Palette | Header nennt CeCILL-C oder CeCILL-2.1; gewählte Filter/Skripte und Daten separat erfassen. Windows/Linux-Angebot vorhanden, von uns nicht integriert. |

Quellen: [FFmpeg-Filter](https://ffmpeg.org/ffmpeg-filters.html#chromashift), [lagfun](https://ffmpeg.org/ffmpeg-filters.html#lagfun), [frei0r-Konfiguration in FFmpeg](https://ffmpeg.org/ffmpeg-filters.html#frei0r), [FFmpeg-Filterdatei](https://github.com/FFmpeg/FFmpeg/blob/master/libavfilter/vf_chromashift.c), [frei0r Windows](https://github.com/dyne/frei0r#ms--windows), [Baltan](https://github.com/dyne/frei0r/blob/6156f2ed78d7b97ad9e8c7cc79b318795d4ea8c8/src/filter/baltan/baltan.cpp), [Glitch0r](https://github.com/dyne/frei0r/blob/6156f2ed78d7b97ad9e8c7cc79b318795d4ea8c8/src/filter/glitch0r/glitch0r.c), [gepinntes Filmkorn](https://github.com/libretro/slang-shaders/blob/cb01f2f238700eed7ec1c859be1737a9fbcdcb7c/film/shaders/film-grain.slang), [libgmic-Schnittstelle](https://gmic.eu/libgmic.html), [G'MIC quantize](https://gmic.eu/reference/quantize.html), [G'MIC-Lizenzhinweise im Header](https://github.com/GreycLab/gmic/blob/master/src/gmic.h).

Die beiden frei0r-Dateien wurden auf Commit `6156f2ed78d7b97ad9e8c7cc79b318795d4ea8c8` untersucht. Für eine Umsetzung auch die übrigen bislang unfixierten Kandidaten konkret pinnen und die tatsächlichen Fähigkeiten der gewählten Installation abfragen. Diese Vorauswahl ist keine getestete Integration oder abschließende Prüfung aller mitgelieferten Dateien.

## Architekturgrenzen unserer heutigen App

| Stelle | Befund | Bedeutung |
| --- | --- | --- |
| `src/Ntscrt.Windows/EffectPipeline.cs:4` / `:12` | Feste Kette NTSC → WPF-Resize → CRT → Ausgabekomposition; Kontext trägt Framenummer, keine explizite Zeit-/Historienverwaltung | Vor neuen zeitlichen Stufen einen gemeinsamen Verarbeitungsvertrag festlegen. |
| `src/Ntscrt.Windows/PixelFrame.cs:8` | BGRA8-Puffer; WPF-Lesen, Skalieren, Anzeigen und PNG im selben Typ; Stride implizit | Plattformneutrale Pixel-/Farbbeschreibung und Besitzregeln von UI/Dateicode trennen; NTSCRT erwartet RGBA8. |
| `src/Ntscrt.Windows/CrtProcessor.cs:24` / `:62` | Vortice/D3D11; Upload, Zieltextur und Readback; Alpha wird undurchsichtig | Kein direkter Linux-Pfad. Pufferübergaben zur wgpu-Engine zunächst einfach halten, danach messen. |
| `src/Ntscrt.Windows/MainWindow.Video.cs:25` / `:143` | Vorschau kann Frames auslassen; CRT-Kette bleibt über Sprünge/Loops erhalten | Neue Historieneffekte brauchen definiertes Fortschreiben, Reset und/oder Vorlauf. |
| `src/Ntscrt.Windows/VideoExporter.cs:24` / `:40` | Frische Kette, eingefrorene Einstellungen, jedes Frame | Gute Trennung von Vorschau/Export, aber noch kein Nachweis gleicher Historie bei zusätzlichen zeitlichen Effekten. |
| `src/Ntscrt.Windows/VideoSource.cs:42` / `:74` | FFmpeg-Prozess und begrenzter Cache; normalisierte konstante Framerate, Rücksprung kann Dekodieren von Anfang verlangen | Gemeinsame Medienbasis nutzbar, Besucher für frühere Frames/Seekstrategie und Zeitmodell ergänzen. |
| `native/src/lib.rs:42` / `src/Ntscrt.Windows/NtscProcessor.cs:9` | Kleiner Rust-C-Host für ntsc-rs; DLL-Name und Host-API 1 | Erweiterbare Grenze, aber kein heutiger Digital-/Engine-Host. `.so`/`.dll`, Lebensdauer und API-Versionen ausdrücklich behandeln. |
| `src/Ntscrt.Windows/Ntscrt.Windows.csproj:4` | `net10.0-windows`, WPF, Vortice | Neue Oberfläche und Linux-Grafikadapter nötig; .NET allein macht die App nicht portabel. |

Die Zeit-/Historiengrenze ist ein Befund zum Ausbau. Sie beweist **nicht** die Ursache des früheren VirtualBox-/CRTsim-Flackerns. Dieses Thema bleibt entsprechend Nutzerentscheidung zurückgestellt.

## Empfohlene gemeinsame Grundlage

Für den langfristigen Kern NTSCRTs Rust-/wgpu-Engine erproben, anfangs hinter unserer bestehenden Windows-Oberfläche. Unsere FFmpeg-Medienanbindung und Ausgabegeometrie weiterverwenden, soweit der Plattform- und Bildvergleich das bestätigt. Digitalvideo zuerst als kleiner CPU-Nachweis; die vollständige Engine danach als gesonderter GPU-Nachweis. Ein kompletter Engine-Aufruf darf NTSC/CRT nicht zusätzlich nochmals durch unsere alte Kette schicken.

Benötigter Vertrag:

- Frame: Maße, Stride, BGRA/RGBA, Alpha, Farbraum/Transfer/Range, Eigentümer und Lebensdauer.
- Kontext: rationale Zeitbasis/Framerate, Frameindex, feste Zufallssteuerung, unveränderlicher Parametersnapshot und Historienkennung.
- Stufe: Fähigkeiten und Parameter, Herkunft/Version/Lizenz, Bypass, Zustand, Reset, benötigte Quellhistorie/Vorlauf, Fehler und Abbruch.
- Vorschau und Export: gleiche Reihenfolge und Auswertung; Vorschau darf Anzeige auslassen, aber den Effektzustand nicht unkontrolliert überspringen.
- Looks: Schemaversion und Prozessorversionen; Bestandsformat migrieren. Fehlende Stufen und unvollständigen Import ausdrücklich melden.

**Versionsrisiko:** NTSCRT verwendet librashader `76462c030b75c4f2d56e5386c3d4d7d1128318b8` mit lokalem Push-Constant-Patch und wgpu 28. Unser Referenzstand ist librashader 0.12.0 mit D3D11. Für einen nachvollziehbaren Vergleich zunächst NTSCRTs Pin/Patch erhalten; Versionsmodernisierung separat bewerten. Kein stiller Austausch der bestehenden ausgelieferten Bibliothek. [Pin/Patch](https://github.com/finnmckenty/NTSCRT/blob/cfde8457e34eee55ab6900655efaaba8b2e59c18/port/Cargo.toml#L11)

wgpu bietet grundsätzlich Vulkan/OpenGL für Linux; das ist ein Backendangebot, kein Nachweis, dass diese gesamte Effektkette auf unserer späteren Zielhardware läuft. WPF läuft nur auf Windows. Avalonia unterstützt Windows/Linux, bleibt aber eine spätere Oberflächenentscheidung nach erfolgreicher Kernprobe. [wgpu 28](https://docs.rs/wgpu/28.0.0/wgpu/), [WPF](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/), [Avalonia-Plattformen](https://docs.avaloniaui.net/docs/supported-platforms)

## Nachweise für Etappe 3

1. Synthetisches Testbild und kurzer Testclip mit festen Einstellungen; Digitalvideo aus/an für DCT und Video 1. Pixelkanäle, Maße, Alpha und Farbkonvertierung prüfen.
2. Derselbe Frame nach fortlaufender Verarbeitung, Vorwärtssprung, Rücksprung, Loop und Neuinstanz. Vorhersage, Paketverlust und Datamosh brauchen echte frühere Quellframes; Current-frame-Wiederholung ist kein gültiger Seekersatz.
3. Separate Vorschau-/Exportinstanzen vergleichen, auch bei ausgelassenen Anzeigeframes und geänderten Einstellungen. Sechs strukturierende Codecparameter sind bei NTSCRT pro Render fest, keine beliebig animierbaren Werte.
4. Alle sieben CRT-Presets und bestehende Tape-&-Tube-Looks erhalten; Referenzbilder mit dokumentierter Toleranz vergleichen. Upstream-Testquellen übernehmen nur mit ihren Hinweisen; tatsächliche Ausführung/GPU-Überspringen erfassen.
5. Native Windows- und Linux-Probe: Bild → NTSC/CRT → PNG sowie Video → gleiche Kette plus Digitalvideo → MP4 mit synchronem Ton. Laufzeit pro Stufe, Transfers und Speicher messen; Hardware und Backend benennen.
6. Danach Architekturentscheidung dokumentieren: Engine übernehmen/adaptieren, Medienadapter und UI auswählen; erst anschließend Veröffentlichung planen.

Vorhandene Tests als Ausgangspunkt: [Digitalvideo](https://github.com/finnmckenty/NTSCRT/blob/cfde8457e34eee55ab6900655efaaba8b2e59c18/port/engine/tests/digital.rs), [Glitch-Parität](https://github.com/finnmckenty/NTSCRT/blob/cfde8457e34eee55ab6900655efaaba8b2e59c18/port/engine/tests/glitch_parity.rs), [Screen Loop](https://github.com/finnmckenty/NTSCRT/blob/cfde8457e34eee55ab6900655efaaba8b2e59c18/port/engine/tests/howl.rs), [Windows-CI-Konfiguration](https://github.com/finnmckenty/NTSCRT/blob/cfde8457e34eee55ab6900655efaaba8b2e59c18/.github/workflows/windows-port.yml).

**Ergebnis der Analyse:** Die wesentlichen zusätzlichen Effekte sind inzwischen als wiederverwendbarer Rust-Code vorhanden. Das macht gezielte Übernahme aussichtsreicher als unabhängigen Nachbau. Die eigentliche Integrationsarbeit liegt in Zeit-/Historiensteuerung, GPU-/Pixelübergängen, Medienadaptern, Presets und Bedienung. Bild- und Leistungsparität sowie Linuxfähigkeit sind offene praktische Nachweise.
