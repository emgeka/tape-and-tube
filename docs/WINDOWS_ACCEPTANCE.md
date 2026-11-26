# Windows-Abnahme für Etappe 5

Stand 24.11.2026. Aktueller Testbuild `20261124-085522-688`. Die bisherige Sichtprüfung mit dem Referenzclip erfolgte in `20261123-153413-457`. Der öffentliche Stand bleibt 0.1.0-beta.2. Für die lokale Abnahme verwenden wir den bereits bereitgestellten Hochkantclip und das CRTsim-Preset aus der früheren Fehleranalyse. Das Material und die daraus erzeugten Dateien bleiben lokal.

## Prüfungen mit realem Material

Der Referenzclip läuft 24,797 Sekunden und enthält 744 H.264-Bilder und eine AAC-Tonspur. Die Quelldatei speichert 1920 × 1080 Pixel mit einer 90°-Drehinformation. Der Export wird mit 960 × 1707 Pixeln angefordert. MP4 ergänzt eine Pixelzeile für gerade Kodiermaße.

| Fall | Ergebnis |
| --- | --- |
| Alter CRTsim-Fehlerfall mit gespeichertem Preset, `animate_artifacts=0` | **Bestanden unter Windows auf dem Hauptrechner.** Der lokale CLI-Export ließ sich vollständig dekodieren. Die Datei enthält 744 Bilder bei 960 × 1708 Pixeln und 24,797 s Video. Gemessen wurden null Bilder mit mindestens 98 % nahezu schwarzen Pixeln. Zwischen Sekunde 14 und 18 lag der größte Sprung der mittleren Bildhelligkeit zwischen zwei Frames bei rund 4 von 255 Stufen. |
| Derselbe Fall mit `animate_artifacts=1` | **Bestanden.** Alle 744 Bilder bei 960 × 1708 Pixeln ließen sich dekodieren. Null nahezu schwarze Bilder nach demselben Kriterium. Der größte Sprung der mittleren Bildhelligkeit zwischen Sekunde 14 und 18 lag ebenfalls bei rund 4 von 255 Stufen. Die AAC-Nutzdaten und die Audiozeitstempel stimmen mit dem ersten Export überein. |
| Tonversatz mit dem gespeicherten Preset | **Bestanden.** Bei Sekunde 2, 10, 16 und 22 ergab der Vergleich mit der Quelltonspur keinen Versatz im Messraster von 0,25 ms. Bild und Ton enden innerhalb von 1 ms. |
| VCR, TV-Empfang und Screen Loop mit dem Referenzmaterial | **Lokale Funktionsprüfung bestanden.** VCR und TV-Empfang erzeugten drei PNGs aus dem Video bei 0, 0,5 und etwa 1 s. Ein Standbild aus dem Clip ergab mit Screen Loop ein dekodierbares GIF mit zwölf Bildern bei 240 × 427 Pixeln. |

Die Helligkeitsmessung erfasst ganze Bilder. Die frühere Störung lässt sich damit objektiv eingrenzen. Kurze örtlich begrenzte Schwankungen und die Wiedergabe in VirtualBox bleiben Gegenstand der Sichtprüfung.

## Sichtprüfung des Testbuilds

| Ablauf | Erwartetes Ergebnis |
| --- | --- |
| Hochkantclip in der App öffnen, CRTsim-Preset laden, vor Sekunde 16 pausieren und danach weiterlaufen lassen. Kurzen Kontroll-Export abspielen. | Hochkantlage stimmt. Bild und Ton laufen synchron. Um Sekunde 16 fehlen weder einzelne Bilder noch längere Bildabschnitte. |
| Beim pausierten Clip zwischen MiniDV und VCR/TV-Empfang wechseln, anschließend vor- und zurückspringen. | Die Änderung erscheint am aktuellen Frame. Die vorbereiteten Vorschauframes zeigen beim Fortsetzen den aktuellen Look. |
| Bei geöffnetem Material die Sprache wechseln und Beschriftungen, Listen und Tooltips etwa für TV-Empfang und Screen Loop ansehen. **Ausgabe** neben den Speichertasten bei kleiner Fenstergröße öffnen. | Die aktuellen Effektwerte bleiben erhalten. Die Effekttexte wechseln mit. Die Ausgabeeinstellungen bleiben erreichbar und die Formathilfe nennt PNG, MP4 (H.264 oder HEVC) und GIF. |
| Screen Loop für ein eigenes Standbild aktivieren und die beiden Griffe bewegen. Danach einen kurzen Export öffnen. | Fluchtpunkt und Bewegungsziel reagieren in der Vorschau. Der Export übernimmt den gewählten Bewegungsverlauf. |
| Einen animierten NTSCRT-Look importieren, Anfang und Ende vergleichen und einen kurzen Export ansehen. | Der Look bleibt importiert. Die sichtbaren Übergänge treten an den erwarteten Positionen auf. |

Am 24.11.2026 hat der Nutzer den Test mit dem Hochkantclip und dem CRTsim-Preset sowie den Effektwechsel bei pausierter Vorschau im Build `20261123-153413-457` als erfolgreich bestätigt.

Die Sichtprüfung mit `20261124-085522-688` bestätigte den Wechsel von der Systemsprache zu Französisch bei geöffnetem Material. Vorschau und Griffpositionen blieben erhalten. Der Tooltip am Regler für die Signalstärke erschien auf Französisch. Die obere Schaltfläche **Sortie** öffnete die Ausgabeeinstellungen auch bei Mindestbreite.

Auf einem Standbild aus dem Referenzvideo ließen sich beide Screen-Loop-Griffe bewegen. Der Export folgte dem gewählten Verlauf. Der animierte NTSCRT-Look **5 second blips** ließ sich importieren und zwischen Anfang und Ende vergleichen. Beide über die App exportierten MP4-Dateien ließen sich vollständig dekodieren. Jede Datei umfasst 150 H.264-Bilder mit 320 × 568 Pixeln und einer Laufzeit von fünf Sekunden. Eine Stichprobe von sechs Bildern pro Datei zeigte den erwarteten Bewegungsverlauf beziehungsweise die wechselnden Störungen. Die Testdateien liegen lokal unter `artifacts/windows-acceptance/20261124-085522-688`.

Zum Abschluss die konkrete Installer-Fassung in einer frischen Windows-11-VM installieren. Dabei die Auswahl für FFmpeg, die Erkennung der übrigen Laufzeitkomponenten, den Start von Tape & Tube und einen kurzen Export mit Ton prüfen. Die bisherige VM-Abnahme der Beta.2 gilt für deren Installer.

Am 24.11.2026 wurde der lokale Windows-Installer für **0.1.0-beta.3** unter `artifacts/releases/20261124-090708-170` gebaut. Die Prüfsummen für Installer und Begleitarchive stimmen. Das zugehörige Paket bestand mit dem im Setup vorgesehenen FFmpeg-Paar 9.0.2 insgesamt 239 Prüfungen in einem getrennten Verzeichnis, darunter der Export mit zeitversetztem Originalton. Mit dem lokal über PATH gefundenen FFmpeg-Nightly `N-128251-g6d1bfd7609-20261031` überschritt derselbe Exporttest die Wartezeit von drei Minuten. Die Ursache ist offen.

Der Nutzer bestätigte die manuelle Installation dieses Kandidaten in der Windows-VM. Dabei fielen englische Texte in der Übersicht und der erst während der Installation beginnende FFmpeg-Download auf. Die überarbeitete Fassung zeigt die Übersicht in der gewählten Setupsprache und bereitet FFmpeg auf der Komponentenseite vor. Ein erneuter VM-Test soll den geänderten Ablauf prüfen.

Auf dem Hauptrechner erschien die deutsche Übersicht im Setup. Mit ausgewähltem FFmpeg-Download führte **Weiter** durch den Download und die Prüfsummenprüfung, bevor das Setup die nächste Seite anzeigte. Beim Zurückgehen zeigte die Komponentenseite **Geprüft und zum Kopieren bereit**. Der Test wurde vor der Installation beendet.

Der neue Beta.3-Kandidat mit dem App-Icon und der dazu passenden Setup-Startseite liegt unter `artifacts/releases/20261124-093128-460`. Die Prüfsumme des Installers stimmt. Das zugehörige Windows-Paket bestand erneut 239 Prüfungen mit FFmpeg 9.0.2.

Beim anschließenden App-Test in der Windows-VM meldete der Nutzer, dass der für den gemeinsamen Renderer ausgewählte Grafikadapter keine Compute-Shader unterstützt. Der Befund betrifft die Renderinitialisierung. Die grafikunabhängige Prüfung des Installers und FFmpegs bleibt über einen einfachen Export ohne Effekte möglich. Auf dem Hauptrechner ließ sich mit dem Paket aus demselben Installer ein dreisekündiger Ausschnitt des Referenzclips mit dem früheren CRTsim-Preset vollständig als H.264/AAC exportieren und dekodieren.

Der Nutzer bestätigte am 24.11.2026 den MP4-Kontrolltest mit einem Clip mit Ton und ausgeschalteten Effekten in der Windows-11-VM. Der gemeinsame Renderer bleibt mit dem dort ausgewählten virtuellen Grafikadapter wegen fehlender Compute-Shader offen. Für die Beta-Hinweise die bestätigte VM-Funktion und die Grafikanforderung für die erweiterten Effekte aufführen.

## Prüfung vor dem nächsten Beta-Installer

Die folgenden Befunde stammen aus der ersten Durchsicht des Entwicklungsstands vom 23.11.2026. Der aktuelle Stand der Übersetzungen und Ausgabeoptionen folgt darunter.

- **Übersetzungen.** Der Test des vorhandenen Builds besteht alle 96 Prüfungen. Im Quellkatalog sind 262 Texte je Sprache vorhanden. Alle 13 Tooltip-Ressourcen des Hauptfensters sind in den zehn Sprachen abgedeckt. Außerhalb dieses Prüfbereichs liegen 43 englische Hilfetexte zu VCR und TV-Empfang (30 Regler, acht Band- und fünf Gerätezustände) sowie 23 zu Screen Loop. Diese Texte werden direkt aus den Katalogen übernommen. Auch NTSC-Regler samt Auswahlwerten und Schalterhilfen sowie 32 Digitalvideo-Parameter und deren Auswahlwerte umgehen die Übersetzung. Die Sprachumschaltung aktualisiert bisher vor allem XAML-Ressourcen und baut die Effektfelder nicht neu auf. Eigene englische Fehlermeldungen gelangen über `ex.Message` in die Statusleiste. Bei der Korrektur sollten Übersetzungen anhand stabiler IDs beim Anzeigen der Regler angewendet und im geöffneten Fenster nach Sprachwechsel geprüft werden. Eigennamen, technische Bezeichnungen und Preset-Dateinamen bleiben als solche erkennbar.
- **Bedienung.** Laden und Speichern sind gemeinsam oben erreichbar. Die Exportoptionen liegen im Ausgabetab, das Format wird beim Speichern gewählt. Ein direkter Weg von den oberen Speichertasten zu den Optionen würde Größe und Qualität leichter auffindbar machen. Die Hilfe im Ausgabetab nennt weiterhin nur PNG und MP4. Der Bildexport bietet PNG, der Videoexport bietet H.264, HEVC und animiertes GIF. Quellbeschnitt, interne Verkleinerung, CRT-Skalierung und Scanline-Raster erfüllen unterschiedliche Aufgaben. Die Zuordnung lässt sich durch klare Gruppen für Quelle, Effektverarbeitung und Ausgabe sowie eine unmittelbar sichtbare Zielgröße erklären. Für die Vorschau bietet sich eine gemeinsame Auswahl für Ausgabe, Original und Vergleich an. Die beiden derzeitigen Schalter schließen sich gegenseitig aus. Screen Loop sollte Digitalvideo als vorübergehend ausgesetzt kennzeichnen, wobei der gewählte Zustand sichtbar bleibt. Mindestfenstergröße, hohe Windows-Skalierung und längere Übersetzungen sind visuell noch zu prüfen.
- **Verarbeitung.** Bei Rücksprüngen beginnt der gemeinsame Kern seinen Verlauf bei Frame 0 erneut. Der FFmpeg-Decoder startet bei Bedarf ebenfalls neu und überspringt bis zur Zielposition per Filter. Der schnelle Zwischenstand beim pausierten Effektwechsel verbessert die Reaktion, die genaue Berechnung bleibt danach erforderlich. Ein Profil mit einem späten Sprung in einem längeren Clip sollte getrennt Decodierung und Rekonstruktion messen. Effekte mit zeitlicher Abhängigkeit brauchen ihre Vorgeschichte. Der Worker verarbeitet pro Frame außerdem volle Rohbildpuffer über Prozessgrenzen und kopiert sie beim Farbkanaltausch. Die unveränderte Renderkonfiguration wird pro Frame erneut aufgebaut und verglichen. Bei skalierenden Ausgabelayouts werden die unveränderten Filtergewichte pro Frame neu angelegt. Diese Arbeit ist im Code sichtbar. Ihr Anteil an der Laufzeit ist noch nicht gemessen.

Für die nächste Abnahme bieten sich zuerst die sichtbaren Übersetzungslücken und die Orientierung im Ausgabebereich an. Leistungsmessungen lassen sich mit dem vorhandenen Build vorbereiten. Änderungen am zeitabhängigen Renderpfad brauchen erneut den Exportvergleich mit CRTsim sowie eine Prüfung mit animierten Looks.

Seit der Überarbeitung vom 24.11.2026 führt **Ausgabe** von der Medienleiste direkt zu den Ausgabeeinstellungen. Die Formathilfe nennt die tatsächlich angebotenen Formate. Die 331 unterschiedlichen Beschriftungen, Auswahlwerte und Hilfetexte der mitgelieferten NTSC-, Digitalvideo-, Analog- und Screen-Loop-Kataloge liegen in allen zehn Sprachen vor. Englisch verwendet die Originaltexte. Der Sprachtest prüft die Kataloge sowie die Live-Aktualisierung eines Effekt-Schalters mit Tooltip beim Sprachwechsel. Fremde Shadertexte und technische Fehlerdetails können weiterhin in der Originalsprache erscheinen.

Im aktuellen Testbuild sind 118 Sprachprüfungen, 43 Ausgabeprüfungen und 193 Integrationsprüfungen bestanden. Die Integrationstests verwenden Testmaterial und decken Screen Loop sowie importierte NTSCRT-Looks ab.

## Verbleibende Automatisierung

- **Bestanden am 24.11.2026** mit Windows-Testbuild `20261124-135732-823` und FFmpeg 9.0.2. Die 17 CLI-Prüfungen decken unter anderem PNG-Einzelbilder und Serien, H.264/HEVC mit AAC und GIF ab. Der MCP-Test bestätigt Initialisierung, Werkzeugliste, beschnittenes PNG, wiederholten HEVC-Export, Überschreibschutz und Abbruch.
- Fortschritt und geschätzte Restzeit über CLI sowie Statusabfragen über MCP gewinnen Priorität, wenn längere Aufträge im verwendeten Client unklar bleiben oder abbrechen.
- **Für einen späteren Ausbau vormerken:** ein eigener MCP-Vorschauaufruf, Look-Liste und Speichern von Looks über CLI und MCP. PNG-Vorschau, Preset-Dateien und der Effektkatalog decken den grundlegenden Workflow bereits ab. Importierte NTSCRT-Looks folgen weiterhin dem vereinbarten schreibgeschützten Ablauf.

Der Funktionsabgleich bezieht sich auf die Windows-Referenz NTSCRT 0.28.1. ProRes und der separate Viewport-Export aus der allgemeinen NTSCRT-Dokumentation gehören in dieser Referenz nicht zum Windows-Speicherdialog. [Windows-Abgleich](NTSCRT_COMPARISON.md#verbleibende-windows-lücken-am-23112026)

## Beta.4-Kandidat vom 24.11.2026

Installer und passendes Quellpaket liegen unter `artifacts/releases/20261124-145413-788`. Alle drei lokalen SHA-256-Prüfsummen stimmen. Das portable Paket bestand 268 Prüfungen in einem getrennten Verzeichnis mit FFmpeg 9.0.2. Die Prüfung erfasst unter anderem die Exportformate, die 49 Original-Presets sowie die mitgeführten NTSCRT-Dateien im Quellpaket.

Der Installationstest auf dem Entwicklungsrechner bestand mit vorhandenem FFmpeg 9.0.2. 1.032 installierte Dateien und das beigefügte Quellpaket stimmen mit den Kandidatendateien überein. Erneute Installation und Deinstallation ließen eigene Dateien bestehen. Die gespeicherte FFmpeg-Auswahl wurde übernommen und ein absichtlich fehlendes ausgewähltes Werkzeug wurde gemeldet. Die installierte App bestand 16 Effekt-, 17 Video- und 45 Exportprüfungen. Nach dem Test waren Programmdatei und Deinstallationseintrag entfernt. Der Test umfasste die Installation mit vorhandenen Laufzeitkomponenten und ohne FFmpeg-Download.

Ein zweiter Test mit demselben Installer bestätigte den optionalen Download von FFmpeg 9.0.2 und dessen SHA-256-Prüfung. Installation, erneute Installation, Video-/Exportprüfungen und Deinstallation waren erfolgreich. Beide Tests liefen mit vorhandenen Microsoft-Laufzeitkomponenten. Die Ergebnisse stehen in `installer-validation-existing.json` und `installer-validation-download.json` im Kandidatenordner. Die für GitHub vorgesehenen Dateien sind lokal unter `artifacts/github/v0.1.0-beta.4` zusammengestellt.

## Lokale Paketabnahme vom 26.11.2026

Der Entwicklungsstand mit Vergleichsexport, Vorschauton und korrigiertem Effektwechsel wurde unter `artifacts/releases/20261126-093903-237` als lokaler Entwurf gepackt. Der Entwurf trägt die bestehende Versionskennung 0.1.0-beta.4. Alle drei SHA-256-Prüfsummen stimmen. Das portable Paket bestand in einem separaten Verzeichnis 302 Prüfungen mit FFmpeg 9.0.2, darunter 79 Exportprüfungen und 121 Sprachprüfungen. Eine vorhandene Windows-Installation wurde für die Prüfung nicht angetastet. Der Installationslauf auf einem frischen System steht für diesen Entwurf aus.

Ein separater Größenvergleich mit gleichem Setup-Rezept ergab 66.711.594 Byte mit gebündelter Laufzeit und 22.064.385 Byte mit externer .NET-Laufzeit. Die App der Messfassung bestand 16 Effektprüfungen mit der bereits installierten x64-Desktoplaufzeit 10.0.15. Die .NET-Prüfseite des schlanken Installers und die Nachinstallation einer fehlenden Laufzeit stehen aus. Die Messdatei ist für den Größenvergleich vorgesehen.
