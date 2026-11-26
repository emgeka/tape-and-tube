# Tape & Tube — Entwicklungsplan in sechs Etappen

Stand: 20.11.2026, nach Veröffentlichung von 0.1.0-beta.2. Ziel ist zunächst Funktionsparität mit NTSCRT auf Windows. Darauf bauen eigene Erweiterungen und eine Linux-Ausgabe auf. Herkunft und ursprüngliche Lizenzen bleiben sichtbar; externe Komponenten bleiben gezielt austauschbar.

## Unabhängigkeit von NTSCRT

Nutzervorgabe vom 24.11.2026: Tape & Tube so weiterentwickeln, dass die Arbeit auch bei einer plötzlichen Einstellung von NTSCRT fortgesetzt werden kann.

- Die verwendeten Quellmodule, Presets, Originalhinweise, Revision und unsere Patches im eigenen Projekt oder in einem von uns kontrollierten Quellpaket dauerhaft verfügbar halten. Der Umfang beschränkt sich auf die tatsächlich verwendeten Bestandteile.
- Installation, Ausführung und regulärer Build sollen ohne Zugriff auf das NTSCRT-Repository funktionieren. Neue NTSCRT-Versionen gezielt prüfen und übernehmen. Fehlerbehebungen am eingebundenen Stand können lokal erfolgen.
- Stand vom 24.11.2026: Die 94 verwendeten NTSCRT-Dateien liegen bytegleich unter `prototypes/processing-core/vendor/NTSCRT-0.28.1/`. Windows-Build, Preset-Paketierung und Linux-Testskript greifen auf diese Kopie zu. Der GitHub-Quellentwurf enthält sie einschließlich Shaderquellen und Lizenztexten. In einem frischen lokalen Git-Checkout wurde der Windows-Effektkern ohne das NTSCRT-Vollarchiv und ohne Netzwerk gebaut. Beide Patches wurden angewandt und ließen sich erneut prüfen. Mit dem neu gebauten Kern bestanden 45 Export- und 193 Integrationsprüfungen. Für Drittkomponenten wie librashader und Cargo-Abhängigkeiten wurden vorhandene lokale Caches verwendet. Ein nativer Linux-Lauf der geänderten Vorbereitung steht noch aus.

## Schlankerer Windows-Installer

Nutzeridee vom 25.11.2026: **Laufzeitumgebungen bei der Installation prüfen und bei Bedarf nachinstallieren, um die Downloadgröße zu reduzieren.**

- Vorschlag: Tape & Tube für den regulären Installer mit gemeinsam installierter .NET-Laufzeit bauen. Auf der vorhandenen Komponentenseite die passende **.NET 10 Desktop Runtime (x64)** erkennen, vorhandene kompatible Versionen nutzen und bei Bedarf den offiziellen Microsoft-Installer anbieten. Nach dessen Abschluss erneut prüfen und die gemeinsam genutzte Laufzeit bei einer Tape-&-Tube-Deinstallation erhalten. [Microsoft zur .NET-Desktoplaufzeit](https://learn.microsoft.com/dotnet/core/install/windows)
- Beta.4 enthält rund 62 MiB komprimierte .NET-/WPF-Dateien im portablen ZIP. Die tatsächliche Ersparnis beim Setup anhand eines Vergleichsbuilds messen. Erstinstallation mit vorhandener, fehlender oder unpassender Laufzeit, Abbruch sowie Upgrade von Beta.4 prüfen. Eine portable Ausgabe mit integrierter Laufzeit bleibt eine Option für Offline-Nutzung.
- Vergleich vom 26.11.2026 mit identischem Inno-Setup-Rezept. Das lokale Setup mit mitgelieferter Laufzeit misst 63,6 MiB. Die Messfassung mit extern bereitgestellter .NET-Desktoplaufzeit misst 21,0 MiB, rund 67 % weniger. Die App der Messfassung startete mit der installierten x64-Desktoplaufzeit 10.0.15 und bestand 16 Effektprüfungen. Die Messfassung enthält noch keine .NET-Erkennung oder Nachinstallation im Setup.

## Aktuelle Priorität: NTSCRT-Funktionen zuerst

Der Nutzer hat am 20.11.2026 die Reihenfolge festgelegt. Wir schließen zuerst die Lücken zu NTSCRT für Windows und verschieben eigenständige Features auf die Zeit danach. Fehlerbehebungen sowie gemessene Leistungsoptimierungen bleiben Teil der Arbeit, wenn sie die Nutzung der übernommenen Funktionen beeinträchtigen. Der bereits umgesetzte HDR-Import ist abgeschlossen und löst keinen weiteren HDR-Ausbau in dieser Phase aus.

1. **VCR und TV-Glitch.** Im lokalen Windows-Testbuild integriert. Band, Deck und Empfang sind bedienbar. Vorschau und Export sowie Positionssprünge sind automatisiert geprüft. Der Praxistest mit eigenem Material steht aus.
2. **Screen Loop.** Im lokalen Windows-Testbuild integriert. Die Rückkopplung verwendet Finns Vorlauf, alle 23 Einstellungen und fünf Original-Presets. Vorschau und MP4-Export unterstützen Video und Standbilder. Die Bedienung der Griffe und der MP4-Export mit einem Standbild aus dem Referenzclip sind unter Windows geprüft.
3. **Animation und Looks.** Der lokale Windows-Build lädt NTSCRT-Looks einschließlich Keyframes für Vorschau und Export. Der Reiter **Animation** erstellt, bearbeitet und speichert eigene Effekt-Keyframes für Videos und Standbilder. Die Übergänge werden in Vorschau und Export ausgewertet. Importierte Looks bleiben schreibgeschützt und können als NTSCRT-JSON gespeichert werden.
4. **Weitere Windows-Lücken.** Die sechs Digitalvideo-Formate, der Quellbeschnitt und der GIF-Export sind im lokalen Windows-Build zugänglich. Quellbeschnitt gilt auch für NTSCRT-Looks. Standbilder lassen sich mit einstellbarer Länge und Bildrate auch ohne Keyframes exportieren. Die Kommandozeile rendert PNG, MP4 und GIF mit eigenen und NTSCRT-Presets. Ein lokaler MCP-Server stellt dieselbe Formatabfrage und Renderfunktion bereit. ProRes ist laut NTSCRT-Dokumentation auf Windows ausgenommen. Maßstab ist die [NTSCRT-Dokumentation für Windows](https://github.com/finnmckenty/NTSCRT#download).

Diese Reihenfolge stellt die größte sichtbare Effektlücke an den Anfang. Funktionsparität heißt, dass die übernommenen Funktionen im normalen Arbeitsablauf bedienbar sind und in Vorschau, Positionssprung und Export wie vorgesehen funktionieren. Für einen ersten Testbuild muss nur der jeweils integrierte Bereich nutzbar und geprüft sein.

### Offene Exportfunktionen unter Windows

- **H.265/HEVC** ist im lokalen Windows-Build im Speicherdialog, in der Kommandozeile und über MCP auswählbar. H.264 bleibt Standard. Beide MP4-Varianten übernehmen die erste Tonspur als AAC. HEVC ist mit dem FFmpeg-Build des Installers geprüft.
- **MP4-Qualität** ist im Bereich Ausgabe, in der Kommandozeile und über MCP in vier Stufen wählbar. Hoch entspricht der bisherigen Voreinstellung.
- **MP4-Durchläufe** wiederholen Bild und Ton einer Videoquelle bis zu 50-mal innerhalb derselben Datei. Die Einstellung ist in der App, in der Kommandozeile und über MCP verfügbar.
- **Scanline-Raster** kann in der App, in der Kommandozeile und über MCP die Ausgabemaße an ganze Pixelzeilen pro Scanline anpassen. Quellbeschnitt und Bildanpassung werden berücksichtigt.
- **Vorschau-Zoom und Verschieben** sind per Regler, Mausrad und Ziehen verfügbar. Der Vorher-nachher-Vergleich folgt derselben Ansicht. Der Export behält seine Ausgabemaße.
- **Ganzzahlskalierung** richtet die Vorschau an ganzen physischen Bildschirmzeilen pro Scanline aus. Zoom, Verschieben und Vorher-nachher-Vergleich verwenden dieselbe Darstellung.
- **GIF-Breite und -Bildrate** sind in der App, in der Kommandozeile und über MCP getrennt wählbar. Leere Felder verwenden dieselbe Automatik wie zuvor.
- **GIF-Dateigrößenschätzung** berücksichtigt Ausgabemaße, Bildrate und Dauer. Über geschätzten 10 MB erscheint ein Hinweis im Ausgabebereich.
- **Vorberechnete Videovorschau** puffert im lokalen Windows-Build während der Pause bis zu zwölf Folgebilder mit den aktuellen Einstellungen. Der Speicher für Quell- und Effektbilder ist auf 96 MiB begrenzt. Positionssprünge und Look-Änderungen leeren den Puffer.
- **Screen-Loop-Griffe** setzen den Fluchtpunkt und sein Bewegungsziel direkt im bearbeiteten Vorschaubild. Die Positionen fließen in dieselben Regler und Presets ein.
- **Laufende Störeffekte** lassen sich in der Standbild-Vorschau ohne aktive Animation einschalten. Ein PNG speichert das beim Klick angezeigte Bild.
- **Skalierungsmethode** bietet die sechs Filter des gemeinsamen Kerns für eigene Looks. Die Wahl wird im Look-Preset gespeichert und gilt für Vorschau und Export.
- **Interne Verkleinerung** ist separat ein- und ausschaltbar. Automatisch behält die vorherige Verarbeitungslogik bei. Eigene Looks speichern die Einstellung.
- **Effektparameter-Katalog für CLI und MCP** beschreibt die sieben Stufen einschließlich Standardwerten, Bereichen und verfügbaren Presets. CRT-Parameter lassen sich nach Shader filtern.
- **Direkte Effektparameter für CLI und MCP** lassen sich pro Renderauftrag mit oder ohne Tape-&-Tube- beziehungsweise Screen-Loop-Preset setzen. Importierte NTSCRT-Looks bleiben schreibgeschützt.
- **Probelauf für CLI und MCP** löst Einstellungen, Ausgabegeometrie und Zeitbasis ohne Rendern oder Schreiben auf.
- **Mehrere PNG-Positionen** lassen sich in einem CLI- oder MCP-Aufruf exportieren. Der Probelauf zeigt die aufgelösten Frame-Nummern und Dateinamen.
- **PNG-Nahaufnahmen** sind für einzelne Frames und Serien per CLI und MCP verfügbar. Der Probelauf zeigt die genaue Position und Größe des Ausschnitts.
- **Windows-Sichtprüfung abgeschlossen.** Der Nutzer hat den Test mit dem früheren CRTsim-Problemclip am 24.11.2026 bestätigt. Die [Abnahmeliste](WINDOWS_ACCEPTANCE.md) dokumentiert den Sprachwechsel, Screen Loop und den animierten NTSCRT-Look. Der Test des nächsten Beta-Installers in einer frischen Windows-VM steht aus.

### Digitalvideo-Abgleich vom 23.11.2026

Die Windows-Vergleichsversion und der gemeinsame Effektkern im regulären Entwicklungsstand basieren auf [NTSCRT 0.28.1](https://github.com/finnmckenty/NTSCRT/releases/tag/v0.28.1) (Commit `0ab60bf952b09a0c41ea4290979f23dadd963614`). Der Vergleich unten verwendet als Ausgangspunkt den bisherigen Effektkern `cfde8457e34eee55ab6900655efaaba8b2e59c18`.

- Der Digitalvideo-Katalog wächst von 20 auf 32 Parameter. Hinzu kommen unter anderem Einchip-Sensoren, vertikales Verschmieren, Blockgröße, Bitratenbudget mit Puffer, mehrere Kompressionsgenerationen, Verlustmuster und Bewegungs-Datamosh. Die bestehenden Feldmodi werden um Woven und Blended ergänzt. Unsere Verarbeitung für ein einzelnes Halbbild muss bei diesen beiden Modi ausbleiben. Sensor und Blockgröße gelten wie Codec und Bildrate für den ganzen Clip.
- Isolierter Windows-Codecvergleich mit 18 identischen synthetischen Frames pro Fall und gespeicherten Tape-&-Tube-Werten: Webcam, MiniDV und Video Call ändern sich mit dem neuen Kern. Early Web, CD-ROM und Datamosh sind auf diesem Material pixelgleich. Ein Verlauf von CCD Response 0 bis 1 ändert 16 von 18 Frames. Der Vergleich prüft den Codec ohne CRT, Vorschauoberfläche oder MP4-Kodierung.
- Der neue NTSCRT-Parser schreibt Look-Version 3 und migriert ältere Bandverschleißwerte sowohl in den Basiswerten als auch in Keyframes. Tape & Tube akzeptiert NTSCRT-Looks der Versionen 1 bis 3. Der Host-Patch für die Grafikeinbindung ist an den neuen Quellstand angepasst.
- **Entscheidung vom 23.11.2026:** Der Nutzer priorisiert Funktionen und den aktuellen NTSCRT-Stand. Für bisherige Tape-&-Tube-Presets wird kein zweiter Renderer vorgehalten. Der Windows-Testbuild verwendet den neuen Kern samt Import für NTSCRT-Look-Version 3. Die gestalterischen Unterschiede aus dem Codec-Vergleich sind für den Wechsel dokumentiert.
- **Separater Windows-Testbuild:** Alle sechs Digitalvideo-Formate und 32 Regler sind zum Test verfügbar. Alle 44 NTSCRT-Look-Dateien erreichten den Renderer. 80 Prüfungen bestanden auf der RTX 5060 Ti mit Vulkan, darunter Positionssprünge, MP4 mit Ton, eigene Digitalvideo-Keyframes, Woven und Screen Loop. Der animierte Aperture-Look konnte bei 144 × 96 schwarz rendern. Der korrigierte Testbuild berechnet CRT bei solchen Zielgrößen größer und verkleinert anschließend. Vorschau und die dekodierte MP4-Datei zeigten bei 144 × 96 ein sichtbares erstes Bild.

### NTSCRT-Kern 0.29.1 vom 25.11.2026

Der lokale Windows-Testbuild `20261125-101325-401` verwendet die mitgeführten Quellen von [NTSCRT 0.29.1](https://github.com/finnmckenty/NTSCRT/releases/tag/v0.29.1), Revision `d5929bf4a819c617b6c6575ffbe9a477a1692ad0`. Der librashader-Patch und unser Host-Patch wurden beim Offline-Build berücksichtigt. Digitalvideo und sämtliche 49 Original-Preset-Dateien sind bytegleich zum vorigen Stand. Der Host akzeptiert das NTSCRT-Look-Format 4. Eigene Keyframes werden mit der aktuellen VCR-Semantik an den Kern übergeben. Die aktualisierten VCR-Hilfetexte sind in allen zehn Sprachen enthalten. 194 Integrations-, 45 Export- und 121 Sprachprüfungen bestanden.

Im Vergleich desselben synthetischen Clips nach MP4-Dekodierung änderten sich bei **Oft abgespielte VHS** alle zwölf Frames. **Antennen-TV mit Geisterbild** war für diesen Clip bildgleich. Die 0.29.1-Änderungen an der NTSCRT-Oberfläche, darunter Vorschauton, sind ein gesonderter Aufgabenpunkt für Tape & Tube.

### NTSCRT 0.29.2 vom 25.11.2026

Am 26.11.2026 gegen den lokalen Pin 0.29.1 geprüft. Das Release vereinheitlicht die interne CRT-Rendergröße für Vorschau und Export und behebt laut NTSCRT bei großen Quellen das schwarze oder streifige Bild bei CRT Aperture und ausgeschaltetem Downscale. Im Vergleich liegen Änderungen im Rust-Effektkern und in Screen Loop, bei unveränderten Preset- und Codec-Dateien. Unsere Host-Anpassung für die frühere CRT-Zwischengröße muss beim Upgrade mit der neuen Rendergrößenlogik abgeglichen werden. Für den Vergleich CRT Aperture mit deaktiviertem Downscale, CRTsim bei Hochformat und kleine Exportmaße verwenden. Die im NTSCRT-Release genannte Verlangsamung der Videoexporte ist für Tape & Tube noch zu messen. [NTSCRT-Release](https://github.com/finnmckenty/NTSCRT/releases/tag/v0.29.2) · [Änderungen gegenüber 0.29.1](https://github.com/finnmckenty/NTSCRT/compare/v0.29.1...v0.29.2)

Getrennter Windows-Vergleich am 26.11.2026 mit Revision `889256dde173a89e475919be3d1bba34e6cc7355`. Die für den Effektkern geänderten Dateien wurden anhand ihrer Git-Blob-Hashes geprüft. Der Vergleichskern behält die Grafik- und Zustandsprüfungen des Tape-&-Tube-Hosts. Die CRT-Zwischengröße berechnet 0.29.2 selbst.

- **CRT Aperture ohne interne Verkleinerung bei 1904 × 2544.** 0.29.1 erzeugte Schwarz mit mittlerem Y=16. 0.29.2 erzeugte ein sichtbares Bild mit mittlerem Y=105,7. Der kleine Export mit 144 × 96 Pixeln blieb bei beiden sichtbar.
- **CRTsim-Hochkantclip.** Im lokalen Ausschnitt um Sekunde 14–18 enthielten beide Ergebnisse 119 dekodierbare Frames, keine nahezu schwarzen Bilder und Bild und Ton mit gleicher Laufzeit. Bei 960 × 1708 Pixeln benötigten zwei abwechselnde Läufe mit 0.29.1 im Mittel 7,85 s und mit 0.29.2 im Mittel 6,49 s. Die Messung gilt für diesen Clip und diese Einstellungen.
- Die beiden neuen CRT-Tests von NTSCRT, 52 Ausgabe-, 79 Export- und 216 Integrationsprüfungen liefen mit dem getrennten Kern erfolgreich. Die Messergebnisse liegen lokal unter `artifacts/upstream-review/ntscrt-0.29.2`.
- Am 26.11.2026 in den regulären Windows-Build `20261126-095514-162` übernommen. Das Originalarchiv stimmt mit der GitHub-Revision überein. Von 94 mitgeführten Dateien unterscheiden sich vier Engine-Dateien. Der Host-Patch enthält die Adapterprüfung und den Zustandsreset. Im regulären Build bestanden 53 Ausgabe-, 79 Export-, 216 Integrations- und 433 ABI-Prüfungen. Der CRTsim-Hochkant-Ausschnitt wurde mit 119 Frames und durchgehender Tonspur ohne nahezu schwarze Frames dekodiert.

### Vorschauton

Stand vom 25.11.2026: Unter der Vorschau kann **Vorschauton** für Videos mit Tonspur eingeschaltet werden. Die Wiedergabe verwendet die erste Tonspur. FFmpeg liefert die Audiodaten auf der normalisierten Videozeitleiste. Die Windows-Audiouhr steuert das Bildtempo und wartet bei Rückstand der Vorschau. Der Ton ist beim App-Start ausgeschaltet.

- Pause beendet die Audiowiedergabe. Fortsetzen, Loop und Basic-Look-Wechsel starten den Ton am Bild, an dem die Vorschau weiterläuft.
- Korrektur vom 26.11.2026: Effektwechsel in Expert, etwa Bildschirm, Filmaufnahme und Screen Loop, halten die Wiedergabe am aktuellen Bild, bis der neue Effekt berechnet ist. Bei zuvor laufendem Video starten Bild und Ton anschließend automatisch wieder.
- Geprüft sind Audio mit Quell-Zeitversatz, positionsgenaues Springen, wechselnde Bildrate, Quellen ohne Tonspur und ein Test der Windows-Audiouhr bei künstlich verzögerter Bildanzeige. Der bisherige Export mit Originalton bleibt Teil der Exportprüfung.
- Danach den Windows-Beta-Stand abnehmen und über den schlankeren Installer entscheiden. Die Linux-Desktop-Ausgabe bleibt ein eigener Planungsschritt.

**Bereits im Windows-Testbuild:** Basic und Expert, die ersten frühen Epochen-Looks und die Side-by-side-Vorschau samt Export. Weitere Röhren- und Empfangsprofile, manuelles Drehen und ein möglicher HDR-Ausbau sind weiter unten dokumentiert.

Dieser Plan unterscheidet Analyse, Prototyp und Veröffentlichung. Die Fortsetzung der Planung ist kein Auftrag, alle folgenden Codeänderungen auszuführen. Etappe 3 wurde anschließend ausdrücklich beauftragt. Die Bestandsaufnahme für Etappe 2 ist abgeschlossen; der getrennte Kern-Prototyp funktioniert unter Windows und nativ unter Linux Mint mit Mesa-Software-Rendering. Die erste Worker-Architektur ist entschieden und in Windows angebunden; gemeinsame Oberfläche und weitere Freigaben bleiben offen.

Fortsetzung vom 17.11.2026: Die [Kernentscheidung und Umsetzungsvorlage](CORE_INTEGRATION_PLAN.md) wurde ausdrücklich zur Umsetzung beauftragt. Die [erste lokale Windows-Integration](DIGITAL_VIDEO_INTEGRATION.md) verbindet den dauerhaften Rust-/wgpu-Worker mit Vorschau und Export und ergänzt Digitalvideo sowie Webcam/MiniDV/CD-ROM-Gesamtlooks. Bestehende Presets behalten ihren Renderer. Die öffentliche Beta.2 bleibt unverändert. Basic/Expert, historische Presets und Side-by-side bleiben vorgemerkte Features.

| Etappe | Ziel | Stand und Abschlusskriterium |
| --- | --- | --- |
| 1. Beta.2 abschließen | Installation mit C++/DirectX/FFmpeg vereinfachen und prüfen | **Abgeschlossen und veröffentlicht.** [0.1.0-beta.2](https://github.com/emgeka/tape-and-tube/releases/tag/v0.1.0-beta.2), einschließlich Installer, passendem Quellcode und Prüfsummen. Letzter vom Nutzer gemeldeter VM-Export ohne Flackern/schwarze Bilder; die Ursache des historischen CRTsim-Problems bleibt unbestätigt. |
| 2. Effektumfang und Grundlage vergleichen | Lücken zu Finns Windows-Version, geeignete Quellen, Lizenzen, Plattformen und Aufwand erfassen | **Quellcodeanalyse abgeschlossen.** Siehe [Funktions- und Architekturvergleich](NTSCRT_COMPARISON.md). Referenz ist NTSCRT v0.26.0; Laufzeit-, Bildqualitäts- und Geschwindigkeitsvergleich stehen noch aus. |
| 3. Gemeinsamen Verarbeitungskern erproben | Bild/Video → NTSC → CRT plus zusätzliche Stufe → PNG/Video mit Ton auf Windows und Linux | **Funktionsnachweise unter Windows und Linux erbracht.** Je 12 Rust-Tests, 433 externe C-ABI-Prüfungen und 29 Grafik-/Medienprüfungen erfolgreich. Alle sieben CRT-Presets, PNG und MP4/AAC geprüft. Linux Mint 22.3/VirtualBox benötigt im Test Mesa-Software-Rendering; der Standardtreiber unterstützt die erforderlichen Compute-Shader nicht. Direkter Bildvergleich mit identischen Eingaben ausgewertet. [Linux-Testbericht](LINUX_CORE_TEST.md), [Prototyp](../prototypes/processing-core/README.md). Dauerhafter Worker und erster Windows-Adapter sind jetzt lokal integriert; weitere Leistungstests und die gemeinsame Oberfläche folgen. |
| 4. Effektlücken schließen | Digitalvideo, VCR, Glitch und Screen Loop | **Digitalvideo, VCR, Glitch und Screen Loop lokal integriert.** |
| 5. Windows-Parität abschließen | Keyframes, NTSCRT-Looks und weitere Lücken in Bedienung, Export und Automatisierung | **In Arbeit.** Import und Auswertung der NTSCRT-Looks sowie das Erstellen und Bearbeiten eigener Keyframes sind lokal integriert. Die weiteren Windows-Lücken stehen oben. |
| 6. Eigene Erweiterungen und Linux-Ausgabe | Eigenständige Funktionen und native Linux-App | **Nach Windows-Parität.** Die Linux-Kernprobe ist vorhanden. Die Desktop-Ausgabe ist ein gesondertes Vorhaben. |

## Beauftragter Prototyp und nächster Nachweis

**Etappe 3 wurde mit einem isolierten Digitalvideo-Prototyp begonnen.** Der gemeinsame Ablauf ist nun unter Windows und nativ unter Linux Mint nachgewiesen, einschließlich Vergleich und Export. Dies ist noch keine vollständige neue App oder Linux-Beta. Der [Testbericht](LINUX_CORE_TEST.md) empfiehlt Rust/wgpu und FFmpeg als gemeinsame Grundlage; nächste Entscheidungen betreffen die Produktionsintegration, verbleibende Vertrags-/Leistungsfragen und anschließend die Oberfläche.

1. Den untersuchten NTSCRT-Quellstand fixieren; das MIT-lizenzierte `ntscrt-codec` gezielt hinter einer eigenen, versionierten Host-Schnittstelle anbinden. Originalhinweise und eigene GPL-3.0-only-Anteile getrennt erhalten.
2. Bildformat, Zeitbasis, Framenummer, Zufallssteuerung, Parametersnapshot und Instanzlebensdauer festlegen. Vorhandene Looks und die sieben CRT-Presets schützen.
3. Zunächst DCT mit unabhängig komprimierten Frames und Video 1 zeigen. Anschließend vorhergesagte Frames, Datenverlust und Datamosh mit vollständiger Historie/Vorlauf prüfen. Kompressions-Keyframes sind etwas anderes als Effekt-Keyframes auf einer Zeitleiste.
4. Dasselbe Material fortlaufend, nach Vor-/Rücksprung, nach Loop und im Export vergleichen. Für ausgelassene Vorschauframes muss der Effektzustand korrekt bleiben.
5. CPU-Zeit und Kopierkosten messen; Linux-CPU-Build prüfen. Danach die GPU-Probe mit NTSCRTs Engine, deren Referenzversionen und allen sieben CRT-Presets durchführen.

Der geforderte native Linux-Test für PNG und Video mit synchronem Ton wurde am 17.11.2026 erfolgreich durchgeführt. Die Grafikausführung verwendete llvmpipe und beweist keine Hardwarebeschleunigung oder fertige Linux-Desktopausgabe. Für die spätere Oberfläche bleibt Avalonia ein Kandidat; eine Entscheidung ist noch offen.

## Gesammelte Featureideen vom 17.11.2026

Feste Arbeitsregel: Neue Featureideen grundsätzlich hier dokumentieren, auch ohne gesonderten Vermerkauftrag. Ideen werden gesammelt; Anwendungscode wird erst nach ausdrücklichem Umsetzungsauftrag geändert.

Diese Ideen stammen vom Nutzer und sind für die weitere Planung festgehalten. Sie sind noch kein Umsetzungsauftrag; Umfang und Priorität werden vor Codeänderungen gemeinsam festgelegt. Die folgenden Ausgestaltungen sind Diskussionsvorschläge, noch keine zugesagten Funktionen oder historisch geprüften Gerätemodelle.

### Bildröhren über die Jahrzehnte

Nutzeridee: **Röhrenbildschirmsimulation über die Jahrzehnte der Bildschirmröhren hinweg.**

- Repräsentative Epochen- und Geräteprofile recherchieren; Fernseher, Computermonitor und Arcade-Bildschirm unterscheiden.
- Mögliche Merkmale: Röhrenform/Wölbung, Leuchtstoff, Maskenstruktur, Nachleuchten, Schärfe, Konvergenz und Bildstabilität.
- Epoche, Geräteart und Erhaltungszustand als mögliche verständliche Auswahl; Einzelparameter bleiben zugänglich.
- Signalquelle und Bildschirmprofil getrennt kombinierbar halten.
- Zunächst eine historische Geräte-/Merkmalsmatrix erstellen und die Abdeckung durch vorhandene Shader prüfen. Zuordnung: Effekte in Etappe 4, Profilbedienung in Etappe 5.

### Empfangsstörungen in Vergangenheit und Gegenwart

Nutzeridee: **Störsignale beim Rundfunkempfang und deren Auswirkungen auf die Bildqualität in Vergangenheit und Gegenwart.**

- Ergänzung des Nutzers vom 26.11.2026: **Neben NTSC auch PAL-spezifische Effekte nutzen.** Als ersten Ausgestaltungsvorschlag europäisches PAL mit 625 Zeilen und 50 Halbbildern pro Sekunde untersuchen. Ein Kandidat sind **Hanover Bars**, horizontale Farbmuster, die bei bestimmten Phasenfehlern und einfacher PAL-Dekodierung sichtbar werden. Die Wirkung mit Farbbalken und bewegtem Material prüfen. PAL-Farbdekodierung, Bandfehler und Röhrenbild sollen kombinierbare Stufen bleiben. [ITU-R zu analogen Fernsehsystemen](https://www.itu.int/rec/R-REC-BT.470-7-200502-I/en) · [BBC-Forschungsbericht zu PAL-Farbfehlern](https://downloads.bbc.co.uk/rd/pubs/reports/1984-20.pdf)
- Analoge und digitale Empfangsverfahren sowie ihre unterschiedlichen Fehlerbilder recherchieren und getrennt modellieren.
- Mögliche Profile: schwacher Analogempfang, Mehrwegeempfang/Geisterbilder, Gleichkanalstörungen, Impulsstörungen, Synchronisationsfehler sowie digitale Übertragungsfehler mit Blockbildung, eingefrorenen Bereichen und Aussetzern.
- Als weiterführenden Vorschlag eine zweite Bild-/Videoquelle als störenden Sender prüfen.
- Stärke, Häufigkeit und zeitlichen Verlauf steuerbar machen; spätere Verbindung mit Keyframes untersuchen.
- NTSCRTs vorhandene Empfänger-/Glitchsimulation zuerst auf tatsächliche Abdeckung prüfen. Zuordnung: Empfangseffekte in Etappe 4, Animation/Bedienung in Etappe 5.

### Epochen als stimmige Preset-Snapshots

Nutzeridee vom 17.11.2026: **Eine Vielzahl auswählbarer Presets, die stimmige „Snapshots“ von Bildeindrücken verschiedener Epochen abbilden.**

- Gewünscht ist eine umfangreiche Auswahl in sich stimmiger Gesamtlooks mit erkennbarem Epochenbezug.
- Präzisierung des Nutzers vom 17.11.2026: **Presets strukturiert ablegen und zugänglich machen, damit auch unerfahrene Nutzer historische Zustände schnell finden, aufrufen und anwenden können.** Der gewünschte Bildeindruck soll ohne Zusammenstellen einzelner Effekte und ohne Durcharbeiten vieler Einstellungen nutzbar sein. Dies ist ein zentrales Bedienziel und ein angestrebter eigenständiger Mehrwert gegenüber NTSCRT; ein tatsächlicher Vorsprung ist noch zu belegen.
- Ziel für den Einstieg: einen verständlich benannten historischen Gesamtlook auswählen, auf das eigene Medium anwenden und das Ergebnis direkt nutzen/exportieren. Technische Einzelparameter bleiben zur freiwilligen Vertiefung erreichbar.
- Ausgestaltungsvorschlag für die Struktur: stabile Preset-IDs und Metadaten für Epoche, Region/Fernsehnorm, Quelle/Übertragungsweg, Aufnahmegerät und Bildschirmprofil. Diese Merkmale intern ablegen; die Auswahloberfläche soll passende Kombinationen übersichtlich anbieten, ohne alle Merkmale als Pflichtentscheidungen abzufragen.
- Ausgestaltungsvorschlag für die Auswahl: wenige verständliche Hauptkategorien, aussagekräftige Vorschaubilder, kurze Beschreibungen, Suche/Filter und Favoriten. Konkrete Kategorien und Bedienabläufe erst gemeinsam festlegen.
- Vorgeschlagenes Abnahmekriterium: Ein Nutzer ohne Kenntnisse der Effektparameter findet einen gewünschten historischen Bildeindruck und wendet ihn an, ohne die erweiterten Einstellungen öffnen zu müssen. Vollständige Presets speichern alle nötigen Stufen und Werte; fehlende Komponenten oder eingeschränkte Reproduzierbarkeit werden verständlich gemeldet.
- Als Ausgestaltungsvorschlag Bildquelle, Übertragung/Empfang, Aufnahme/Wiedergabe und Bildschirm zu zusammenpassenden Momentaufnahmen kombinieren. Die beiden bisherigen Featureideen fließen damit in vollständige Looks ein.
- Als Bedienvorschlag nach Epoche und Anwendungssituation browsen sowie aussagekräftige Vorschaubilder und kurze Beschreibungen anbieten.
- Historische Zuordnungen anhand geeigneter Referenzen prüfen; recherchierte Rekonstruktionen und frei interpretierte Looks erkennbar unterscheiden.
- Vorhandene Presets erhalten und neue Looks weiter anpassbar halten. Zuordnungsvorschlag: Effektgrundlagen in Etappe 4, Auswahl und Presetgestaltung in Etappe 5. Umfang und konkrete Presetliste sind noch offen.

Gemeinsames Gestaltungskonzept: **Bildquelle → Übertragung und Empfang → Aufnahme/Wiedergabe → Bildschirm einer gewählten Epoche.**

Ergänzung des Nutzers vom 25.11.2026: **Frühe Retro-Epochen bei den eigenen Looks mitpriorisieren. Gerade die Anfänge des Fernsehens bieten Potenzial für eigenständige Neuerungen gegenüber NTSCRT.** Sie gehören von Anfang an in die Planung für **Basic → Looks**.

Ergänzung des Nutzers vom 26.11.2026: **Auch Effekte aus der Zeit vor dem Röhrenfernsehen vorsehen. Das Kinetoskop und weitere Bildeindrücke dieser Epoche sind Kandidaten.** Ausgestaltungsvorschlag für **Basic → Looks**: beim Kinetoskop die Wirkung des kurzen Filmstreifens und der Betrachtung durch ein Guckloch anhand historischer Vorbilder erproben. Als weitere Kandidaten das Mutoskop und frühe Kinoprojektion untersuchen. [Library of Congress zum Kinetoskop](https://www.loc.gov/collections/edison-company-motion-pictures-and-sound-recordings/articles-and-essays/history-of-edison-motion-pictures/origins-of-motion-pictures/) · [Library of Congress zu Mutoskop und frühen Projektoren](https://www.loc.gov/collections/edison-company-motion-pictures-and-sound-recordings/articles-and-essays/history-of-edison-motion-pictures/shift-to-projectors-and-the-vitoscope/)

Ausgestaltungsvorschläge für drei unterschiedliche Bildwelten:

- **Mechanisches Fernsehen um 1930.** Bairds Televisor als Vorbild für ein grobes, flackerndes 30-Zeilen-Bild. Zeilenaufbau und Helligkeitsverlauf als eigene Simulation untersuchen. [Science Museum Group zum Baird-Televisor](https://collection.sciencemuseumgroup.org.uk/objects/co8405159/televisor-made-by-baird-international-television-ltd-black-and-white-television)
- **Frühes elektronisches Fernsehen ab 1936.** Britisches 405-Zeilen-Fernsehen als Vorbild für einen monochromen Röhrenlook. Zeilenraster, Bildgeometrie und Signalweichheit anhand konkreter Geräte und Aufnahmen abstimmen. [Science Museum Group zum Baird T5](https://collection.sciencemuseumgroup.org.uk/objects/co8559261/baird-t5-television-receiver)
- **Fernsehen auf Film, späte 1940er und 1950er.** Kineskop-Aufnahmen als Vorbild für ein vom Monitor abgefilmtes Fernsehbild. Die Wirkung der Röhre und den Charakter der Filmkopie getrennt untersuchen und für den Look kombinieren. [Library of Congress zur Fernseharchivierung](https://www.loc.gov/static/programs/national-film-preservation-board/documents/tvstudy.pdf)

Vorschlag für den ersten eigenständigen Prototyp: das mechanische 30-Zeilen-Bild an einem Standbild und einem Bewegungsclip erproben. Danach bestimmen, welche weiteren frühen Looks mit vorhandenen Stufen auskommen und welche eigene Verarbeitung benötigen.

Erster Versuch vom 25.11.2026: Der lokale Vergleich unter `artifacts/historical-looks/baird-30-line/` zeigt ein synthetisches Original neben einer separaten Simulation mit 30 vertikalen Abtastzeilen, 12,5 Bildabtastungen pro Sekunde und einem 3:7-Bildfeld. Die Neonfärbung, Linienbreite und das leichte Flackern sind gestalterische Annäherungen. Es gibt ein Standbild und einen kurzen Bewegungsclip. [Cinémathèque française zum historischen Televisor](https://www.cinematheque.fr/fr/catalogues/appareils/collection/recepteur-de-televisioncnc-ap-15-1197.html) · [Museumsinventar zum 3:7-Format](https://www.lombardiabeniculturali.it/scienza-tecnologia/schede/ST050-00095/)

Umgesetzt am 25.11.2026 unter **Basic → Looks → Frühes Fernsehen → Baird-Televisor · 30 Zeilen**. Der Look zentriert das 3:7-Bildfeld innerhalb der gewählten Ausgabegröße. Bei Video läuft die Bildabtastung mit bis zu 12,5 Hz auf der bestehenden Zeitleiste. Ein Hochformat-Test prüft Standbild, Bildsprung und die Übereinstimmung mit dem MP4-Export.

Ebenfalls umgesetzt unter **Frühes Fernsehen → Britisches Röhrenfernsehen · 405 Zeilen**. Der Look orientiert sich am frühen Marconi-EMI-Format von 1936 mit einem mittigen Schwarzweißbild im Format 5:4, 377 sichtbaren Zeilen und weichen horizontalen Konturen. Die Simulation zeigt ein zusammengesetztes Vollbild bis zu 25-mal pro Sekunde. Das zeitlich versetzte Abtasten der 50 historischen Halbbilder ist eine mögliche spätere Verfeinerung. [Zeitgenössische Beschreibung des 5:4-Formats](https://worldradiohistory.com/UK/Practical-Wireless/30s/Practical-Wireless-1936-02-S-OCR.pdf) · [BBC zur 405-Zeilen-Übertragung](https://www.worldradiohistory.com/UK/BBC/BBC-Technical/BBC-Monograph/bbc-monograph-39-OCR.pdf) · [BBC-Forschungsbericht zu den 377 sichtbaren Zeilen](https://downloads.bbc.co.uk/rd/pubs/reports/1953-01.pdf)

Umgesetzt am 25.11.2026 unter **Frühes Fernsehen → Kineskop · 16-mm-Fernsehfilm**. Das weiche, mittige 4:3-Schwarzweißbild erhält Filmkorn, leichte Schwankungen von Belichtung und Bildstand sowie vereinzelte Staubspuren. Diese Bewegungsmerkmale sind gestalterische Annäherungen und reproduzierbar an die simulierten Filmbilder gekoppelt. Video verwendet bis zu 24 neue Filmbilder pro Sekunde auf der vorhandenen Ausgabezeitleiste. In Expert stehen alle drei frühen Looks in derselben Auswahl. Als Nächstes folgt die Side-by-side-Vergleichsvorschau. [Library of Congress zur Kineskop-Aufnahme](https://guides.loc.gov/moving-image-research/finding-collections/abbreviations-terms) · [Library of Congress zur Rolle der 16-mm-Kopien](https://www.loc.gov/static/programs/national-film-preservation-board/documents/tvstudy.pdf)

Ergänzung des Nutzers vom 25.11.2026 nach Test des Kineskop-Looks: **Die eigenen Looks mit den anderen Optionen kombinierbar machen.**

Umgesetzt unter **Expert → Effekte**. Empfang, VCR, NTSC/VHS und Digitalvideo greifen vor Baird oder dem britischen 405-Zeilen-Bild. Der gewählte historische Bildschirm übernimmt die Rolle des CRT-Shaders. Die separat zuschaltbare 16-mm-Filmstufe kann das Bild jedes dieser Bildschirme oder eines CRT-Shaders abfilmen. Kombinationen lassen sich als Preset speichern und verwenden in Vorschau und Export dieselbe Stufenfolge. Basic bietet weiterhin die unmittelbar anwendbaren Gesamtlooks. Kombinationen mit Screen Loop, Keyframe-Animation und importierten NTSCRT-Gesamtlooks sind gesondert zu prüfen.

#### Vorschlag für die erste historische Bibliothek vom 24.11.2026

Basic gruppiert Looks nach der gewünschten Bildwirkung. Zeitangaben stehen als Orientierung in der Beschreibung, weil mehrere Aufnahme- und Wiedergabeformen gleichzeitig verbreitet waren. Die vier bereits enthaltenen Looks bilden den Anfang. Zwei Varianten erschließen die vorhandenen VCR- und Empfangsstufen.

| Kategorie | Look | Zeitbezug und sichtbarer Unterschied | Grundlage |
| --- | --- | --- | --- |
| Fernsehen und VHS | VHS auf Röhre | Heimvideo der 1980er und 1990er mit weichen Farbkanten und sichtbaren Scanlines | Vorhandener Basic-Look mit ntsc-rs und `crt-easymode`. VHS kam 1976 auf den Markt. [JVCKENWOOD](https://www.jvckenwood.com/en/80-100-20th/) |
| Fernsehen und VHS | Oft abgespielte VHS | Dieselbe Wiedergabesituation mit kurzen Bandaussetzern und Störungen am unteren Bildrand | Neuer Vorschlag aus VCR-Bandzustand `Used`, zurückhaltender Kopfumschaltstörung und demselben CRT-Profil. Vorschau gegen „VHS auf Röhre“ abstimmen. |
| Fernsehen und VHS | Antennen-TV mit Geisterbild | Analoges Fernsehen mit einem schwachen, seitlich versetzten Doppelbild | Neuer Vorschlag aus TV-Empfang mit `ghost_level` und `ghost_delay`, Composite-Signal und CRT. Mehrwegeempfang ist ein historisch belegter Auslöser. [ITU-R zu Mehrwegeempfang](https://www.itu.int/dms_pub/itu-r/oth/0C/07/R0C070000640002PDFE.pdf) |
| Camcorder | MiniDV | Digitale Camcorder-Aufnahme ab Mitte der 1990er mit typischer Standardauflösung | Vorhandener NTSCRT-Look. Sony führte MiniDV-Handycams 1995 ein. Die konkrete Einstellkombination ist eine Annäherung. [Sony-Geschäftsbericht 1996](https://www.sony.com/en/SonyInfo/IR/library/ar/ar_sony_1996.pdf) |
| Frühes Computervideo | CD-ROM (Video 1) | Frühes PC-Video mit niedriger Auflösung und sichtbaren Kompressionsblöcken | Vorhandener NTSCRT-Look. Die Bezeichnung verweist auf Microsoft Video 1. [FFmpeg-Codec-Referenz](https://ffmpeg.org/doxygen/trunk/msvideo1_8c_source.html) |
| Frühes Computervideo | Webcam (2005) | Weiches, niedrig aufgelöstes Webcam-Bild als Bildeindruck der 2000er | Vorhandener NTSCRT-Look. Die Jahreszahl stammt aus dem Originalnamen. |

Für den ersten Katalog genügen eine stabile ID, Kategorie, Titel, Kurzbeschreibung, Zeitbezug, Signalnorm soweit belegt, Herkunft und die Effektwerte. Die Karten zeigen Titel und Bildwirkung, die Herkunft bleibt im Detail sichtbar. Für die ersten analogen Varianten gilt die Kennzeichnung **NTSC-inspiriert**. PAL-, SECAM- und konkrete Röhrenmodelle bleiben eigene Recherchepunkte. [Library of Congress zu analogen Videoquellen](https://loc.gov/preservation/digital/formats/content/video_source.shtml)

Erste Abnahme mit demselben Standbild und Bewegungsclip für alle Karten. VHS-Aussetzer und Antennen-Geisterbild müssen als unterschiedliche Bildfehler erkennbar sein. Der Wechsel zwischen Looks lässt Quellbeschnitt und Ausgabegröße stehen. Bei einem zeitabhängigen Look soll der Videoexport denselben Effekt am gleichen Zeitpunkt wie die Vorschau zeigen.

- Windows-Testbuild vom 24.11.2026 (`20261124-141522-315`): Die sechs Karten stehen in drei Kategorien. Der Test über die Karten bestätigt, dass alle sechs Quellbeschnitt und Ausgabegröße übernehmen. Für die zwei neuen Looks unterscheiden sich die gerenderten Bilder und der geprüfte Vorschauframe stimmt mit dem Effektframe vor der MP4-Kodierung überein.
- Ergänzung vom 24.11.2026 nach Rückfrage des Nutzers: Auch Finn McKentys vollständige NTSCRT-Looks für Basic prüfen. **Clean CRT** ergänzt die Auswahl um einen Röhren-Look ohne Signalstörungen. **Clean VHS** bietet Bandwiedergabe ohne CRT. **CRT aperture mask** kombiniert eine verschlissene EP-Kassette mit einer sichtbaren Streifenmaske. Diese drei Originaldateien haben die Animation deaktiviert und enthalten keinen Quellbeschnitt. **5 second blips** ist ein zusätzlicher Kandidat für animierte Looks und verwendet eine Fünf-Sekunden-Zeitleiste. Originaldateien und Credits erhalten. Der Basic-Aufruf soll den Beschnitt und die Ausgabegröße des Nutzers respektieren. [Original-Presets aus NTSCRT 0.28.1](https://github.com/finnmckenty/NTSCRT/tree/0ab60bf952b09a0c41ea4290979f23dadd963614/presets)
- Ergänzung vom 24.11.2026: Die eigenen Retro-Looks über die Jahrzehnte sollen ebenfalls in **Basic → Looks** stehen. Vorschlag für die Bedienung: nach dem Bildeindruck gruppieren und bei wachsender Auswahl zusätzlich nach Jahrzehnt filtern. Eine Herkunftsangabe auf der Karte kennzeichnet eigene Tape-&-Tube-Looks und NTSCRT-Originale. Eigene Gesamtlooks kombinieren passende Signalquelle, Empfang oder Aufnahme und Bildschirm zu einer direkt anwendbaren Voreinstellung.
- Nutzerwunsch vom 25.11.2026: Beim Wechsel eines Basic-Looks während der Wiedergabe an der aktuellen Position mit dem neuen Look weiterspielen. Umgesetzt für eigene Looks und NTSCRT-Karten. Bei pausierter Vorschau bleibt die Position erhalten.
- Windows-Testbuild vom 24.11.2026 (`20261124-142139-823`): **Clean VHS**, **Clean CRT** und **CRT aperture mask** sind als NTSCRT-Originale in Basic auswählbar. Die Originaldateien und der MIT-Hinweis sind enthalten. Alle neun Karten erhalten Quellbeschnitt und Ausgabegröße. Für jedes der drei Originale stimmt der geprüfte Vorschauframe mit dem entsprechenden Frame vor der MP4-Kodierung überein.
- Nutzeridee vom 24.11.2026: Alle NTSCRT-Looks mit der Installation in einem eigenen Verzeichnis ablegen, damit sie sich ohne Entpacken öffnen lassen. Ausgestaltungsvorschlag: Die 44 Original-Looks unverändert unter `presets/NTSCRT/` mit Versions- und Lizenzhinweis ausliefern. Die fünf Screen-Loop-Presets bleiben im eigenen Unterordner `Screen Loop/` und werden über die Screen-Loop-Steuerung geladen. **Preset laden…** öffnet den NTSCRT-Ordner beim ersten Aufruf und merkt sich danach den zuletzt verwendeten Ordner. Basic zeigt weiterhin eine kuratierte Auswahl. Für das nächste Paket sowohl die Installationsdateien als auch die eingebetteten Originale im Tape-&-Tube-Quellpaket prüfen.
- Windows-Prüfpaket vom 24.11.2026 (`20261124-142856-967`): Alle 49 JSON-Dateien entsprechen bytegenau dem gepinnten Originalarchiv. Der Dateidialog öffnete `presets/NTSCRT/` und lud **Clean VHS** direkt. Die drei eingebetteten Originale sind auch in der Tape-&-Tube-Quellcode-ZIP vollständig enthalten. Die portable Prüfung aus einem getrennten Verzeichnis ist bestanden.
- Nutzervorgabe vom 24.11.2026: Den mitgelieferten NTSCRT-Umfang möglichst weit reduzieren. Vorschlag für das nächste Release: Die 44 Looks, fünf Screen-Loop-Presets und Originalhinweise bleiben im Windows-Paket. Das vollständige NTSCRT-Archiv mit 32,65 MiB entfällt dort. Das passende Quellpaket enthält die verwendeten Dateien aus `port/engine` und `port/codec`, die relevanten Lizenztexte, die gepinnte Revision, Build-Dateien und die tatsächlich angewandten Patches. Vor dem Ersetzen des vollständigen Archivs den Windows-Effektkern aus dem reduzierten Quellpaket ohne Zugriff auf den vollständigen NTSCRT-Checkout bauen und Vorschau sowie Export prüfen.
- Windows-Prüfpaket vom 24.11.2026 (`20261124-143800-066`): Das ZIP sank von 123,33 auf 90,73 MiB. Die 49 Original-Presets sind bytegleich. Das separate Quellpaket ist 1,66 MiB groß und enthält 94 ausgewählte Dateien aus dem gepinnten NTSCRT-Archiv. Der Effektkern ließ sich in einem getrennten, kurzen Windows-Pfad ohne das vollständige NTSCRT-Archiv bauen. Mit diesem Kern bestanden 45 Exportprüfungen und 193 Integrationsprüfungen. Auch die portable Paketprüfung ist bestanden.

### Ton passend zum Look

Nutzeridee vom 26.11.2026: **Den Ton passend zu Effekten und Looks verändern können, eventuell über einen globalen Schalter „Sound Processing“ und mit individuellen Effektkombinationen in Expert.**

- Ausgestaltungsvorschlag: Ein globaler Schalter wendet die eingestellten Toneffekte an oder gibt die Originaltonspur wieder. Beim Umschalten bleiben die Effektwerte erhalten. **Vorschauton** steuert weiterhin, ob man den Ton beim Abspielen hört.
- **Basic:** Passende Audioeinstellungen in ausgewählten Looks hinterlegen, etwa begrenzte Frequenzen und Mono für einen alten Fernseher oder Bandrauschen und leichte Gleichlaufschwankungen für VHS. **Expert:** Audioeffekte einzeln wählen, kombinieren und gemeinsam mit dem Bild als Preset speichern.
- Für Vorschau und Export dieselbe Audioverarbeitung vorsehen und Bild-Ton-Synchronität nach Sprüngen, Look-Wechseln und Wiederholungen prüfen. Welche Tonfassung ein Vergleichsvideo übernimmt, bei der Umsetzung festlegen.

### Basic- und Expert-Modus

Nutzeridee vom 17.11.2026: **Basic und Expert als zwei Bedienmodi vorsehen. Basic konzentriert sich auf die Preset-Auswahl für schnelle Erfolgserlebnisse ohne technisches Vorwissen; Expert bietet alle verfügbaren Detaileinstellungen.**

- Basic macht die strukturierte Preset-Bibliothek zum zentralen Einstieg: Medium laden, passenden Gesamtlook auswählen, Ergebnis ansehen und nutzen/exportieren. Die Vielzahl technischer Effektparameter soll dafür nicht durchgearbeitet werden müssen.
- Expert erschließt sämtliche im aktiven Verarbeitungskern verfügbaren Detailparameter für individuelle Looks und deren Feinabstimmung.
- Die Idee konkretisiert das bereits festgehaltene Preset-Bedienziel und wird mit diesem gemeinsam geplant, nicht als getrennte Preset-Sammlung.
- Ausgestaltungsvorschlag: Beide Modi verwenden dieselbe Effektkette und denselben Look-Zustand. Ein Moduswechsel verändert oder verwirft keine Einstellungen; im Expert-Modus angepasste Looks bleiben beim Wechsel nach Basic erhalten.
- Ausgestaltungsvorschlag: Eigene Looks im Expert-Modus als Presets speichern und anschließend auch in Basic auswählen können. Standardmodus, sichtbare Basisregler und genauer Moduswechsel sind noch gemeinsam festzulegen.
- Zuordnungsvorschlag: Etappe 5, Bedienung und Presetverwaltung. Als Featureidee festgehalten, noch kein Umsetzungsauftrag.

**Vorschlag für den ersten Windows-Stand vom 24.11.2026**

- **Basic** als vorgeschlagener Einstieg beim ersten Start. Sobald ein Medium geladen ist, zeigt die Seitenleiste eine kleine Auswahl vorhandener Looks mit Namen, kurzem Bildeindruck und Herkunft. Der Vergleich bleibt in der Vorschau erreichbar, der Export in der Medienleiste.
- Nutzervorgabe vom 24.11.2026: **Beim Schließen den zuletzt aktiven Modus (Basic oder Expert) lokal speichern und beim nächsten Start wiederherstellen.** Abnahme: Expert wählen, die App schließen und in Expert wieder starten. Danach zu Basic wechseln und denselben Ablauf mit Basic wiederholen.
- **Expert** zeigt die heutigen Effektstufen und die Animationssteuerung. Der Umschalter ändert nur die Ansicht. Der aktuelle Look, die Keyframes und die Wiedergabeposition bleiben beim Wechsel erhalten.
- Eingebaute Basic-Looks ändern die Effektstufen. Quellbeschnitt und Ausgabegröße bleiben beim Anwenden erhalten. Der bestehende Dateiimport übernimmt weiterhin die Einstellungen, die in der geöffneten Datei gespeichert sind. Eigene Expert-Looks können später in dieselbe Bibliothek aufgenommen werden.
- Abnahme: Ein Testbild oder Video laden, einen Look anwenden und exportieren. In Expert einen Effektwert ändern, nach Basic und zurück wechseln und dieselben Werte sowie dieselbe Vorschauposition vorfinden. Historische Kategorien ausarbeiten, sobald dieser Ablauf funktioniert.
- Windows-Testbuild vom 24.11.2026 (`20261124-140809-928`): Basic bietet VHS auf Röhre, MiniDV, Webcam (2005) und CD-ROM (Video 1). Expert zeigt Effekte und Animation, die Ausgabe ist in beiden Modi erreichbar. Der letzte Modus wurde nach regulärem Schließen in beiden Richtungen beim Neustart wiederhergestellt.

### Vergleichsvorschau nebeneinander

Nutzeridee vom 17.11.2026: **Zusätzlich zur Splitscreenvorschau eine Side-by-side-Ansicht von Quellmaterial und verarbeitetem Output anbieten.**

- Zwischen bestehender Vorschau mit verschiebbarem Trenner und zwei vollständigen Ansichten nebeneinander wählen können; beide Vergleichsarten erhalten.
- Quelle und Ergebnis klar beschriften und bei Video denselben Zeitpunkt bzw. Frame synchron anzeigen.
- Am 25.11.2026 umgesetzt. Über **Nebeneinander** erscheinen die vollständige Quelle und die bearbeitete Ausgabe in getrennten, beschrifteten Vorschauen. Der gemeinsame Zoom vergrößert beide Ansichten. **Trenner** bietet weiterhin den Vergleich mit verschiebbarer Trennlinie.
- Ergänzende Nutzeridee vom 17.11.2026: **Optional die Side-by-side-Darstellung als fertiges Vergleichsvideo exportieren**, um Quelle und Effekt gemeinsam als aufmerksamkeitsstarken Social-Media-Output zu zeigen.
- Am 25.11.2026 umgesetzt unter **Ausgabe → Vergleichsvideo exportieren**. Die eingestellten Maße gelten für den Gesamtclip. Automatisch entsteht eine Leinwand mit zwei Hälften im Seitenverhältnis der bearbeiteten Ausgabe. Links steht das vollständige Original, rechts die bearbeitete Fassung. Beide Bilder werden ohne Verzerrung eingepasst und verwenden dieselbe Zeitleiste. MP4, MOV und WebM übernehmen die Tonspur der Quelle. Der Vergleich lässt sich auch als GIF speichern.
- Ausgestaltungsvorschlag: Seitenverhältnisse erhalten und beide Ansichten mit vergleichbarem Darstellungsmaßstab anzeigen; Verhalten bei abweichenden Ausgabeformaten und kleinen Fenstern gemeinsam festlegen.
- Zuordnungsvorschlag: Etappe 5, Bedienung und Vergleichsvorschau. Als Featureidee dokumentiert, noch kein Umsetzungsauftrag.

## Digitalvideo-Formate und Gesamtlooks

Am 20.11.2026 bewertete der Nutzer die Vorgehensweise von NTSCRT als logischer und stringenter. Dort übernimmt ein Digitalvideo-Format die Kamera- und Codec-Werte sowie die empfohlene interne Auflösungsbreite. Die NTSC- und CRT-Einstellungen bleiben erhalten.

- Im lokalen Build umgesetzt: Webcam, MiniDV und CD-ROM stehen als Digitalvideo-Formate zur Auswahl. Die Auswahl aktiviert Digitalvideo und übernimmt die passende interne Breite. NTSC/VHS, CRT und eine fest gewählte Exportgröße bleiben erhalten.
- Gesamtlooks für die geplante Preset-Bibliothek vorsehen. Ihre Auswahl wendet die darin gespeicherten Einstellungen über mehrere Effektstufen hinweg an.

## HDR-Quellmaterial

Am 20.11.2026 fragte der Nutzer nach HDR-Unterstützung für die neue Effektkette.

- Im lokalen Windows-Build umgesetzt: PQ- und HLG-Videos beim Einlesen per Tone-Mapping nach SDR (BT.709) umwandeln. Vorschau und Export verwenden dieselbe Umwandlung. Die Effektkette und der Export arbeiten mit SDR.
- Abnahmevorschlag: reale PQ- und HLG-Clips sowie eine SDR-Kontrollquelle prüfen. Helle Details, Hauttöne und Farbsättigung in Vorschau und Export vergleichen. Bei einem längeren Clip auch Ton-Synchronität und Positionssprünge prüfen.
- HDR-Ausgabe als separate spätere Erweiterung betrachten. Dafür wäre eine durchgängige Verarbeitung mit höherer Farbtiefe einschließlich der Effektstufen nötig.

## Manuelle Drehung von Videos

Nutzeridee vom 20.11.2026: **Eine optionale Drehfunktion in Tape & Tube anbieten.** Anlass war ein Testvideo, das in NTSCRT um 90° nach links gedreht geladen wurde. Die Idee betrifft die Bedienung in Tape & Tube.

- Ausgestaltungsvorschlag: Die automatisch erkannte Ausrichtung als Voreinstellung verwenden und eine Drehung um 90° nach links oder rechts sowie um 180° ermöglichen. Die gewählte Ausrichtung gilt für Vorschau und Export. Eine Funktion zum Zurücksetzen stellt die automatische Ausrichtung wieder her.

## Quellbeschnitt per Maus verschieben

Nutzeridee vom 23.11.2026: **Den Bildausschnitt direkt in der Vorschau per Maus horizontal und vertikal verschieben.**

- Ausgestaltungsvorschlag: Ziehen in der Vorschau und die vorhandenen Positionsregler steuern denselben Ausschnitt. Die Regler zeigen die beim Ziehen gewählte Position, die auch Vorschau und Export verwenden.

## Weitere Exportformate für Bilder und Videos

Nutzeridee vom 23.11.2026: **Standbilder in gängigen Bildformaten ausgeben können.**

- Ergänzung des Nutzers vom 24.11.2026: **TIFF und BMP ebenfalls als Exportformate vorsehen.**
- Ausgestaltungsvorschlag: Neben dem vorhandenen PNG auch JPEG, WebP, TIFF und BMP im Dialog **Bild speichern…** anbieten. Für verlustbehaftete Formate soll die Qualität einstellbar sein. Quellbeschnitt, Effekte und Ausgabegröße gelten für jedes Format.
- Windows-Testbuild vom 24.11.2026 (`20261124-134401-371`): Alle fünf Bildformate sind im Speicherdialog verfügbar. Unter **Ausgabe** lässt sich die Qualität für JPEG und verlustbehaftetes WebP einstellen. WebP ist standardmäßig verlustfrei und benötigt FFmpeg mit `libwebp`.
- Nutzeridee vom 24.11.2026: **Auch weitere Videoformate für den Export prüfen.**
- Ergänzung des Nutzers vom 24.11.2026: **MOV und WebM als Exportcontainer sowie VP9 als Videocodec vorsehen.**
- Ausgestaltungsvorschlag: MP4 mit H.264 als Standard für die Weitergabe beibehalten. MOV mit ProRes 422 für den Videoschnitt und WebM mit VP9 und Opus für die Einbindung im Web anbieten. Bei FFmpeg-abhängigen Formaten den jeweils verfügbaren Encoder prüfen. Videoformate mit Ton mit dem Referenzclip auf Synchronität und Wiedergabe testen. Die Formatauswahl bei den Ausgabeoptionen berücksichtigen.
- Windows-Testbuild vom 24.11.2026 (`20261124-135201-778`): Der Speicherdialog bietet MOV mit ProRes 422 und PCM sowie WebM mit VP9 und Opus an. Tape & Tube prüft die benötigten FFmpeg-Encoder. Automatisierte Tests mit einem zeitversetzten Testclip bestätigen Bildanzahl, Bildrate, Auflösung und Tonversatz für beide Formate.

## Komponenten und Credits übersichtlicher anzeigen

Nutzeridee vom 24.11.2026: **Die Hardware- und Softwareangaben in Komponenten & Credits strukturiert statt als Fließtext anzeigen.**

- Ausgestaltungsvorschlag: Hardware und den tatsächlich verwendeten Grafikadapter in einem eigenen Abschnitt anzeigen. Softwarekomponenten erhalten pro Eintrag Felder für Aufgabe, Version, Status, Lizenz und Quelle. Die Herkunft von Tape & Tube und die Danksagungen bleiben in einem eigenen Abschnitt gut auffindbar. Technische Details und Dateipfade lassen sich bei Bedarf aufklappen.
- Windows-Testbuild vom 24.11.2026 (`20261124-135732-823`): **Komponenten & Credits** ist in **Projekt**, **Software** und **Hardware** gegliedert. Bibliothekspfade stehen in aufklappbaren Details. Das Hardware-Register zeigt die dem Prozess verfügbaren logischen CPU-Kerne und die tatsächlich initialisierten Grafikadapter.

## Seitenverhältnis für die Ausgabe sperren

Nutzeridee vom 23.11.2026: **Über ein Häkchen in der Ausgabe wählen können, ob das Seitenverhältnis beim Einstellen der Größe erhalten bleibt.**

- Ausgestaltungsvorschlag: Bei benutzerdefinierten Maßen berechnet die aktive Sperre beim Ändern der Breite oder Höhe jeweils den anderen Wert. Bei gelöster Sperre bleiben beide Maße frei wählbar. Als Bezugsverhältnis bietet sich der aktuelle Quellausschnitt an. Die Optionen **Einpassen** und **Beschnitt** bestimmen weiterhin, wie das Bild in die Zielgröße passt.

## CPU-Kerne und Auslastung anzeigen

Nutzeridee vom 20.11.2026: **Verfügbare und von Tape & Tube genutzte CPU-Kerne sowie die CPU-Auslastung anzeigen.**

- Ausgestaltungsvorschlag: Die Anzeige bei Vorschau und Export zugänglich machen. Dabei die Zahl der verfügbaren logischen Prozessoren, die tatsächlich gemessene CPU-Last von Tape & Tube einschließlich seiner Verarbeitungsprozesse und die Gesamtauslastung des Systems verständlich beschriften. Bei „genutzten Kernen“ das Messintervall angeben.
- Zuordnungsvorschlag: Leistungsdiagnose nach Abschluss der Windows-Funktionsparität.

## Leitlinien für die Umsetzung

- Für das Aufholen der Hauptfunktionen zuerst NTSCRTs vorhandenen Code bewerten. FFmpeg, frei0r, G'MIC und zusätzliche RetroArch-Shader ergänzen das Angebot nach konkretem Nutzen.
- Keine Sammlung vollständig einbauen, nur um die Effektzahl zu erhöhen. Einfache Rausch-/Versatzfilter bilden keine vollständige Band-/Laufwerkssimulation ab.
- Varianten, Parameteränderungen und Export verwenden dieselbe Verarbeitung und dieselbe Zeitbasis. Versionsangaben und fehlende Module werden ausdrücklich behandelt.
- Neue Abhängigkeiten brauchen einen festgelegten Quellstand, ihre ursprünglichen Hinweise und eine dokumentierte Austauschgrenze. Keine Änderung an fremden Installationen oder stiller Download beliebiger Module durch Presets.
- Aufwand im Vergleich ist qualitativ. Daraus folgt weder eine verbindliche Kalenderprognose noch eine Schätzung menschlicher Arbeitsstunden.

Details und Quellen: [NTSCRT_COMPARISON.md](NTSCRT_COMPARISON.md), ergänzende Grundlage: [EFFECTS_AND_LINUX.md](EFFECTS_AND_LINUX.md).
