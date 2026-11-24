# Gemeinsamer Kern — Entscheidung und Umsetzungsvorlage

Stand: 17.11.2026. Diese Vorlage hält die Architekturentscheidung nach dem Windows-/Linux-Prototyp fest. Die erste Integration wurde anschließend ausdrücklich beauftragt und ist im lokalen Entwicklungsstand umgesetzt; siehe [Implementierung und Grenzen](DIGITAL_VIDEO_INTEGRATION.md). Dieses Dokument hält auch die weitergehenden Freigabeziele fest und ist keine öffentliche Releasefreigabe.

## Entscheidung für die nächste Implementierung

**Zielkern: NTSCRTs gepinnte Rust-/wgpu-Engine hinter einem eigenen, dauerhaft laufenden Worker-Prozess. FFmpeg/ffprobe bleiben die Medienwerkzeuge.** Die bestehende WPF-Oberfläche wird zuerst über einen Adapter angebunden. Plattformneutrale Anwendungslogik wird dabei von WPF getrennt; die Wahl der späteren gemeinsamen Oberfläche bleibt offen.

Gründe und Grenzen:

- Die vollständige Kette mit Digitalvideo, NTSC und sieben CRT-Presets funktioniert im isolierten Prototyp unter Windows und nativ unter Linux. [Linux-Test und Bildvergleich](LINUX_CORE_TEST.md)
- Ein dauerhaft laufender Prozess erhält Codec-/Grafikzustand über Frames. Der bisherige einmalige CLI-Job mit kompletter Rohbilddatei auf Festplatte dient als Referenz; er ist noch keine geeignete interaktive App-Anbindung.
- Der Worker kann bei Abbruch oder Ausfall beendet und neu gestartet werden, ohne GPU-Objekte oder Rust-Panics in die Oberfläche einzubetten. Er hält seine GPU-Objekte auf dem eigenen Verarbeitungsthread. Das ist eine Entwurfsentscheidung; Robustheit und Kosten müssen gemessen werden.
- Eingebaute Pipe-Kommunikation mit binären Pixeln zuerst erproben. Gemeinsamer Speicher wird erst bei nachgewiesenem Bedarf ergänzt. Kein Prozessstart pro Frame, keine neue Geräte-/Shaderinitialisierung pro Reglerbewegung.
- Der bereits geprüfte Digitalvideo-C-Host bleibt ein separater Test-/Integrationsnachweis. Er wird nicht zusätzlich in den vollständigen Worker geladen: Digitalvideo, NTSC und CRT dürfen jeweils nur einmal ausgeführt werden.
- Der Linux-Test mit llvmpipe belegt Funktion, keine beschleunigte Vorschauleistung. Die VM benötigt einen ausdrücklich gewählten Softwareweg; ungeeignete Adapter führen zu einer verständlichen Meldung. Kein stiller Wechsel auf langsames Software-Rendering.

## Bausteine und Verantwortung

Die Namen in dieser Tabelle sind geplante Arbeitsnamen, keine bereits vorhandenen neuen Projekte.

| Baustein | Verantwortung | Grenze |
| --- | --- | --- |
| Plattformneutrale Anwendungslogik | Projekt-/Lookzustand, Presetkatalog, Zeitbasis, Ausgabegeometrie, Auftragsplanung, Fehler | Keine WPF-Typen und keine Effektsimulation. |
| Medienadapter | FFprobe-Metadaten, FFmpeg-Dekodierung, Quellframezugriff, Kodierung und Tonzuordnung | Keine zweite Effektkette im FFmpeg-Export verstecken. |
| Worker-Client | Prozesslebensdauer, Protokoll, binäre Puffer, Fortschritt, Historie und Abbruch | Keine Einstellungen eigenständig abändern oder fehlende Stufen ersetzen. |
| Rust-Worker | Digitalvideo → NTSC → Downscale → später VCR/Glitch → CRT; Metadaten und Zustandsführung | Keine Fenster, Dateiauswahldialoge oder sprachabhängige UI-Texte. |
| Windows-UI | Laden, Wiedergabe, Vergleich, Einstellungen, Exportbedienung | Arbeitet über Anwendungslogik und Adapter; bisherige Funktionalität erhalten. |
| Bestandsrenderer | Vorhandene NTSC-/D3D11-Verarbeitung für alte Looks | Befristeter Kompatibilitätsweg für gespeicherte Bestandslooks, kein paralleler Neubau der zusätzlichen Effekte. |

Vorschau und Export bekommen getrennte Worker-Instanzen mit demselben Kern und eingefrorenen Lookwerten. Der Export darf die Vorschauinstanz und deren Zustände nicht übernehmen. Prozessanzahl, Puffer und Cache bleiben begrenzt; Ressourcen für beide Instanzen zusammen messen.

## Frame-, Farb- und Zeitvertrag

| Thema | Festlegung für die erste Integration |
| --- | --- |
| Pixel | Zeilenweise von oben, gepacktes RGBA8 am Worker. WPF-BGRA wird am Adapter genau einmal umgeordnet. Kanalreihenfolge mit asymmetrischem Farbtest prüfen. |
| Alpha | Erste Produktionsstufe verarbeitet ein undurchsichtiges SDR-Bild. Transparente Bilder vor dem Kern ausdrücklich auf Schwarz zusammensetzen, entsprechend bestehendem Verhalten. Keine versprochene transparente CRT-/VHS-Ausgabe. |
| Farbe | RGB-Vollbereich, dokumentierter SDR-Eingangsfarbraum und Transfer. Videometadaten beim Dekodieren auswerten und in den gewählten Arbeitsraum konvertieren. HDR/PQ/HLG und unbekannte Eingaben ausdrücklich behandeln; keine ungeprüfte HDR-als-SDR-Auslegung. Exportkonvertierung und Kennzeichnung konsistent halten. |
| Maße | Eingangs-/Ausgangsmaße und Nutzdatenlänge validieren, Überläufe vor Allokation abfangen. Mindestens die bisherigen 24-Megapixel-Quellen erhalten, bevor der neue Renderer Standard wird; der Prototyp erlaubt bislang weniger. Speicher-/Gerätelimits ausdrücklich melden. |
| Zeit | Rationale Bildrate und ganzzahliger Index der normalisierten Quellsequenz. Daraus Zeit ableiten; UI-Wanduhr steuert Anzeigeanforderungen, nicht die Effektberechnung. |
| Variable Bildrate | Für den ersten Schritt wie bisher eine eindeutig normalisierte CFR-Sequenz verwenden. Dekoder, Historie und Export müssen dieselbe Folge und Zuordnung zur Quellzeit sehen. Native VFR-Verarbeitung ist damit nicht zugesagt. |
| Zufall | NTSC-Seed aus dem Look erhalten. Digitalsimulation folgt dem geprüften Capture-/Frameindex-Verfahren; keinen zusätzlichen Seedregler erfinden. CRT-Framezählung und Offsets gehören zum versionierten Rendererprofil. |
| Einstellungen | Vollständiger unveränderlicher Snapshot pro Renderauftrag, mit Source-ID, Lookrevision, Renderer-/Modulversionen und Zeitkonvention. Spätere Parameterhistorie bekommt eine eigene Revisionskennung. |

**Bekannte Migration:** Der Bestandsrenderer verwendet Framenummern ab 0; der geprüfte Engine-Prototyp setzt für NTSC/CRT `sourceIndex + 1`. Außerdem unterscheiden sich Resize und librashader-Version. Deshalb wird ein alter Look nicht allein durch Umbenennung zum neuen Renderer migriert.

## Protokoll und Zustandsführung

Vorgesehenes Worker-Protokoll v1:

- Start-Handshake mit Protokollversion, Build-/Quellpins, Fähigkeiten, Parametern, sieben CRT-Presets und tatsächlichem Grafikadapter. Nicht unterstützte Versionen vor dem ersten Bild ablehnen.
- Explizite Nachrichten für Quelle öffnen, Look setzen, Frame rendern, Zustandsreset, Abbruch und Beenden. Kontrollnachrichten und Binärdaten mit Typ, Auftragskennung und begrenzter Länge rahmen; keine Base64-Bilder in JSON.
- Quelle/Lookrevision in Antworten prüfen. Verspätete Antworten einer alten Quelle oder Reglerstellung dürfen keine neuere Vorschau ersetzen.
- Frühere Quellframes bei Bedarf über einen Rückkanal anfordern. Der Client muss solche Anfragen während eines laufenden Renderauftrags bedienen können; kein gegenseitiges Warten auf dessen Ende. Fehlende Historie ergibt einen Fehler, keinen Ersatz durch das aktuelle Bild.
- Parameter, Header, Längen und unbekannte Nachrichten strikt prüfen. Ressourcen-/Replaygrenzen, Prozessabsturz, Kanalende und Zeitüberschreitung kontrolliert behandeln.
- Bei unterstütztem Abbruch zunächst kooperativ stoppen. Reagiert der Worker nicht, Prozess beenden, Ausgabe verwerfen und eine neue Instanz starten. Grenzen dieses Vorgehens bei Grafiktreiberfehlern praktisch prüfen.

Ein Neustart stellt noch keinen zeitlichen Effektzustand wieder her. Anschließend muss der erforderliche Vorlauf kontrolliert erneut berechnet werden. Standardmäßig Verlauf korrekt fortschreiben und nur Anzeigeframes auslassen. Rücksprünge/Loop erhalten einen definierten Reset und Vorlauf. Wiederverwendbare Checkpoints sind an Quelle, Einstellungen, Zeitbasis und Version gebunden.

Für temporale CRT-Presets ist ein Reset plus Wiederaufbau ab definiertem Start zunächst der sichere Referenzweg. Schnellere Seekverfahren dürfen ihn erst nach Gleichheitsprüfung ersetzen. Lange Vorläufe brauchen Fortschritt und Abbruch; aus dem Einzelbildvergleich aller sieben Shader folgt keine allgemeine Seekgleichheit.

## Presets, Modi und Vergleich

**Ein gemeinsamer Lookzustand** trägt die tatsächlichen Werte. Basic und Expert werden später zwei Ansichten dieses Zustands. Beim Umschalten keine Werte zurücksetzen und keinen anderen Renderer auswählen.

Vorgesehenes neues Lookformat enthält:

- Format-/Schemaversion, Rendererprofil, erforderliche Stufen mit deren Schema-/Prozessorversionen, vollständige Parameter und Ausgabeeinstellungen.
- Stabile Preset-ID, Titel/Beschreibung mit Sprachbezug und Metadaten für Epoche, Region/Fernsehnorm, Quell-/Übertragungs-/Aufnahmeweg und Bildschirmprofil. Unbekannte historische Angaben nicht erfinden.
- Herkunft/Urheber/Lizenz für den Presetinhalt; Referenzen und Kennzeichnung recherchierter Rekonstruktion oder kreativer Interpretation.
- Getrennte Kennzeichnung des Nutzerpresets und seiner Vorlage; Änderungen an einer Vorlage verändern einen gespeicherten Nutzerlook nicht stillschweigend.

Bestehende JSON-v1-Looks werden weiterhin gelesen und zunächst ihrem Bestandsrenderer zugeordnet. Eine Konvertierung erzeugt einen neuen Look nach überprüftem Bildvergleich; das Original wird erhalten. NTSCRT-Import ist ein eigener Adapter. Fehlende oder nicht übertragene Stufen sichtbar melden, niemals einen Teilimport als identisches Gesamtergebnis bezeichnen.

Der Katalog bekommt eine definierte Reihenfolge/Sortierung und strukturierte Metadaten. Die Oberfläche soll historische Gesamtlooks direkt zugänglich machen, ohne alle Metadaten als Pflichtauswahl abzufragen. Favoriten, Vorschauen, Basic/Expert und umfassende historische Bibliothek werden in Etappe 5 umgesetzt, nicht beiläufig im Host-Umbau.

Vergleichsdarstellung gehört hinter die Effektkette: Quell-/Outputframe desselben Zeitpunktes getrennt bereithalten. Der vorgemerkte Side-by-side-Export wird später als eigener Kompositionsmodus auf diese Frames angewendet; er ist kein CRT-Effekt und ändert den gespeicherten Look nicht.

## Integration in überprüfbaren Paketen

| Paket | Konkretes Ergebnis | Abschlussnachweis |
| --- | --- | --- |
| A. Verträge und Worker | Plattformneutrale Frame-/Looktypen und dauerhaft laufender Worker; Metadaten, Datenkanal, Historie, Fehler/Abbruch | Echte Frames über mehrere Aufträge ohne Prozessneustart; Source-/Lookwechsel, verlorene Verbindung, fehlende Historie und Abbruch getestet. Alte Test-CLI als Referenz erhalten. |
| B. Windows-Adapter | Neue Kette in Vorschau und Export zuschaltbar; alte Looks bleiben im Bestandsweg | Vorhandene 199 Prüfungen plus neue Integrationstests; keine doppelte NTSC-/CRT-Verarbeitung; alle sieben Presets und Ausgabemodi erhalten. |
| C. Digitalvideo als erstes Nutzerfeature | Vollständige zusätzliche Stufe mit verständlichen Formatvorgaben und einigen Gesamtlooks | Webcam/MiniDV/CD-ROM als konkrete erste Kandidaten; Kamera-/Codec-/Fehlerwerte vollständig speichern, laden und exportieren. Gegen Referenz und Historie prüfen. Zehn UI-Sprachen erhalten; keine übersetzten Originalparameter als bereits vorhanden ausgeben. |
| D. Messung und Freigabe | Reale Quellen, längere Clips, neue/alte Looks, Bild-/Tonzeit und Ressourcen bewerten | Pro Stufe sowie für Pixelumordnung, Pipe-/Kopierweg, Upload/Readback und Gesamtausgabe messen. 720p/1080p, Hochformat, 29,97 fps, Quellen mit Zeitversatz/VFR und lange Seeks einbeziehen. Kein Echtzeitversprechen aus dem 160×120-Testclip. |

Die erste Produktionsintegration umfasst **A bis C mit den nötigen Prüfungen aus D**. Bei nicht erfüllten Grenzen bleibt die neue Kette im Entwicklungsstand; die veröffentlichte Beta.2 wird dadurch nicht automatisch ersetzt. VCR/Glitch und Screen Loop folgen nach der belastbaren normalen Filmkette. Eine neue Oberfläche und eine Linux-Beta sind eigene folgende Aufträge.

## Kompatibilitäts- und Freigabekriterien

1. Bestehende Medien-, Vergleichs-, Sprach-, Größen- und Exportfunktionen bestehen ihre Prüfungen; Bestandslooks ändern ihren Renderer nicht unbemerkt.
2. Digitalvideo/NTSC/CRT als vollständige Kette: fortlaufend, nach Seek/Loop, nach Parameteränderung und im Export mit definiertem Zustand. Historienfehler dürfen keine scheinbar gültige Ausgabe erzeugen.
3. Referenzbilder, Differenzmetriken und Toleranzen **vor** Freigabe eines neuen Referenzsatzes festlegen. Keine allgemeine Bytegleichheit zwischen GPU-Backends behaupten; Unterschiede und lokale Maxima ausweisen.
4. Ungültige Adapter vor der Verarbeitung erkennen und verständlich melden. Softwaremodus ausdrücklich auswählbar und in Credits/Diagnose erkennbar. Windows-Hardware und Mint-Softwaretest getrennt kennzeichnen; native Linux-Hardware bleibt ein zusätzliches Testziel.
5. Begrenzter Cache und keine vollständige dekodierte Videodatei als obligatorische Produktionsvorstufe. Seek-/Historienzugriff, Workerabbruch und temporäre Ausgaben sicher behandeln; vorhandene Quelldateien nie überschreiben.
6. Alle Testquellen und Exportdateien nachvollziehbar, persönliche Medien/Schlüssel/Umgebungsdaten außerhalb öffentlicher Pakete. Kein automatischer Download von Code aufgrund eines Presets.

## Abhängigkeiten und Veröffentlichung

Vorerst die geprüften Pins aus `prototypes/processing-core/upstream-sources.json` und Cargo.lock beibehalten. NTSCRT bleibt klar als Ursprung der übernommenen Module benannt. Eigene Anwendung, Worker und Hostanbindung GPL-3.0-only; Finns übernommene Dateien behalten MIT, alle weiteren Komponenten ihre Lizenzen. Änderungen am librashader-Pin/Patch sind eine eigene Vergleichsaufgabe.

Vor einem neuen öffentlichen Quell-/Binärpaket:

- Quellenbeschaffung, eigene Adapter und Worker in die expliziten Build-/Quellpaketlisten aufnehmen; Originalarchive und Änderungen reproduzierbar referenzieren.
- Lizenz-/Quellhinweise für die tatsächlich gebauten zusätzlichen Abhängigkeiten ergänzen; die Beta.2-Prüfung deckt den neuen statisch gebauten Worker nicht automatisch ab.
- Dokumentationslinks prüfen. `scripts/prepare-github.mjs` und das lokale Paketverfahren berücksichtigen jetzt die eigenen Kernquellen und Patches; Downloads, persönliche Medien und Schlüssel bleiben ausgeschlossen.
- Installer, Laufzeitbibliotheken, Komponentenpfade und Diagnose an den gewählten Worker anpassen. Bestehende externe FFmpeg-Auswahl und ursprüngliche sieben Shader erhalten.

Diese Vorlage ändert keine Version, veröffentlicht nichts und entscheidet noch kein endgültiges Oberflächendesign. [Sechs-Punkte-Plan](ROADMAP.md), [Funktionsvergleich](NTSCRT_COMPARISON.md), [Prototyp](../prototypes/processing-core/README.md).
