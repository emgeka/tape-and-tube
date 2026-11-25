# SPDX-License-Identifier: GPL-3.0-only
# Source-only test kit. Contains no personal media, Windows binaries or credentials.
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$kitRoot = Join-Path $projectRoot ('artifacts/core-prototype/test-kits/' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
$packageRoot = Join-Path $kitRoot 'TapeTube-core-prototype'
$prototypeFiles = @(
    'Cargo.toml','Cargo.lock','upstream-sources.json','prepare.ps1','build.ps1','test.ps1','test.mjs','linux-test.sh','README.md','package-test-kit.ps1',
    'engine-adapter/Cargo.toml','engine-adapter/host-lifecycle.patch','runner/Cargo.toml','runner/src/main.rs','runner/src/worker.rs','runner/src/analog.rs','runner/src/screen_loop.rs','runner/src/imported_look.rs',
    'digital-host/Cargo.toml','digital-host/README.md','digital-host/include/tapetube_digital.h','digital-host/src/lib.rs','digital-host/src/tests.rs',
    'abi-smoke/AbiSmoke.csproj','abi-smoke/Program.cs','abi-smoke/README.md'
)
$copies = @(@{Source='LICENSE';Target='LICENSE'})
$copies += @(@{Source='THIRD_PARTY.md';Target='THIRD_PARTY.md'},@{Source='COPYRIGHT.md';Target='COPYRIGHT.md'},@{Source='licenses/upstream/NTSCRT-MIT.txt';Target='licenses/upstream/NTSCRT-MIT.txt'})
$copies += Get-ChildItem -LiteralPath (Join-Path $projectRoot 'docs') -File | Where-Object {$_.Extension -in @('.md','.json','.jpg')} | ForEach-Object {@{Source="docs/$($_.Name)";Target="docs/$($_.Name)"}}
foreach ($file in $prototypeFiles) { $copies += @{Source="prototypes/processing-core/$file";Target="prototypes/processing-core/$file"} }
$copies += @(
    @{Source='artifacts/core-prototype/downloads/librashader-76462c03.zip';Target='artifacts/core-prototype/downloads/librashader-76462c03.zip'},
    @{Source='.tools/shaders.zip';Target='artifacts/core-prototype/downloads/shaders.zip'}
)
foreach ($copy in $copies) {
    if (-not (Test-Path -LiteralPath (Join-Path $projectRoot $copy.Source) -PathType Leaf)) { throw "Missing test-kit input: $($copy.Source)" }
}
if (Test-Path -LiteralPath $packageRoot) { throw 'Choose a new staging directory; existing test kits are never replaced.' }
New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
foreach ($copy in $copies) {
    $destination = Join-Path $packageRoot $copy.Target
    New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $projectRoot $copy.Source) -Destination $destination
}
& (Join-Path $projectRoot 'scripts/copy-ntscrt-source.ps1') -Destination (Join-Path $packageRoot 'prototypes/processing-core/vendor')
@'
Tape & Tube: Quell-Testpaket fuer den gemeinsamen Verarbeitungskern

Dieses Paket ist ein Prototyp-Test, keine neue App-Version.
In Linux Mint in den persoenlichen Ordner entpacken. Nicht direkt im
VirtualBox-Freigabeordner bauen. Im entpackten Ordner ein Terminal oeffnen:

  bash prototypes/processing-core/linux-test.sh

Voraussetzungen und Hinweise: prototypes/processing-core/README.md
Das Skript prueft Werkzeuge, installiert aber keine Systempakete.
Die verwendeten NTSCRT-Quellen und die anderen Originalarchive sind enthalten.
Cargo benoetigt fuer uncached Abhaengigkeiten weiterhin Internet.
Ergebnisse: artifacts/core-prototype/linux-...

Eigener Code GPL-3.0-only. NTSCRT-Module MIT (Finn McKenty), librashader,
ntsc-rs und Shader behalten ihre eigenen Lizenzen und Originalhinweise.
Die mitgelieferten Quellen und Archive enthalten ihre Originalhinweise.
'@ | Set-Content -LiteralPath (Join-Path $packageRoot 'START-LINUX.txt') -Encoding utf8
$inventory = Get-ChildItem -LiteralPath $packageRoot -File -Recurse | Sort-Object FullName | ForEach-Object {
    [pscustomobject]@{path=[IO.Path]::GetRelativePath($packageRoot,$_.FullName).Replace('\','/');bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}
}
$inventory | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $packageRoot 'source-inventory.json') -Encoding utf8
$archive = Join-Path $kitRoot 'TapeTube-core-prototype-source-test-kit.zip'
Compress-Archive -LiteralPath $packageRoot -DestinationPath $archive -CompressionLevel Fastest
$digest = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
"$digest  $([IO.Path]::GetFileName($archive))" | Set-Content -LiteralPath (Join-Path $kitRoot 'SHA256SUMS.txt') -Encoding ascii
[pscustomobject]@{archive=$archive;sha256=$digest;sourceFiles=@($inventory).Count;bytes=(Get-Item -LiteralPath $archive).Length;includesBinaries=$false} | ConvertTo-Json
