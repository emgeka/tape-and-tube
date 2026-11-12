. "$PSScriptRoot/environment.ps1"
$Executable = Get-AppExecutable
$Process = Start-Process -FilePath $Executable -ArgumentList '--export-smoke' -WindowStyle Hidden -PassThru -Wait
if ($Process.ExitCode -ne 0) {
    $ErrorLog = Join-Path $ProjectRoot 'artifacts/export-smoke-error.txt'
    if (Test-Path -LiteralPath $ErrorLog) { Get-Content -LiteralPath $ErrorLog }
    throw "Exportprüfung fehlgeschlagen (Exitcode $($Process.ExitCode))."
}
Get-Content -LiteralPath (Join-Path $ProjectRoot 'artifacts/export-smoke.json')
