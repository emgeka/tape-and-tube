# SPDX-License-Identifier: GPL-3.0-only
. "$PSScriptRoot/environment.ps1"
$Run = Start-Process -FilePath (Get-AppExecutable) -ArgumentList '--update-smoke' -WindowStyle Hidden -PassThru -Wait
if ($Run.ExitCode -ne 0) {
    Get-Content -LiteralPath (Join-Path $ProjectRoot 'artifacts/update-smoke-error.txt') -ErrorAction SilentlyContinue
    throw 'Update-Kanal-Prüfungen fehlgeschlagen.'
}
Get-Content -LiteralPath (Join-Path $ProjectRoot 'artifacts/update-smoke.json')
