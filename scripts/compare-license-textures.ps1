# SPDX-License-Identifier: GPL-3.0-only
param([string]$EvidenceDirectory = 'artifacts/license-evidence')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore
function Get-Pixels([string]$File) {
    $Stream = [IO.File]::OpenRead((Resolve-Path -LiteralPath $File))
    try {
        $Frame = [Windows.Media.Imaging.BitmapFrame]::Create($Stream, [Windows.Media.Imaging.BitmapCreateOptions]::PreservePixelFormat, [Windows.Media.Imaging.BitmapCacheOption]::OnLoad)
        $Bitmap = New-Object Windows.Media.Imaging.FormatConvertedBitmap($Frame, [Windows.Media.PixelFormats]::Bgra32, $null, 0)
        $Pixels = New-Object byte[] ($Bitmap.PixelWidth * $Bitmap.PixelHeight * 4)
        $Bitmap.CopyPixels($Pixels, $Bitmap.PixelWidth * 4, 0)
        $Hash = [Security.Cryptography.SHA256]::Create()
        try { $Digest = [BitConverter]::ToString($Hash.ComputeHash($Pixels)).Replace('-', '').ToLowerInvariant() } finally { $Hash.Dispose() }
        return [ordered]@{width=$Bitmap.PixelWidth; height=$Bitmap.PixelHeight; bgraSha256=$Digest}
    } finally { $Stream.Dispose() }
}
$Revision = (Get-Content -Raw dependencies.json | ConvertFrom-Json).'slang-shaders'.revision
$AssetRoot = "assets/slang-shaders-$Revision/crt/shaders"
$Results = @()
foreach ($File in Get-ChildItem -LiteralPath "$EvidenceDirectory/royale" -Filter '*.png' -File) {
    $Original = Get-Pixels $File.FullName
    $Bundled = Get-Pixels "$AssetRoot/crt-royale/$($File.Name)"
    $Match = $Original.width -eq $Bundled.width -and $Original.height -eq $Bundled.height -and $Original.bgraSha256 -eq $Bundled.bgraSha256
    $Results += [ordered]@{texture=$File.Name; original=$Original; bundled=$Bundled; pixelsMatch=$Match}
}
if ($Results.Count -ne 6) { throw 'Expected all six CRT-Royale textures' }
$Results | ConvertTo-Json -Depth 5 | Set-Content -Encoding utf8 "$EvidenceDirectory/royale/texture-comparison.json"
if ($Results.pixelsMatch -contains $false) { throw 'Texture pixel mismatch' }
Write-Output 'All six CRT-Royale textures match the author source pixel for pixel.'
