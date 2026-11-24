# SPDX-License-Identifier: GPL-3.0-only
. "$PSScriptRoot/environment.ps1"
$Executable = Get-AppExecutable
$Process = Start-Process -FilePath $Executable -ArgumentList '--digital-smoke' -WindowStyle Hidden -PassThru -Wait
if ($Process.ExitCode -ne 0) {
    Get-Content -LiteralPath (Join-Path $ProjectRoot 'artifacts/digital-smoke-error.txt')
    throw "Digitalvideo-Integration fehlgeschlagen (Exitcode $($Process.ExitCode))."
}
Get-Content -LiteralPath (Join-Path $ProjectRoot 'artifacts/digital-integration/report.json')
