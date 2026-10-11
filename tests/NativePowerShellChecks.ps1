# Read-only regression for the actual 32-bit -> native PowerShell fallback.
# Extracts setup's production relaunch block; the probe only inspects drivers.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$tokens = $null
$parseErrors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile((Join-Path $root 'installer\Install-Drivers.ps1'), [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count) { throw ($parseErrors | Out-String) }
$guard = $ast.EndBlock.Statements | Where-Object {
    $_ -is [Management.Automation.Language.IfStatementAst] -and $_.Extent.Text -match 'Is64BitOperatingSystem'
} | Select-Object -First 1
if (-not $guard) { throw 'Native PowerShell fallback missing.' }
$probeDir = Join-Path $root 'build\tools\native-probe'
New-Item -ItemType Directory -Force $probeDir | Out-Null
$inspect = (Join-Path $PSScriptRoot 'Inspect-InstalledDrivers.ps1').Replace("'", "''")
$probe = $ast.ParamBlock.Extent.Text + "`r`n" + '$ErrorActionPreference = ''Stop''' + "`r`n" + $guard.Extent.Text + "`r`n" + @"
. '$inspect'
if (-not [Environment]::Is64BitProcess) { throw 'Probe stayed in 32-bit PowerShell.' }
if (-not (Test-DriverComplete 'ViGEmBus')) { throw 'Native probe must detect the installed ViGEmBus driver.' }
'Native 64-bit driver check passed.' | Set-Content -LiteralPath `$LogPath
exit 0
"@
$probePath = Join-Path $probeDir 'probe.ps1'
$resultPath = Join-Path $probeDir 'result.log'
Set-Content -LiteralPath $probePath -Value $probe -Encoding UTF8
$arguments = '-NoProfile -ExecutionPolicy Bypass -File "' + $probePath + '" -InstallDir "' + $root + '" -LogPath "' + $resultPath + '"'
$process = Start-Process -FilePath (Join-Path $env:windir 'SysWOW64\WindowsPowerShell\v1.0\powershell.exe') -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
if ($process.ExitCode -ne 0) { throw "Native fallback probe failed with exit code $($process.ExitCode)." }
Get-Content -LiteralPath $resultPath
