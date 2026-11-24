# Digitalvideo in der Windows-App

Lokaler Entwicklungsstand vom 17.11.2026, ausdrücklich vom Nutzer beauftragt. Die veröffentlichte 0.1.0-beta.2 ist davon unabhängig.

## Bedienung

Unter **Effekte → Digitalvideo → Details** stehen **Webcam (2005)**, **Video Call**, **MiniDV**, **Early Web**, **CD-ROM (Video 1)** und **Datamosh** bereit. Die Auswahl aktiviert Digitalvideo und übernimmt Kamera, Codec und die passende interne Breite aus NTSCRT. NTSC/VHS, CRT, der Skalierungsfaktor sowie benutzerdefinierte Ausgabegröße und Beschnitt bleiben erhalten. Bei „Wie Vorschau“ können sich die Ausgabemaße durch die neue interne Breite ändern.

**Digitalvideo** schaltet die Kamerastufe unabhängig zu. Unter **Details** sind alle 32 Parameter aus NTSCRT 0.28.1 erreichbar. Deren Originalnamen bleiben wie bei den NTSC-Details unverändert. Gespeicherte Presets bündeln die gesamte Kette.

PNG nutzt die fertig berechnete Vorschau. Videoexport rendert jedes Frame in einer eigenen Worker-Instanz und übernimmt den Originalton. Bestehende Ausgabeformate, Fit/Crop, Vergleichsschieber und die sieben CRT-Presets bleiben verfügbar.

## Verarbeitung und Kompatibilität

- Neue Tape-&-Tube-Presets mit Digitalvideo verwenden `ntscrt-wgpu-v2` und speichern die 32 Werte. Der gemeinsam genutzte Effektkern unterstützt NTSCRT-Look-Dateien der Versionen 1 bis 3.
- Die neue Kette ist Digitalvideo → ntsc-rs → GPU-Downscale → VCR → TV-Empfang → CRT → Ausgabegeometrie. Jede Effektstufe ist einzeln zuschaltbar. NTSC und CRT werden jeweils einmal ausgeführt. Ein altes Preset stellt den darin vorgesehenen Renderer wieder her.
- Pro Vorschau/Export läuft ein dauerhafter Hintergrundprozess. Auch Änderungen und Rücksprünge behalten dessen Grafikgerät; zeitlicher Codec-/Shaderzustand wird ausdrücklich zurückgesetzt. Verpasste Frames werden mit echten früheren Quellbildern nachgerechnet, nicht mit Duplikaten des Zielbilds.
- Der Adapter reicht gepacktes RGBA8 über begrenzte Binärpakete weiter; die Windows-Seite wandelt BGRA/RGBA genau an der Prozessgrenze um. Kein vollständiges Rohvideo muss vorher auf Platte geschrieben werden. Eingabe-/Ausgabelimit: 24 Megapixel; Ausgabemaße weiter maximal 8192.
- Lange Sprünge können langsam sein: korrekte zeitliche Rekonstruktion beginnt derzeit bei Frame 0. Abbruch beendet den Worker; der nächste Auftrag startet mit sauberem Zustand. Kein Echtzeitversprechen.
- Seit dem 20.11.2026 wandelt der Videoimport PQ (HDR10) und HLG vor den Effekten per Tone-Mapping nach SDR BT.709 um. Vorschau, Positionssprünge und Export nutzen dieselbe Konvertierung. Der Fenstertitel zeigt dabei „HDR → SDR“. Getaggtes BT.2020-SDR wird ebenfalls nach BT.709 konvertiert. FFmpeg benötigt hierfür `zscale` und bei HDR `tonemap`. Dolby Vision benötigt eine kompatible Basisspur und verwendet diese ohne dynamische Dolby-Vision-Anpassungen. Bilddateien verwenden den bisherigen 8-Bit-Bildadapter.

## Videorekorder und TV-Empfang

Unter **Effekte** lassen sich **Videorekorder (VCR)** und **TV-Empfang (Glitch)** unabhängig aktivieren. Der VCR bietet Finn McKentys acht Band- und fünf Deckzustände sowie die Originalregler für Band, Abspielgerät und Empfang. Ein Band- oder Deckzustand ändert die zugehörigen Regler. Die Beschriftungen der Stufen sind lokalisiert, die Detailregler und Namen der Vorgaben verwenden die englischen Originalbezeichnungen.

**Andere Kassette** behält den Bandzustand und wählt eine andere Verteilung der Bandfehler. Die Nummer bezeichnet die Variante. **Kassette zurücksetzen** stellt Variante 1 wieder her. **TV-Empfang** hat neutrale Anfangswerte. Für einen sichtbaren Effekt beispielsweise `Signal strength` verringern oder `Vertical hold` verstellen.

Presets mit VCR- oder Empfangseinstellungen speichern beide Schalter und alle Werte als Tape-&-Tube-Preset der Version 3. Bestehende Presets der Versionen 1 und 2 lassen sich weiter laden. Die Vorschau und der Export verwenden die NTSCRT-Simulation mit dem Zeitpunkt des jeweiligen Quellbilds.
- Ein geeigneter Compute-Shader-Grafikadapter ist erforderlich. Softwarebetrieb nur ausdrücklich über `NTSCRT_ALLOW_SOFTWARE=1` (oder den bestehenden Linux-Mesa-Testmodus); keine stille Softwareumschaltung. GPU/Backend stehen in Komponenten & Credits.

## Austauschbare Bestandteile und Herkunft

Standardpfad ist `core/TapeTube.Core.exe` neben der App. Ein alternatives kompatibles Programm lässt sich über `coreWorker` in `components.local.json` oder `TAPETUBE_CORE_WORKER` setzen. Protokollversion und NTSCRT-Referenzstand werden vor Verarbeitung geprüft. JSON-Metadaten maximal 1 MiB, Binärteil maximal 96 MB, Framenummer und Quellkennung werden auf beiden Seiten geprüft. Das Protokoll ist eine lokale Prozessschnittstelle, kein Netzwerkdienst.

NTSCRT von Finn McKenty liefert die tatsächlichen MIT-lizenzierten Digitalvideo- und Engine-Module sowie die Formatwerte. Eigener Worker und Windows-Host: GPL-3.0-only. Die beiden Adapterpatches, Quellen und Prüfsummen sind im [Quellmanifest](../prototypes/processing-core/upstream-sources.json) dokumentiert. Zusätzliche MinGW-Laufzeiten und deren Hinweise gehören zum lokalen Worker-Build. Die frühere librashader-DLL bleibt erhalten. Details: [THIRD_PARTY.md](../THIRD_PARTY.md).

## Prüfen

```powershell
./scripts/build.ps1 -Offline
./scripts/test-digital.ps1
```

`-Offline` setzt bereits beschaffte, geprüfte Quellen und Abhängigkeiten voraus. Ohne diesen Schalter erfolgt die vorhandene Quellenbeschaffung. Die Referenz-CLI und C-ABI-Tests bleiben in `prototypes/processing-core` erhalten.

Der neue Integrationstest prüft echte MP4-Frames, bytegenaue Seek-/Loop-/Skip-Ergebnisse aller sieben Shader, fortbestehenden Prozess, Quellenwechsel, Abbruch/Wiederanlauf, Katalogvalidierung und drei vollständige H.264/AAC-Exporte gegen die Vorschau. Berichte liegen lokal unter `artifacts/digital-integration`. Zusätzliche große Quellen und Laufzeitmessungen werden separat dokumentiert; diese Tests ersetzen keine Linux-App-Freigabe oder neue Installer-Abnahme.

Vor öffentlicher Binärweitergabe sind die zusätzliche native Abhängigkeitsinventur und das neue Paket zu prüfen. Quellpaketlisten, Build und lokales Paketverfahren berücksichtigen den Worker bereits. Diese Implementierung veröffentlicht nichts und ändert die Releaseversion nicht.

## Ausgeführte Abschlussprüfung am 17.11.2026

- Windows-Hardware: NVIDIA GeForce RTX 5060 Ti, Vulkan. **76 neue Integrationsprüfungen bestanden**, zusätzlich alle **199 bisherigen App-Prüfungen** (Effekte, Medien, Export, Ausgabegeometrie, zehn Sprachen und Updatekanäle).
- Vollständige Kette Digitalvideo + NTSC + CRT über zwölf echte Quellframes bytegenau gegen die beibehaltene Referenz-CLI verglichen. Auch alte Version-1-Presets ohne neue Felder sowie vollständige Version-2-Looks wurden geprüft.
- Zusätzliche Medienfälle: 1280×720 und 1920×1080 mit 30000/1001 fps; 1080×1920 mit variablen Zeitabständen, Beschnitt und 960×1707-Ausgabe, für H.264 auf 1708 Zeilen ergänzt. Alle Exportframes stimmen vor der Kompression mit der Vorschau überein.
- CRTsim über 24 Sekunden/288 Frames: Vorschau und Export stimmen an ausgewählten Positionen vor, bei und nach 16 Sekunden bytegenau überein. Kein vollständig schwarzes berechnetes Frame; das kodierte Bild ist auch bei 17 Sekunden sichtbar. Dies ist ein Windows-Hardware-Test, keine erneute VirtualBox-Abnahme.
- Prozess bleibt bei Quellen-/Lookwechseln, allen sieben Shadern und Sprüngen bestehen. Fehlende Historie, Abbruch und unerwartetes Prozessende erzeugen keinen scheinbar gültigen Ersatzframe; Wiederanlauf geprüft. Ein ausgeschalteter Effektweg verändert die Quelle auch bei abweichender gespeicherter Retrobreite nicht.
- Messprotokoll: `artifacts/digital-integration/media-measurements.json`. Erfasst werden gesamte Frameverarbeitung einschließlich Kanaltausch, IPC, CPU-/GPU-Stufen und Komposition sowie Exportdauer und Worker-Arbeitsspeicher. Einzelfallzeiten sind keine isolierten Stufenprofile und kein Echtzeitversprechen; die großen synthetischen Clips sind jeweils nur etwa eine halbe Sekunde lang.
- Lokale Quellpaketprüfung: explizite Dateiliste, Dokumentationslinks, zehn Sprachkataloge und Ausschluss von Medien, Werkzeugcaches und Schlüsseln erfolgreich. Für den zusätzlichen Worker wurden Cargo-Metadaten und vorhandene Originalhinweise von 235 Abhängigkeitseinträgen gesammelt; offene native Hinweise bleiben vor einer neuen Binärveröffentlichung zu bewerten.
