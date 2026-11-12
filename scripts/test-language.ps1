. "$PSScriptRoot/environment.ps1"
$Executable = Get-AppExecutable
$Process = Start-Process -FilePath $Executable -ArgumentList '--language-smoke' -WindowStyle Hidden -PassThru -Wait
if ($Process.ExitCode -ne 0) {
    $ErrorLog = Join-Path $ProjectRoot 'artifacts/language-smoke-error.txt'
    if (Test-Path -LiteralPath $ErrorLog) { Get-Content -LiteralPath $ErrorLog }
    throw "Sprachprüfung fehlgeschlagen (Exitcode $($Process.ExitCode))."
}
Get-Content -LiteralPath (Join-Path $ProjectRoot 'artifacts/language-smoke.json')
