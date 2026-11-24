# SPDX-License-Identifier: GPL-3.0-only
. "$PSScriptRoot/environment.ps1"
$Cli = Join-Path (Split-Path (Get-AppExecutable)) 'TapeTube.Cli.exe'
$Directory = Join-Path $ProjectRoot 'artifacts/cli-smoke'
New-Item -ItemType Directory -Force $Directory | Out-Null
$Image = Join-Path $Directory 'source image.png'
$Clip = Join-Path $Directory 'source clip.mp4'
$Preset = Join-Path $Directory 'cropped-look.json'
$Ffmpeg = if ($env:TAPETUBE_FFMPEG) { $env:TAPETUBE_FFMPEG } else { (Get-Command ffmpeg).Source }
$Ffprobe = if ($env:TAPETUBE_FFPROBE) { $env:TAPETUBE_FFPROBE } else { (Get-Command ffprobe).Source }
& $Ffmpeg -v error -nostdin -y -f lavfi -i 'testsrc2=size=96x64:rate=12' -frames:v 1 -update 1 $Image
Assert-NativeExit 'CLI-Testbild vorbereiten'
& $Ffmpeg -v error -nostdin -y -f lavfi -i 'testsrc2=size=96x64:rate=12:duration=1' -f lavfi -i 'sine=duration=1' -c:v mpeg4 -c:a aac $Clip
Assert-NativeExit 'CLI-Testvideo vorbereiten'
'{"version":3,"downscale":{"enabled":false},"ntsc":{"enabled":false},"shader":{"enabled":false},"crop":{"ratio":"1:1","x":1}}' |
    Set-Content -LiteralPath $Preset -Encoding utf8NoBOM

function Invoke-Cli([string[]]$Arguments) {
    $Json = & $Cli @Arguments
    Assert-NativeExit 'TapeTube.Cli'
    return $Json | ConvertFrom-Json
}
$Describe = Invoke-Cli @('describe')
if ($Describe.outputs.Count -ne 3 -or $Describe.digitalFormats.Count -ne 6) { throw 'Incomplete CLI metadata.' }
if (($Describe.mp4Qualities -join ',') -ne 'standard,high,very-high,maximum') { throw 'Missing MP4 quality choices.' }
$Catalog = Invoke-Cli @('describe','all')
if ($Catalog.effects.PSObject.Properties.Name.Count -ne 7 -or $Catalog.effects.digital.parameters.Count -ne 32 -or
    $Catalog.effects.'screen-loop'.parameters.Count -ne 23 -or $Catalog.effects.processing.presetFields.downscaleMethod.Count -ne 6 -or
    $Catalog.effects.ntsc.parameters.Count -eq 0 -or $null -eq $Catalog.effects.ntsc.defaults.random_seed -or
    $Catalog.effects.vcr.tapes.Count -ne 8 -or $Catalog.effects.reception.parameters.Count -lt 5 -or
    $Catalog.effects.crt.PSObject.Properties.Name.Count -ne $Describe.shaders.Count) { throw 'Incomplete effect catalog.' }
$OnlyCrt = Invoke-Cli @('describe','--shader','crtsim')
if ($OnlyCrt.effects.PSObject.Properties.Name -ne 'crt' -or $OnlyCrt.effects.crt.PSObject.Properties.Name -ne 'crtsim' -or
    $OnlyCrt.effects.crt.crtsim.Count -eq 0) { throw 'CRT detail filter did not return the requested shader.' }
& $Cli describe digital --shader crtsim 2>$null | Out-Null
if ($LASTEXITCODE -ne 2) { throw 'An incompatible shader filter should fail.' }
$Png = Join-Path $Directory 'output.png'
$CloseupPath = Join-Path $Directory 'closeup.png'
$CloseupArgs = @('render','--input',$Image,'--output',$CloseupPath,'--zoom','4','--zoom-at','1,1')
$CloseupPlan = Invoke-Cli ($CloseupArgs + '--dry-run')
$Closeup = Invoke-Cli ($CloseupArgs + '--overwrite')
$CropRect = $CloseupPlan.closeup.crop
if ($Closeup.width -ne 96 -or $Closeup.height -ne 64 -or $CropRect.x -ne 72 -or $CropRect.y -ne 48 -or
    $CropRect.width -ne 24 -or $CropRect.height -ne 16) { throw 'Close-up output or planned crop is incorrect.' }
function Read-RgbFrame([string]$Path) {
    $Raw = Join-Path $Directory 'closeup-test.rgb'
    & $Ffmpeg -v error -y -i $Path -frames:v 1 -pix_fmt rgb24 -f rawvideo $Raw
    Assert-NativeExit 'Decode PNG pixels'
    return ,([IO.File]::ReadAllBytes($Raw))
}
$OriginalRgb = Read-RgbFrame $Image
$ZoomedRgb = Read-RgbFrame $CloseupPath
for ($Row = 0; $Row -lt 64; $Row++) {
    for ($Column = 0; $Column -lt 96; $Column++) {
        $At = ($Row * 96 + $Column) * 3
        $ExpectedAt = ((48 + ($Row -shr 2)) * 96 + 72 + ($Column -shr 2)) * 3
        if ($ZoomedRgb[$At] -ne $OriginalRgb[$ExpectedAt] -or
            $ZoomedRgb[$At + 1] -ne $OriginalRgb[$ExpectedAt + 1] -or
            $ZoomedRgb[$At + 2] -ne $OriginalRgb[$ExpectedAt + 2]) {
            throw 'PNG close-up did not preserve exact source pixels.'
        }
    }
}
& $Cli render --input $Image --output $CloseupPath --zoom-at '0.5,0.5' --dry-run 2>$null | Out-Null
if ($LASTEXITCODE -ne 2) { throw 'A zoom center without a zoom factor should fail.' }
$OverrideBase = Invoke-Cli @('render','--input',$Image,'--output',$Png,'--overwrite')
$OverrideBaseHash = (Get-FileHash -LiteralPath $Png).Hash
$Noise = '{"ntsc":{"enabled":true,"values":{"composite_noise":true,"composite_noise_intensity":0.35,"random_seed":123}}}'
$null = Invoke-Cli @('render','--input',$Image,'--output',$Png,'--effects',$Noise,'--overwrite')
$NoiseHash = (Get-FileHash -LiteralPath $Png).Hash
if ($NoiseHash -eq $OverrideBaseHash) { throw 'An NTSC override should change the source image.' }
$null = Invoke-Cli @('render','--input',$Image,'--output',$Png,'--effects','{"ntsc":{"enabled":false}}','--overwrite')
if ((Get-FileHash -LiteralPath $Png).Hash -ne $OverrideBaseHash) { throw 'An explicit effect switch should leave the stage off.' }
& $Cli render --input $Image --output $Png --effects '{"ntsc":{"values":{"composite_noise_intensity":8}}}' --overwrite 2>$null | Out-Null
if ($LASTEXITCODE -ne 2 -or (Get-FileHash -LiteralPath $Png).Hash -ne $OverrideBaseHash) { throw 'An invalid effect override must leave the destination intact.' }
$Chain = '{"processing":{"mode":"on","width":64,"method":"nearest"},"digital":{"format":"minidv","values":{"sensor_noise":0.1}},"crt":{"shader":"crtsim"},"vcr":{"enabled":true},"reception":{"enabled":true}}'
$Chained = Invoke-Cli @('render','--input',$Image,'--output',$Png,'--effects',$Chain,'--width','288','--height','192','--overwrite')
if ($Chained.width -ne 288 -or $Chained.height -ne 192) { throw 'Combined effect overrides should render successfully.' }
$Screen = '{"screenLoop":{"values":{"zoom":0.62,"delay":2}},"processing":{"mode":"on","width":64}}'
$LoopPng = Invoke-Cli @('render','--input',$Image,'--output',$Png,'--effects',$Screen,'--overwrite')
if ($LoopPng.frames -ne 1 -or (Get-FileHash -LiteralPath $Png).Hash -eq $OverrideBaseHash) { throw 'Direct Screen Loop parameters should produce feedback.' }
$Still = Invoke-Cli @('render','--input',$Image,'--output',$Png,'--preset',$Preset,'--overwrite')
if ($Still.width -ne 64 -or $Still.height -ne 64 -or $Still.frames -ne 1) { throw 'The CLI did not apply the source crop.' }
$Before = (Get-FileHash -LiteralPath $Png).Hash
$Plan = Invoke-Cli @('render','--input',$Image,'--output',$Png,'--preset',$Preset,'--dry-run')
if (!$Plan.dryRun -or !$Plan.targetExists -or $Plan.format -ne 'png' -or $Plan.width -ne $Still.width -or
    $Plan.height -ne $Still.height -or (Get-FileHash -LiteralPath $Png).Hash -ne $Before) {
    throw 'PNG dry-run should match the crop and leave the existing target alone.'
}
$Unwritten = Join-Path $Directory 'planned-only/output.mp4'
$Mp4Plan = Invoke-Cli @('render','--input',$Clip,'--output',$Unwritten,'--width','81','--height','49',
    '--codec','hevc','--loops','3','--dry-run')
if ($Mp4Plan.width -ne 82 -or $Mp4Plan.height -ne 50 -or $Mp4Plan.frames -ne 36 -or $Mp4Plan.duration -ne 3 -or
    $Mp4Plan.codec -ne 'hevc' -or $Mp4Plan.targetDirectoryExists -or (Test-Path -LiteralPath (Split-Path $Unwritten))) {
    throw 'MP4 dry-run did not resolve padding, duration and loops without creating the target.'
}
& $Cli render --input $Image --output $Png 2>$null | Out-Null
if ($LASTEXITCODE -eq 0 -or (Get-FileHash -LiteralPath $Png).Hash -ne $Before) { throw 'Existing output needs explicit overwrite permission.' }
$Frame = Invoke-Cli @('render','--input',$Clip,'--output',$Png,'--frame','5','--width','80','--height','48','--overwrite')
if ($Frame.width -ne 80 -or $Frame.height -ne 48) { throw 'The selected video frame used the wrong output size.' }
$BatchPath = Join-Path $Directory 'batch.png'
$BatchArguments = @('render','--input',$Clip,'--output',$BatchPath,'--time','0.5,0,0.51',
    '--effects','{"digital":{"format":"minidv"}}')
$BatchPlan = Invoke-Cli ($BatchArguments + '--dry-run')
if (!$BatchPlan.dryRun -or ($BatchPlan.outputs.frameIndex -join ',') -ne '6,0,6' -or
    (Split-Path -Leaf $BatchPlan.outputs[0].path) -ne 'batch-001-frame-000006.png') {
    throw 'The PNG batch plan lost request order or selected unexpected frames.'
}
$Batch = Invoke-Cli ($BatchArguments + '--overwrite')
if ($Batch.frames -ne 3 -or ($Batch.outputs.path -join ',') -ne ($BatchPlan.outputs.path -join ',') -or
    $Batch.outputs[0].actualSeconds -ne 0.5 -or $Batch.outputs[1].actualSeconds -ne 0 -or
    $Batch.outputs[2].actualSeconds -ne 0.5 -or (Test-Path -LiteralPath $BatchPath)) {
    throw 'The PNG batch output did not match its plan.'
}
$BatchHash = (Get-FileHash -LiteralPath $Batch.outputs[0].path).Hash
$null = Invoke-Cli @('render','--input',$Clip,'--output',$Png,'--frame','6',
    '--effects','{"digital":{"format":"minidv"}}','--overwrite')
if ($BatchHash -ne (Get-FileHash -LiteralPath $Png).Hash -or
    $BatchHash -ne (Get-FileHash -LiteralPath $Batch.outputs[2].path).Hash) {
    throw 'Batch output must match the corresponding individual frame, including repeated time selections.'
}
& $Cli @BatchArguments 2>$null | Out-Null
if ($LASTEXITCODE -eq 0 -or $BatchHash -ne (Get-FileHash -LiteralPath $Batch.outputs[0].path).Hash) {
    throw 'A PNG batch must preserve existing outputs unless overwrite is explicit.'
}
$SnappedPng = Invoke-Cli @('render','--input',$Image,'--output',$Png,'--preset',$Preset,'--width','108','--height','180','--snap','--overwrite')
if ($SnappedPng.width -ne 128 -or $SnappedPng.height -ne 213) { throw 'Scanline PNG should use cropped source rows and preserve the portrait canvas.' }
$ScalePreset = Join-Path $Directory 'scale-look.json'
$ScaleHashes = foreach ($Method in @('nearest', 'bilinear')) {
    @{Kind='tape-and-tube';Version=1;NtscEnabled=$false;CrtEnabled=$true;Shader='crtsim';Width=256;Scale=3;
      Ntsc=@{};Parameters=@{};DownscaleMethod=$Method} | ConvertTo-Json | Set-Content -LiteralPath $ScalePreset -Encoding utf8NoBOM
    $null = Invoke-Cli @('render','--input',$Image,'--output',$Png,'--preset',$ScalePreset,'--width','384','--height','256','--overwrite')
    (Get-FileHash -LiteralPath $Png).Hash
}
if ($ScaleHashes[0] -eq $ScaleHashes[1]) { throw 'The saved downscale method did not change the CLI render.' }
$NativeLook = Get-Content -LiteralPath $ScalePreset -Raw | ConvertFrom-Json -AsHashtable
$NativeLook.DownscaleEnabled = $false
$NativeLook | ConvertTo-Json | Set-Content -LiteralPath $ScalePreset -Encoding utf8NoBOM
$NativePng = Invoke-Cli @('render','--input',$Image,'--output',$Png,'--preset',$ScalePreset,'--overwrite')
if ($NativePng.width -ne 96 -or $NativePng.height -ne 64) { throw 'Disabling downscale in a preset should preserve source size in automatic output.' }
$OverridePreset = Invoke-Cli @('render','--input',$Image,'--output',$Png,'--preset',$ScalePreset,
    '--effects','{"crt":{"enabled":false},"processing":{"mode":"off"}}','--overwrite')
if ($OverridePreset.width -ne 96 -or (Get-FileHash -LiteralPath $Png).Hash -ne $OverrideBaseHash) {
    throw 'The explicit render settings should override the preset.'
}
$Mp4Path = Join-Path $Directory 'output.mp4'
$Mp4 = Invoke-Cli @('render','--input',$Clip,'--output',$Mp4Path,'--preset',$Preset,'--overwrite')
if ($Mp4.frames -ne 12 -or $Mp4.width -ne 64 -or $Mp4.height -ne 64) { throw 'Unexpected MP4 result.' }
$SnappedMp4 = Invoke-Cli @('render','--input',$Clip,'--output',(Join-Path $Directory 'snapped.mp4'),'--width','150','--height','100','--snap','--overwrite')
if ($SnappedMp4.width -ne 192 -or $SnappedMp4.height -ne 128 -or $SnappedMp4.frames -ne 12) { throw 'MP4 dimensions did not snap to the source scanlines.' }
$HevcPath = Join-Path $Directory 'hevc.mp4'
$Hevc = Invoke-Cli @('render','--input',$Clip,'--output',$HevcPath,'--preset',$Preset,'--codec','hevc','--overwrite')
if ($Hevc.frames -ne 12 -or $Hevc.width -ne 64 -or $Hevc.height -ne 64) { throw 'Unexpected HEVC result.' }
foreach ($File in @($Mp4Path, $HevcPath)) {
    $Probe = & $Ffprobe -v error -show_entries 'stream=codec_name,codec_tag_string,duration,nb_frames' -of json $File | ConvertFrom-Json
    Assert-NativeExit 'MP4-Codecs prüfen'
    $Video, $Audio = $Probe.streams
    $Expected = if ($File -eq $HevcPath) { 'hevc' } else { 'h264' }
    if ($Video.codec_name -ne $Expected -or $Video.nb_frames -ne 12 -or $Audio.codec_name -ne 'aac' -or
        [Math]::Abs([double]$Video.duration - [double]$Audio.duration) -gt 0.1 -or
        ($File -eq $HevcPath -and $Video.codec_tag_string -ne 'hvc1')) {
        throw "Unexpected video or audio stream in $File."
    }
}
& $Cli render --input $Clip --output (Join-Path $Directory 'unsupported.png') --codec hevc 2>$null | Out-Null
if ($LASTEXITCODE -ne 2) { throw 'MP4 codec should be rejected for PNG.' }
$QualitySizes = foreach ($Quality in @('standard', 'high', 'very-high', 'maximum')) {
    $QualityPath = Join-Path $Directory 'quality.mp4'
    $Result = Invoke-Cli @('render','--input',$Clip,'--output',$QualityPath,'--preset',$Preset,'--quality',$Quality,'--overwrite')
    if ($Result.frames -ne $Mp4.frames) { throw "Unexpected frame count for MP4 quality $Quality." }
    (Get-Item -LiteralPath $QualityPath).Length
}
if ($QualitySizes[0] -ge $QualitySizes[1] -or $QualitySizes[1] -ge $QualitySizes[2] -or
    $QualitySizes[2] -ge $QualitySizes[3] -or $QualitySizes[1] -ne (Get-Item -LiteralPath $Mp4Path).Length) {
    throw 'MP4 quality choices did not change the encode as expected, or the default differs from high.'
}
& $Cli render --input $Clip --output (Join-Path $Directory 'unsupported.png') --quality maximum 2>$null | Out-Null
if ($LASTEXITCODE -ne 2) { throw 'MP4 quality should be rejected for PNG.' }
$LoopSource = Join-Path $Directory 'loop-source.mp4'
& $Ffmpeg -v error -nostdin -y -f lavfi -i 'color=black:size=96x64:rate=12:duration=0.5' -f lavfi -i 'color=white:size=96x64:rate=12:duration=0.5' -f lavfi -i 'sine=duration=0.5' -filter_complex '[0:v][1:v]concat=n=2:v=1:a=0[v];[2:a]adelay=250[a]' -map '[v]' -map '[a]' -c:v mpeg4 -c:a aac $LoopSource
Assert-NativeExit 'Video für MP4-Durchläufe vorbereiten'
$LoopPath = Join-Path $Directory 'loop.mp4'
$Loop = Invoke-Cli @('render','--input',$LoopSource,'--output',$LoopPath,'--loops','3','--overwrite')
if ($Loop.frames -ne 36 -or $Loop.duration -ne 3) { throw 'Wrong frame count or duration for three MP4 plays.' }
$Probe = & $Ffprobe -v error -show_entries 'stream=codec_name,duration,nb_frames' -of json $LoopPath | ConvertFrom-Json
Assert-NativeExit 'Audio- und Videodauer prüfen'
$Video, $Audio = $Probe.streams
if ($Video.nb_frames -ne 36 -or [Math]::Abs([double]$Video.duration - 3) -gt 0.001 -or
    $Audio.codec_name -ne 'aac' -or [Math]::Abs([double]$Audio.duration - 3) -gt 0.03) {
    throw 'Repeat export did not keep audio and video the same length.'
}
$Decoded = Join-Path $Directory 'loop.gray'
& $Ffmpeg -v error -nostdin -y -i $LoopPath -an -pix_fmt gray -f rawvideo $Decoded
Assert-NativeExit 'Bildfolge an den Übergängen dekodieren'
$Pixels = [IO.File]::ReadAllBytes($Decoded)
for ($FrameIndex = 0; $FrameIndex -lt 36; $FrameIndex++) {
    $Expected = if ($FrameIndex % 12 -lt 6) { 0 } else { 255 }
    if ([Math]::Abs([int]$Pixels[$FrameIndex * 96 * 64] - $Expected) -gt 8) {
        throw "Incorrect picture at output frame $FrameIndex."
    }
}
for ($Pass = 0; $Pass -lt 3; $Pass++) {
    foreach ($Window in @(@(0.04, -60), @(0.4, -35), @(0.87, -60))) {
        $Start = ($Pass + $Window[0]).ToString([Globalization.CultureInfo]::InvariantCulture)
        $Stats = & $Ffmpeg -hide_banner -ss $Start -i $LoopPath -t 0.065 -vn -af volumedetect -f null - 2>&1 | Out-String
        Assert-NativeExit 'Ton am Übergang prüfen'
        if ($Stats -notmatch 'max_volume: (-?\d+(?:\.\d+)?) dB') { throw 'Expected a decoded audio peak.' }
        $Peak = [double]::Parse($Matches[1], [Globalization.CultureInfo]::InvariantCulture)
        if (($Window[1] -eq -60 -and $Peak -gt -60) -or ($Window[1] -eq -35 -and $Peak -lt -35)) {
            throw "Audio onset or silence shifted at $Start seconds."
        }
    }
}
& $Cli render --input $Clip --output $LoopPath --loops 51 --overwrite 2>$null | Out-Null
if ($LASTEXITCODE -ne 2) { throw 'MP4 loops above the limit should be rejected.' }
$Gif = Invoke-Cli @('render','--input',$Image,'--output',(Join-Path $Directory 'output.gif'),'--seconds','0.5','--fps','12','--overwrite')
if ($Gif.frames -ne 6 -or $Gif.width -ne 96 -or $Gif.height -ne 64) { throw 'Unexpected animated still result.' }
$CustomGifPath = Join-Path $Directory 'custom.gif'
$CustomGif = Invoke-Cli @('render','--input',$Clip,'--output',$CustomGifPath,'--preset',$Preset,'--gif-width','128','--gif-fps','30','--overwrite')
$GifPlan = Invoke-Cli @('render','--input',$Clip,'--output',$CustomGifPath,'--preset',$Preset,'--gif-width','128','--gif-fps','30','--dry-run')
if ($GifPlan.width -ne $CustomGif.width -or $GifPlan.height -ne $CustomGif.height -or $GifPlan.frames -ne $CustomGif.frames -or
    $GifPlan.duration -ne $CustomGif.duration -or $GifPlan.fps -ne 30) { throw 'GIF dry-run does not match the export.' }
if ($CustomGif.frames -ne 30 -or $CustomGif.width -ne 128 -or $CustomGif.height -ne 128 -or $CustomGif.duration -ne 1) {
    throw 'GIF settings should apply to video and use the cropped aspect ratio.'
}
$ActualGif = & $Ffprobe -v error -show_entries 'stream=width,height,nb_frames,duration' -of json $CustomGifPath | ConvertFrom-Json
Assert-NativeExit 'GIF-Abmessungen und Laufzeit prüfen'
$GifStream = $ActualGif.streams[0]
if ($GifStream.width -ne 128 -or $GifStream.height -ne 128 -or $GifStream.nb_frames -ne 30 -or
    [Math]::Abs([double]$GifStream.duration - 1) -gt 0.001) {
    throw "GIF stream did not preserve 30 fps timing: $($GifStream | ConvertTo-Json -Compress)."
}
& $Cli render --input $Clip --output $CustomGifPath --gif-fps 51 --overwrite 2>$null | Out-Null
if ($LASTEXITCODE -ne 2) { throw 'Invalid GIF rate should be rejected.' }
[pscustomobject]@{passed=$true;checks=@('describe','seven-stage effect catalog','CRT detail query','direct effects and preset overrides','read-only PNG, MP4 and GIF planning','imported crop','overwrite protection','video frame to PNG','PNG batch planning, export and temporal history','PNG close-up geometry and exact pixel sampling','scanline snapping for PNG and MP4','H.264 and HEVC MP4 with AAC','codec validation','four MP4 qualities and default','MP4 picture and audio loops','still to GIF','GIF dimensions and 30 fps timing')} | ConvertTo-Json -Compress
