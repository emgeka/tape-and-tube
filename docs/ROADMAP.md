# Tape & Tube — Entwicklungsplan in sechs Etappen

Stand: 20.11.2026, nach Veröffentlichung von 0.1.0-beta.2. Ziel ist zunächst Funktionsparität mit NTSCRT auf Windows. Darauf bauen eigene Erweiterungen und eine Linux-Ausgabe auf. Herkunft und ursprüngliche Lizenzen bleiben sichtbar; externe Komponenten bleiben gezielt austauschbar.

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

**Danach:** eigene Epochen-Presets, Röhren- und Empfangsprofile, Basic/Expert, Side-by-side-Vorschau und -Export, manuelles Drehen, weiterer HDR-Ausbau und Linux-Desktop-Ausgabe. Die Ideen bleiben weiter unten dokumentiert.

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

Gemeinsames Gestaltungskonzept: **Bildquelle → Übertragung und Empfang → Aufnahme/Wiedergabe → Bildschirm einer gewählten Epoche.** Die konkreten Modelle und historischen Zuordnungen sind noch zu untersuchen.

### Basic- und Expert-Modus

Nutzeridee vom 17.11.2026: **Basic und Expert als zwei Bedienmodi vorsehen. Basic konzentriert sich auf die Preset-Auswahl für schnelle Erfolgserlebnisse ohne technisches Vorwissen; Expert bietet alle verfügbaren Detaileinstellungen.**

- Basic macht die strukturierte Preset-Bibliothek zum zentralen Einstieg: Medium laden, passenden Gesamtlook auswählen, Ergebnis ansehen und nutzen/exportieren. Die Vielzahl technischer Effektparameter soll dafür nicht durchgearbeitet werden müssen.
- Expert erschließt sämtliche im aktiven Verarbeitungskern verfügbaren Detailparameter für individuelle Looks und deren Feinabstimmung.
- Die Idee konkretisiert das bereits festgehaltene Preset-Bedienziel und wird mit diesem gemeinsam geplant, nicht als getrennte Preset-Sammlung.
- Ausgestaltungsvorschlag: Beide Modi verwenden dieselbe Effektkette und denselben Look-Zustand. Ein Moduswechsel verändert oder verwirft keine Einstellungen; im Expert-Modus angepasste Looks bleiben beim Wechsel nach Basic erhalten.
- Ausgestaltungsvorschlag: Eigene Looks im Expert-Modus als Presets speichern und anschließend auch in Basic auswählen können. Standardmodus, sichtbare Basisregler und genauer Moduswechsel sind noch gemeinsam festzulegen.
- Zuordnungsvorschlag: Etappe 5, Bedienung und Presetverwaltung. Als Featureidee festgehalten, noch kein Umsetzungsauftrag.

### Vergleichsvorschau nebeneinander

Nutzeridee vom 17.11.2026: **Zusätzlich zur Splitscreenvorschau eine Side-by-side-Ansicht von Quellmaterial und verarbeitetem Output anbieten.**

- Zwischen bestehender Vorschau mit verschiebbarem Trenner und zwei vollständigen Ansichten nebeneinander wählen können; beide Vergleichsarten erhalten.
- Quelle und Ergebnis klar beschriften und bei Video denselben Zeitpunkt bzw. Frame synchron anzeigen.
- Ergänzende Nutzeridee vom 17.11.2026: **Optional die Side-by-side-Darstellung als fertiges Vergleichsvideo exportieren**, um Quelle und Effekt gemeinsam als aufmerksamkeitsstarken Social-Media-Output zu zeigen. Als mögliche Erweiterung zur Prüfung festgehalten.
- Ausgestaltungsvorschläge für diesen Export: eigener wählbarer Ausgabemodus mit synchronen Quell-/Ergebnisframes, optionalen Beschriftungen und passenden Ausgabeformaten für Social Media. Dabei die Gesamtgröße des Vergleichsvideos ausdrücklich festlegen; normale Exporte bleiben weiterhin auswählbar. Konkrete Layouts und Formate noch gemeinsam entscheiden.
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
- Nutzeridee vom 24.11.2026: **Auch weitere Videoformate für den Export prüfen.**
- Ergänzung des Nutzers vom 24.11.2026: **MOV und WebM als Exportcontainer sowie VP9 als Videocodec vorsehen.**
- Ausgestaltungsvorschlag: MP4 mit H.264 als Standard für die Weitergabe beibehalten. MOV mit ProRes 422 für den Videoschnitt und WebM mit VP9 und Opus für die Einbindung im Web anbieten. Bei FFmpeg-abhängigen Formaten den jeweils verfügbaren Encoder prüfen. Videoformate mit Ton mit dem Referenzclip auf Synchronität und Wiedergabe testen. Die Formatauswahl bei den Ausgabeoptionen berücksichtigen.

## Komponenten und Credits übersichtlicher anzeigen

Nutzeridee vom 24.11.2026: **Die Hardware- und Softwareangaben in Komponenten & Credits strukturiert statt als Fließtext anzeigen.**

- Ausgestaltungsvorschlag: Hardware und den tatsächlich verwendeten Grafikadapter in einem eigenen Abschnitt anzeigen. Softwarekomponenten erhalten pro Eintrag Felder für Aufgabe, Version, Status, Lizenz und Quelle. Die Herkunft von Tape & Tube und die Danksagungen bleiben in einem eigenen Abschnitt gut auffindbar. Technische Details und Dateipfade lassen sich bei Bedarf aufklappen.

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
