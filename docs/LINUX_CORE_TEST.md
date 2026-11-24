# Linux-Verarbeitungskern — Test vom 17.11.2026

## Ergebnis

Der getrennte Verarbeitungskern wurde nativ in **Linux Mint 22.3 x86-64 in VirtualBox** gebaut und ausgeführt. Digitalvideo → NTSC → CRT → PNG und H.264/AAC-Video funktionieren im getesteten Umfang. Dafür wurde **Mesa-Software-Rendering (llvmpipe/OpenGL)** verwendet. Der normale virtuelle Grafiktreiber dieser VM reicht für die benötigten Compute-Shader nicht aus.

Das ist ein Machbarkeitsnachweis für den gemeinsamen Kern, keine veröffentlichte Linux-App und kein Nachweis für beschleunigten Betrieb auf beliebigen Linux-Grafikkarten. Die Windows-App und die veröffentlichte Beta.2 wurden nicht verändert.

## Umgebung und Prüfungen

- Mint 22.3 (Zena), Kernel 7.0.0-43-generic; sechs CPUs und ca. 15 GiB nutzbares RAM.
- Rust 1.99.1 und .NET SDK 10.0.403 im eigenen Testordner. Systempakete installierte der Nutzer im VM-Terminal; danach normaler Benutzerzugang ohne zusätzliche Administratorfreigabe.
- Testpaket und Originalarchive vor Verwendung per SHA-256 geprüft. Dieselben Quellpins und derselbe librashader-Patch wie unter Windows. FFmpeg aus der Linux-Paketinstallation, Buildinformationen im Ergebnisprotokoll.
- **12/12 native Rust-Tests bestanden:** DCT/Video 1/Datamosh, Historie, Seek/Loop, Stride/Alpha, Fehlererholung und Replaybudget. Der absichtliche Extremseek-Test verlängerte den gesamten Lauf in dieser VM auf ca. 150 s; keine Echtzeitgarantie.
- **433 externe C-ABI-Prüfungen bestanden:** drei Fälle mit je 42 exakten Framevergleichen und 14 erwartete Fehlerfälle, einschließlich Callbackfehlern und Session-Erholung.
- Die drei geprüften Digitalvideo-Bildfolgen (DCT unabhängig, Video 1 vorhergesagt, DCT/Datamosh) sind anhand ihrer SHA-256 **bytegleich zum Windows-Lauf**. Das gilt für diese Fälle, nicht pauschal alle Einstellungen/Architekturen.
- **29 Integrationsprüfungen bestanden:** generierter Clip 160×120, 12 fps, 3 s, Ausgabe 320×240; alle sieben CRT-Presets als Einzelbild, vollständige Kette mit easymode bei Seek/Loop/Auslassen, PNG und H.264/AAC-Video.
- Export: 36 Videoframes; Bild und Ton beginnen jeweils bei 0,000 s und dauern 3,000 s. Testtonposition in Quelle/Export jeweils 1,000 s, Prüftoleranz 10 ms. Keine schwarzen Frames im getesteten Export.

## Grafikbefund

**Standardweg:** SVGA3D, Mesa 25.2.8, OpenGL 4.1 / GLES 3.0. Rendern scheitert ausdrücklich an fehlender Fähigkeit `COMPUTE_SHADERS` im Downscale-Pass. Dieser fehlgeschlagene Versuch ist protokolliert und wird nicht als bestandener Grafiktest gezählt.

**Softwareweg:** Mit `LIBGL_ALWAYS_SOFTWARE=1` meldet Mesa llvmpipe (LLVM 20.1.2), OpenGL 4.5 / GLES 3.2. wgpu klassifiziert das Gerät als `Cpu`. Damit bestehen alle 29 Integrationsprüfungen. Nur die Prozessumgebung des Tests wurde geändert, keine VirtualBox-/Desktop-Einstellung. Der Software-Erfolg belegt keine beschleunigte Vorschauleistung.

## CRT-Vergleich mit identischen Eingabepixeln

Zusätzlich wurde die bereits unter Windows dekodierte RGBA-Datei unverändert in die VM übertragen und ihr SHA-256 geprüft. Unterschiedliche FFmpeg-Versionen/Decoder beeinflussen diesen Vergleich daher nicht. Einstellungen und Original-Framenummer stimmen überein. Windows: wgpu/D3D12 auf NVIDIA GeForce RTX 5060 Ti. Linux: wgpu/OpenGL auf llvmpipe.

Je Preset wurde Frame 0 bei 320×240 verglichen. Absolute RGB-Abweichungen auf der Skala 0–255:

| Preset | Mittelwert über das ganze Bild | Größte einzelne Abweichung |
| --- | ---: | ---: |
| easymode | 0,096 | 2 |
| aperture | 0,121 | 20 |
| hyllian | 0,493 | 12 |
| royale | 0,736 | 17 |
| crtglow_gauss | 0,618 | 9 |
| crtglow_lanczos | 0,627 | 9 |
| crtsim | 0,200 | 2 |

Die Mittelwerte liegen in diesem Vergleich unter einem Farbwert, alle Alphawerte stimmen überein. Die Shaderbilder sind **nicht bytegleich**; kleine Mittelwerte schließen lokale Unterschiede bis 20 Farbwerte nicht aus. Einzelbilder belegen weder alle zeitlichen Shaderzustände noch Gleichheit auf anderen Treibern.

## Nachweise und Wiederholung

Lokale Ergebnisse: `artifacts/core-prototype/linux-mint-20261117/`. Dort liegen Umgebung, CPU-/ABI-Protokolle, beide Grafikversuche, PNGs, Testvideo und `cross-platform-comparison.json`. SHA-256 des unverändert aus der VM übertragenen Ergebnisarchivs:

```text
a902105c04ce6f51eb2519a947b4e0de0500728e6db923633f91c6cefee4f793
```

Quellen und Builds liegen in der VM unter `~/tapetube-core-test-20261117/`. Eine neue vollständige Ausführung mit neuem Ergebnisordner:

```sh
test_root="$HOME/tapetube-core-test-20261117"
export CARGO_HOME="$test_root/.cargo" RUSTUP_HOME="$test_root/.rustup"
export PATH="$CARGO_HOME/bin:$test_root/.dotnet:$PATH"
export DISPLAY=:0 XAUTHORITY="$HOME/.Xauthority"
export XDG_RUNTIME_DIR="/run/user/$(id -u)"
export NTSCRT_BACKEND=gl LIBGL_ALWAYS_SOFTWARE=1
bash "$test_root/TapeTube-core-prototype/prototypes/processing-core/linux-test.sh"
```

## Konsequenz

Der native Linux-Funktionsnachweis ist erbracht. **Rust-/wgpu-Kern mit FFmpeg-Medienweg ist die empfohlene gemeinsame Grundlage** für den weiteren Ausbau. Vor einer Produktionsumstellung bleiben Streaming/Abbruch, vollständiger Farbraum-/Alpha-Vertrag, Hardware-Linux-Leistung, größere/echte Quellen und breitere zeitliche Shadervergleiche zu klären. Oberfläche und Linux-Paketierung werden gesondert entschieden.

Der frühere CRTsim-/VirtualBox-Exportfehler der Windows-App ist durch diesen Softwarelauf weder erklärt noch als dauerhaft behoben nachgewiesen.
