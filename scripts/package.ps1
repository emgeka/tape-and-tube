# SPDX-License-Identifier: GPL-3.0-only
param([switch]$Offline)
. "$PSScriptRoot/environment.ps1"
$Metadata = Get-Content -LiteralPath (Join-Path $ProjectRoot 'release.json') -Raw | ConvertFrom-Json
$Stamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
$ReleaseDirectory = Join-Path $ProjectRoot "artifacts/releases/$Stamp"
$PackageName = "TapeTube-$($Metadata.version)-win-x64-REVIEW"
$PackageDirectory = Join-Path $ReleaseDirectory $PackageName
$SourceDirectory = Join-Path $ReleaseDirectory "TapeTube-$($Metadata.version)-source"
New-Item -ItemType Directory -Force $PackageDirectory,$SourceDirectory | Out-Null
Push-Location $ProjectRoot
try {
    $CargoArguments = @('build','--release','--locked','--manifest-path','native/Cargo.toml')
    if ($Offline) { $CargoArguments += '--offline' }
    & $Cargo @CargoArguments
    Assert-NativeExit 'Native Bibliothek bauen'
    Copy-Item -LiteralPath 'native/target/release/ntscrt_ntsc.dll' -Destination 'runtime/ntscrt_ntsc.dll' -Force
    $PublishArguments = @('publish','src/Ntscrt.Windows/Ntscrt.Windows.csproj','-c','Release','-r','win-x64','--self-contained','true',
        '--output',$PackageDirectory,'-p:PublishSingleFile=false','-p:PublishTrimmed=false','-p:DebugType=None','-p:DebugSymbols=false')
    if ($Offline) { $PublishArguments += '--no-restore' }
    & $Dotnet @PublishArguments
    Assert-NativeExit 'Portable Windows-Ausgabe bauen'
    foreach ($File in @('release.json','dependencies.json','components.json','LICENSE','COPYRIGHT.md','THIRD_PARTY.md','RELEASE_REVIEW.md')) {
        Copy-Item -LiteralPath $File -Destination $PackageDirectory
    }
    Copy-Item -LiteralPath 'RELEASE_README.md' -Destination (Join-Path $PackageDirectory 'README.md')
    if (Test-Path -LiteralPath 'docs/screenshot.jpg') {
        New-Item -ItemType Directory -Force (Join-Path $PackageDirectory 'docs'),(Join-Path $SourceDirectory 'docs') | Out-Null
        Copy-Item -LiteralPath 'docs/screenshot.jpg' -Destination (Join-Path $PackageDirectory 'docs/screenshot.jpg')
        Copy-Item -LiteralPath 'docs/screenshot.jpg' -Destination (Join-Path $SourceDirectory 'docs/screenshot.jpg')
    }
    New-Item -ItemType Directory -Force (Join-Path $PackageDirectory 'runtime/media') | Out-Null
    Set-Content -LiteralPath (Join-Path $PackageDirectory 'runtime/media/README.txt') -Value 'Optional video tools: place ffmpeg.exe and ffprobe.exe here. They are not included in this local review package.' -Encoding utf8
    & node 'scripts/collect-shaders.mjs' 'assets/slang-shaders-cb01f2f238700eed7ec1c859be1737a9fbcdcb7c' (Join-Path $PackageDirectory 'assets/slang-shaders-selected')
    Assert-NativeExit 'Shader-Dateien sammeln'
    $ShaderAudit = Get-Content -Raw -Encoding UTF8 -LiteralPath 'docs/shader-license-audit.json' | ConvertFrom-Json
    foreach ($File in $ShaderAudit.files) {
        $Asset = Join-Path $PackageDirectory ('assets/slang-shaders-selected/' + $File.path)
        if ((Get-FileHash -LiteralPath $Asset -Algorithm SHA256).Hash.ToLowerInvariant() -ne $File.sha256) {
            throw "Shader-Prüfbericht passt nicht zur Datei: $($File.path)"
        }
    }
    New-Item -ItemType Directory -Force (Join-Path $PackageDirectory 'licenses') | Out-Null
    Copy-Item -LiteralPath 'docs/shader-license-audit.json','docs/shader-license-evidence.json','docs/librashader-release-evidence.json','docs/SHADER_LICENSE_REVIEW.md','docs/SHADER_LICENSE_REQUEST.md','docs/DEPENDENCY_LICENSE_REVIEW.md','docs/EXPORT_FLICKER_INVESTIGATION.md','docs/INSTALLER.md' -Destination (Join-Path $PackageDirectory 'licenses')
    $LibraEvidence = Get-Content -Raw -LiteralPath 'docs/librashader-release-evidence.json' | ConvertFrom-Json
    foreach ($Dll in @('runtime/librashader.dll',(Join-Path $PackageDirectory 'librashader.dll'))) {
        if ((Get-FileHash -LiteralPath $Dll -Algorithm SHA256).Hash.ToLowerInvariant() -ne $LibraEvidence.dllSha256) { throw 'librashader DLL passt nicht zum Herkunftsnachweis.' }
    }

    $CargoMetadataArguments = @('metadata','--format-version','1','--locked','--filter-platform','x86_64-pc-windows-gnu','--manifest-path','native/Cargo.toml')
    if ($Offline) { $CargoMetadataArguments += '--offline' }
    $NativeMetadata = & $Cargo @CargoMetadataArguments
    Assert-NativeExit 'Rust-Abhängigkeiten lesen'
    New-Item -ItemType Directory -Force 'artifacts/packaging' | Out-Null
    $MetadataFile = Join-Path $ProjectRoot "artifacts/packaging/native-metadata-$Stamp.json"
    $NativeMetadata | Set-Content -LiteralPath $MetadataFile -Encoding utf8
    & node 'scripts/collect-notices.mjs' $ProjectRoot (Join-Path $PackageDirectory 'licenses') $MetadataFile
    Assert-NativeExit 'Lizenzhinweise sammeln'
    $UpstreamSources = Join-Path $ProjectRoot 'artifacts/packaging/upstream'
    if (Test-Path -LiteralPath $UpstreamSources) {
        New-Item -ItemType Directory -Force (Join-Path $PackageDirectory 'sources') | Out-Null
        Get-ChildItem -LiteralPath $UpstreamSources -File | Copy-Item -Destination (Join-Path $PackageDirectory 'sources')
        $LibraArchive = Join-Path $UpstreamSources 'librashader-v0.12.0-source.zip'
        if (Test-Path -LiteralPath $LibraArchive) {
            Add-Type -AssemblyName System.IO.Compression.FileSystem
            $Archive = [IO.Compression.ZipFile]::OpenRead($LibraArchive)
            try {
                $LicenseDirectory = Join-Path $PackageDirectory 'licenses/librashader'
                New-Item -ItemType Directory -Force $LicenseDirectory | Out-Null
                foreach ($Entry in $Archive.Entries) {
                    if ($Entry.FullName -match '^[^/]+/(LICENSE(?:-GPL)?\.md)$') {
                        [IO.Compression.ZipFileExtensions]::ExtractToFile($Entry,(Join-Path $LicenseDirectory $Matches[1]),$true)
                    }
                }
                Copy-Item -LiteralPath '.tools/librashader/librashader.h' -Destination $LicenseDirectory
            } finally { $Archive.Dispose() }
        }
    }

    # Explicit allowlist: never package caches, personal configuration, media or build outputs as source.
    foreach ($File in @('README.md','RELEASE_README.md','RELEASE_REVIEW.md','RELEASE_NOTES.md','CHANGELOG.md','CONTRIBUTING.md','PUBLICATION.md','THIRD_PARTY.md','COPYRIGHT.md','LICENSE','dependencies.json','components.json','release.json','global.json','.gitignore','Start.ps1')) {
        Copy-Item -LiteralPath $File -Destination $SourceDirectory
    }
    foreach ($Directory in @('src/Ntscrt.Windows','native/src','scripts')) {
        $Destination = Join-Path $SourceDirectory $Directory
        New-Item -ItemType Directory -Force $Destination | Out-Null
        Get-ChildItem -LiteralPath $Directory -File | Where-Object { $_.Extension -in @('.cs','.rs','.xaml','.csproj','.ps1','.mjs','.json') } |
            Copy-Item -Destination $Destination
    }
    New-Item -ItemType Directory -Force (Join-Path $SourceDirectory 'native') | Out-Null
    Copy-Item -LiteralPath 'native/Cargo.toml','native/Cargo.lock' -Destination (Join-Path $SourceDirectory 'native')
    Copy-Item -LiteralPath '.github' -Destination (Join-Path $SourceDirectory '.github') -Recurse
    Get-ChildItem -LiteralPath 'docs' -File -Filter '*.md' | Copy-Item -Destination (Join-Path $SourceDirectory 'docs')
    Copy-Item -LiteralPath 'docs/shader-license-audit.json','docs/shader-license-evidence.json','docs/librashader-release-evidence.json' -Destination (Join-Path $SourceDirectory 'docs')
    Copy-Item -LiteralPath 'licenses' -Destination (Join-Path $SourceDirectory 'licenses') -Recurse
    Copy-Item -LiteralPath 'installer' -Destination (Join-Path $SourceDirectory 'installer') -Recurse
    $SourceArchive = Join-Path $ReleaseDirectory "TapeTube-$($Metadata.version)-source.zip"
    Compress-Archive -LiteralPath $SourceDirectory -DestinationPath $SourceArchive -CompressionLevel Optimal
    Set-Content -LiteralPath (Join-Path $PackageDirectory 'SOURCES.txt') -Encoding utf8 -Value "Own application source: $(Split-Path $SourceArchive -Leaf). Supplied next to this review ZIP. Original code is GPL-3.0-only; see COPYRIGHT.md. Upstream versions and source locations are in dependencies.json and licenses/dependency-inventory.json."
    $PackageArchive = Join-Path $ReleaseDirectory "$PackageName.zip"
    Compress-Archive -LiteralPath $PackageDirectory -DestinationPath $PackageArchive -CompressionLevel Optimal
    $Manifest = [ordered]@{name=$Metadata.name;version=$Metadata.version;status=$Metadata.status;channel=$Metadata.channel;publicReleaseApproved=$Metadata.publicReleaseApproved;
        package=Split-Path $PackageArchive -Leaf;source=Split-Path $SourceArchive -Leaf;
        ffmpegBundled=$false;selfContained=$true;shaderFiles=(Get-ChildItem -LiteralPath (Join-Path $PackageDirectory 'assets') -Recurse -File).Count;
        artifacts=@(@{file=Split-Path $PackageArchive -Leaf;sha256=(Get-FileHash -LiteralPath $PackageArchive -Algorithm SHA256).Hash},
                    @{file=Split-Path $SourceArchive -Leaf;sha256=(Get-FileHash -LiteralPath $SourceArchive -Algorithm SHA256).Hash})}
    $Manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $ReleaseDirectory 'release-manifest.json') -Encoding utf8
    $Manifest.artifacts | ForEach-Object { "$($_.sha256.ToLowerInvariant())  $($_.file)" } | Set-Content -LiteralPath (Join-Path $ReleaseDirectory 'SHA256SUMS.txt') -Encoding ascii
    Set-Content -LiteralPath 'runtime/current-release.txt' -Value "artifacts/releases/$Stamp" -Encoding utf8
    Write-Host "Lokaler Prüfentwurf: $ReleaseDirectory"
} finally { Pop-Location }
