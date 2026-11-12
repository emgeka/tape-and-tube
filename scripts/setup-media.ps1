. "$PSScriptRoot/environment.ps1"
$Ffmpeg = Get-Command ffmpeg.exe -ErrorAction SilentlyContinue
$Ffprobe = Get-Command ffprobe.exe -ErrorAction SilentlyContinue
if (-not $Ffmpeg -or -not $Ffprobe) {
    throw 'FFmpeg und ffprobe fehlen. Bitte FFmpeg bereitstellen oder NTSCRT_FFMPEG und NTSCRT_FFPROBE auf die jeweiligen EXE-Dateien setzen.'
}
@{ ffmpeg = $Ffmpeg.Source; ffprobe = $Ffprobe.Source } | ConvertTo-Json |
    Set-Content -LiteralPath (Join-Path $ProjectRoot 'runtime/media-tools.json') -Encoding utf8
Write-Host 'Vorhandenes FFmpeg für die Video-Vorschau eingerichtet.'
