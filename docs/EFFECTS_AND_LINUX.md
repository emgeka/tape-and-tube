# Effektumfang und Linux — Arbeitsgrundlage

Stand: 16.11.2026. Ziel: Tape & Tube soll beim Effektumfang mit NTSCRT mithalten und darüber hinaus eigene Vorteile bieten. Bestehende Open-Source-Prozessoren sollen flexibel eingebunden werden. Eine Linux-Ausgabe ist ausdrücklich in die Planung aufzunehmen. Diese Datei beschreibt Untersuchungen und Entscheidungen; sie kündigt keine bereits implementierten Effekte oder Linux-Binärpakete an.

## Geeignete Quellen

| Projekt | Nutzen | Einbindung und nächster Nachweis |
| --- | --- | --- |
| [NTSCRT](https://github.com/finnmckenty/NTSCRT) | Digitalvideo/Codecs, physikalische VCR-Simulation, Glitch, Screen Loop und Keyframes; wichtigste Referenz für die Funktionslücken | Den aktuellen Windows-Port auf wiederverwendbare CPU-/GPU-Module prüfen. MIT-Hinweise und getrennte Lizenzen aller eingebundenen Komponenten erhalten. Nicht jede Mac-Funktion ist bereits Windows-Code. Zuerst eine Stufe isoliert mit deterministischer Zeitsteuerung und Reset bei Seek ausführen. |
| [FFmpeg-Filter](https://ffmpeg.org/ffmpeg-filters.html) | Kanalversatz, Nachleuchten, zeitliche Mischung, Rauschen, Farb-LUTs und Linsenverzerrung | Im geprüften Gyan-9.0.2-Essentials-Build sind `chromashift`, `rgbashift`, `lagfun`, `tmix`, `tblend`, `noise`, `lut3d`, `lenscorrection`, `perspective` und `vignette` vorhanden. Erste Prototypen können dieselbe festgelegte Filterkette für Vorschau und Export nutzen. Das ist keine vollständige VCR-Simulation und ersetzt sie nicht. |
| [frei0r](https://github.com/dyne/frei0r) | Sammlung portabler Videoeffekt-Plugins, darunter zusätzliche Verfremdungen | C-Plugin-Schnittstelle hinter einem austauschbaren Host; GPL und abweichende dateiweise Hinweise des gewählten Plugins prüfen. Windows- und Linux-Builds für eine kleine Auswahl herstellen. Im geprüften FFmpeg-Essentials-Build ist **kein frei0r-Filter aktiviert**; nicht einfach als bereits verfügbar anbieten. |
| [G’MIC](https://gmic.eu/) / [libgmic](https://github.com/GreycLab/gmic) | Kreative Bildverarbeitung, Texturen, Film- und grafische Looks | Zuerst über einen langlebigen Hilfsprozess oder eine stabile eigene C-Schnittstelle untersuchen. CeCILL-/CeCILL-C-Auswahl und konkrete Filter-/Datenlizenzen festhalten. Still-Effekte sind nicht automatisch zeitlich stabile Echtzeit-Videoeffekte. Keine neue Instanz pro Frame. |
| [RetroArch slang-shaders](https://github.com/libretro/slang-shaders) | Weitere CRT-/VHS-/Bildschirm-Looks mit vorhandenem Shader-Host | Kandidaten auf dem gepinnten Stand einzeln auswählen, inklusive Texturen, Pass-Abhängigkeiten und Originalhinweisen. Zusätzliche Shader dürfen die vorhandenen sieben Presets nicht verdrängen. Multipass, History, Skalierung und Bildqualität prüfen. |

Die FFmpeg-Fähigkeiten wurden am tatsächlich heruntergeladenen Build mit `-version` und `-filters` geprüft. Weitere Kandidaten sind recherchiert, noch nicht eingebunden. Ein Effektkatalog darf nur anzeigen, was der aktive Prozessor wirklich unterstützt. Mehr Filter allein bedeutet noch keinen besseren Look.

## Gemeinsame Verarbeitung statt separater Windows-/Linux-Effekte

Ein gemeinsamer Kern soll Bilder als eindeutig beschriebenes Frame-Format übernehmen: Abmessungen, Stride, Farb-/Alphaformat, Zeitstempel, Framenummer, Seed, Parametersnapshot und Ausgabeskalierung. Die Oberfläche darf keine Effektlogik besitzen. Jede Stufe braucht Fähigkeiten/Parameter-Metadaten, Bypass, Zustandsreset, Fehler und Abbruch sowie Herkunft und Versionsangabe. Zeitabhängige Stufen brauchen reproduzierbaren Vorlauf nach einem Seek; eine einzelne Bildfunktion genügt dafür nicht.

Vorschau und Export sollen dieselbe Stufenreihenfolge und dieselben Einstellungen verwenden. Vorschau darf Frames auslassen; der zeitliche Effektzustand muss trotzdem korrekt fortschreiten. Export verarbeitet alle Frames. Neue Looks speichern Prozessor- und Schemaversionen; fehlende Module führen zu einer verständlichen Meldung statt still veränderten Ergebnissen. Frei auswählbare Bibliotheks-/Werkzeugpfade bleiben erhalten. Fremdmodule bekommen eigene Lizenz-/Quellhinweise und festgelegte Referenzversionen.

Das ist die gewünschte Schnittstelle, noch kein fertiges Plugin-System. Ungeprüfte native Plugins führen Code aus; Installationen verwenden nur bewusst ausgewählte, nachvollziehbare Module. Es gibt keinen stillen Download beliebiger Effekte aus Presets.

## Linux

Die jetzige App ist `net10.0-windows` mit WPF und Vortice/Direct3D 11; `MediaTools` sucht `.exe`-Dateien und Windows-PATH. Das Rust-NTSC-Modul ist ein guter gemeinsamer Ansatz, der derzeitige GPU-Host und Installer sind es nicht. Wine ist kein Nachweis einer nativen Linux-Ausgabe.

Als erster Kandidat bietet sich [Avalonia](https://docs.avaloniaui.net/docs/supported-platforms) für eine gemeinsame .NET-Oberfläche an; [Linux-Anforderungen](https://docs.avaloniaui.net/docs/platform-specific-guides/linux) und Lizenz der gewählten Version vor Festlegung prüfen. Die WPF-Oberfläche lässt sich damit nicht unverändert übernehmen. Für CRT ist ein Linux-fähiges librashader-Backend (z.B. Vulkan/OpenGL) oder ein gemeinsamer GPU-Host zu erproben. CPU-Pufferübergaben, Farbräume und Readback-Kosten sind dabei zu messen.

Reihenfolge:

1. FFmpeg-Installation für Windows abschließen; parallele zusätzliche Effekte zunächst nur untersuchen.
2. NTSCRT-Funktionsmatrix erstellen und eine wiederverwendbare zusätzliche Effektstufe als Prototyp gegen Windows testen. Zusätzlich eine kleine, erkennbare Auswahl FFmpeg-/frei0r-/Shader-Effekte bewerten.
3. Plattformneutralen Verarbeitungskern von WPF/Direct3D trennen. Linux-Prototyp: Bild laden → NTSC → CRT → PNG und Video mit synchronem Ton exportieren. Erst dieser Test entscheidet über die endgültige UI-/GPU-Technik.
4. Gleiche Quellen, Looks und Ausgabegrößen unter Windows und Linux vergleichen; Zeitverhalten, Alpha, Rundung, Shaderabweichungen, Preview/Export und Fehler testen. Keine Zusage bitgleicher GPU-Ausgaben ohne Messung.
5. Erst danach Linux-Pakete bauen. Zunächst x86-64 und ein klar benanntes Distributions-/GPU-Testziel; Paketformat, X11/Wayland und FFmpeg-Bezug bewusst wählen. Keine pauschale Behauptung „läuft auf Linux“ aufgrund eines einzelnen Systems.

Ein Linux-Angebot erweitert den Nutzen. Ob es dem Original einen Vorsprung verschafft, muss an dessen dann aktuellem Stand und an unserem tatsächlich getesteten Angebot gemessen werden.
