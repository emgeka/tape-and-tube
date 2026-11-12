. "$PSScriptRoot/environment.ps1"
$Executable = Get-AppExecutable
$Process = Start-Process -FilePath $Executable -ArgumentList '--output-smoke' -WindowStyle Hidden -PassThru -Wait
if ($Process.ExitCode -ne 0) {
    $ErrorLog = Join-Path $ProjectRoot 'artifacts/output-smoke-error.txt'
    if (Test-Path -LiteralPath $ErrorLog) { Get-Content -LiteralPath $ErrorLog }
    throw "Ausgabegrößenprüfung fehlgeschlagen (Exitcode $($Process.ExitCode))."
}
Get-Content -LiteralPath (Join-Path $ProjectRoot 'artifacts/output-smoke.json')
