# Invoked elevated by Inno Setup. Signed, pinned vendor packages are bundled with setup.
param(
    [Parameter(Mandatory=$true)][string]$InstallDir,
    [Parameter(Mandatory=$true)][string]$LogPath
)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
# Inno 7 no longer disables WOW64 redirection in Exec. Also support callers that
# invoke this script directly from a 32-bit process: dependency/registry checks
# must use the native 64-bit view. Relaunch before starting the transcript.
if ([Environment]::Is64BitOperatingSystem -and -not [Environment]::Is64BitProcess) {
    try {
        $nativePowerShell = Join-Path $env:windir 'Sysnative\WindowsPowerShell\v1.0\powershell.exe'
        $nativeArguments = '-NoProfile -ExecutionPolicy Bypass -File "' + $PSCommandPath +
            '" -InstallDir "' + $InstallDir + '" -LogPath "' + $LogPath + '"'
        $nativeProcess = Start-Process -FilePath $nativePowerShell -ArgumentList $nativeArguments -WindowStyle Hidden -Wait -PassThru
        exit $nativeProcess.ExitCode
    } catch {
        Add-Content -LiteralPath $LogPath -Value "Could not launch native 64-bit PowerShell: $($_.Exception.Message)"
        exit 1
    }
}
# The installer creates the log directory before invoking us. Keep a durable diagnosis
# even when the PowerShell window disappears or the installer reports only an exit code.
try { Start-Transcript -LiteralPath $LogPath -Force -ErrorAction Stop | Out-Null }
catch { Write-Error "Could not start driver setup log at ${LogPath}: $_"; exit 1 }
Write-Host "Dependency setup process: $([IntPtr]::Size * 8)-bit; PowerShell home: $PSHOME"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
function Find-HidHideCli {
    $roots = @($env:ProgramFiles, ${env:ProgramFiles(x86)})
    $product = Get-DriverProduct 'HidHide'
    if ($product -and $product.InstallLocation) { $roots += $product.InstallLocation }
    foreach ($key in @('HKLM:\SOFTWARE\Nefarius Software Solutions e.U.\HidHide',
                        'HKLM:\SOFTWARE\WOW6432Node\Nefarius Software Solutions e.U.\HidHide')) {
        if (Test-Path -LiteralPath $key) {
            $location = (Get-ItemProperty -LiteralPath $key -ErrorAction Stop).Path
            if ($location) { $roots += $location }
        }
    }
    foreach ($root in @($roots | Where-Object { $_ } | Select-Object -Unique)) {
        if (-not (Test-Path -LiteralPath $root)) { continue }
        foreach ($relative in @('HidHideCLI.exe', 'x64\HidHideCLI.exe')) {
            $candidate = Join-Path $root $relative
            if (Test-Path -LiteralPath $candidate -PathType Leaf) { return $candidate }
        }
        foreach ($vendor in @(Get-ChildItem -LiteralPath $root -Directory -Filter '*Nefarius*' -ErrorAction Stop)) {
            $candidate = Get-ChildItem -LiteralPath $vendor.FullName -Recurse -File -Filter HidHideCLI.exe -ErrorAction Stop | Select-Object -First 1
            if ($candidate) { return $candidate.FullName }
        }
    }
}

function Get-DriverProduct([string]$displayName) {
    # Avoid Win32_Product: enumerating it can repair unrelated MSI installations.
    foreach ($root in @('HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall',
                        'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall')) {
        if (-not (Test-Path -LiteralPath $root)) { continue }
        foreach ($key in @(Get-ChildItem -LiteralPath $root -ErrorAction Stop)) {
            $product = Get-ItemProperty -LiteralPath $key.PSPath -ErrorAction Stop
            if ($product.DisplayName -eq $displayName -or
                ($displayName -eq 'ViGEm Bus Driver' -and $product.DisplayName -eq 'Nefarius Virtual Gamepad Emulation Bus Driver')) {
                return $product
            }
        }
    }
}

function Get-KernelDriver([string]$service) {
    # Get-Service enumerates ordinary Win32 services, not kernel/device drivers.
    # Load the .NET Framework assembly explicitly for Windows PowerShell 5.1.
    Add-Type -AssemblyName System.ServiceProcess -ErrorAction Stop
    [System.ServiceProcess.ServiceController]::GetDevices() |
        Where-Object { $_.ServiceName -eq $service } | Select-Object -First 1
}

function Test-DriverComplete([string]$service) {
    $driver = Get-KernelDriver $service
    if (-not $driver) { Write-Host "${service}: kernel driver is not registered"; return $false }
    $config = Get-ItemProperty -LiteralPath "HKLM:\SYSTEM\CurrentControlSet\Services\$service" -ErrorAction Stop
    if ($config.Start -eq 4) { Write-Host "${service}: kernel driver is disabled"; return $false }
    if (-not (Test-Path -LiteralPath (Join-Path $env:windir "System32\drivers\$service.sys") -PathType Leaf)) {
        Write-Host "${service}: driver binary is missing"; return $false
    }
    if ($service -eq 'HidHide' -and -not (Find-HidHideCli)) {
        Write-Host 'HidHide: required HidHideCLI.exe is missing'; return $false
    }
    Write-Host "${service}: kernel driver detected (status $($driver.Status)); required files present"
    return $true
}

function Get-VerifiedPackage($package, [string]$packageDir) {
    $service = $package.Service
    $target = Join-Path $packageDir $package.FileName
    if (-not (Test-Path -LiteralPath $target -PathType Leaf)) { throw "Bundled driver package missing: $target" }
    if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $package.Sha256) {
        throw "SHA256 mismatch for $service; refusing to run."
    }
    $signature = Get-AuthenticodeSignature -FilePath $target
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch $package.PublisherPattern) {
        throw "Invalid or unexpected publisher signature for $service; refusing to run."
    }
    return $target
}

function Test-VCRuntimeComplete([version]$minimumVersion) {
    $key = 'HKLM:\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64'
    if (-not (Test-Path -LiteralPath $key)) { return $false }
    $runtime = Get-ItemProperty -LiteralPath $key -ErrorAction Stop
    return ($runtime.Installed -eq 1 -and $runtime.Version -and
        [version]($runtime.Version -replace '^v', '') -ge $minimumVersion)
}

function Ensure-VCRuntime($package, [string]$packageDir, [string]$logDir) {
    if (Test-VCRuntimeComplete ([version]$package.Version)) {
        Write-Host 'Required x64 Visual C++ runtime already installed'
        return
    }
    $target = Get-VerifiedPackage $package $packageDir
    $vendorLog = Join-Path $logDir 'VCRuntime-install.log'
    Write-Host "Installing Microsoft x64 Visual C++ runtime; log: $vendorLog"
    $process = Start-Process -FilePath $target -ArgumentList ('/install /passive /norestart /log "' + $vendorLog + '"') -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -notin @(0, 3010, 1641)) {
        throw "Visual C++ runtime installer failed with exit code $($process.ExitCode). See $vendorLog."
    }
    if ($process.ExitCode -in @(3010, 1641)) { $script:RebootRequired = $true }
    if (-not (Test-VCRuntimeComplete ([version]$package.Version))) {
        throw "Required x64 Visual C++ runtime not detected after installation. See $vendorLog."
    }
}

function Ensure-Driver($package, [string]$packageDir, [string]$logDir) {
    $service = $package.Service
    if (Test-DriverComplete $service) {
        Write-Host "$service driver and required files already installed"
        return
    }
    Write-Host "$service installation is missing or incomplete; installing/repairing its full vendor package..."
    $target = Get-VerifiedPackage $package $packageDir
    $vendorLog = Join-Path $logDir "$service-install.log"
    # Both pinned packages use Advanced Installer. Let the vendor install its prerequisites
    # and driver; do not edit driver registry entries or uninstall shared drivers ourselves.
    $arguments = '/exenoui /passive /norestart IAgree=Yes /L*v "' + $vendorLog + '"'
    $product = Get-DriverProduct $package.DisplayName
    if ($product) {
        $installedVersion = [version]$product.DisplayVersion
        $bundledVersion = [version]$package.Version
        if ($installedVersion -ge $bundledVersion) {
            if ($installedVersion -eq $bundledVersion) {
                $arguments += ' REINSTALL=ALL REINSTALLMODE=amus'
            } elseif ($product.WindowsInstaller -eq 1 -and $product.PSChildName -match '^\{[0-9A-Fa-f-]{36}\}$') {
                # Repair a newer installed MSI from its cache instead of downgrading it.
                $target = Join-Path $env:windir 'System32\msiexec.exe'
                $arguments = '/famus ' + $product.PSChildName + ' /passive /norestart /L*v "' + $vendorLog + '"'
            } else {
                throw "Cannot automatically repair newer $service package $installedVersion; see $vendorLog."
            }
        }
    }
    Write-Host "Running $service vendor installer; log: $vendorLog"
    $process = Start-Process -FilePath $target -ArgumentList $arguments -WindowStyle Hidden -Wait -PassThru
    Write-Host "$service vendor installer exit code: $($process.ExitCode)"
    if ($process.ExitCode -notin @(0, 3010, 1641)) {
        throw "$service installer failed with exit code $($process.ExitCode). See $vendorLog."
    }
    if ($process.ExitCode -in @(3010, 1641)) { $script:RebootRequired = $true }
    if (-not (Test-DriverComplete $service)) {
        throw "$service remains incomplete after vendor installation (driver disabled/missing or HidHideCLI.exe missing). See $vendorLog. Restart Windows if requested, then rerun setup."
    }
}
$status = 1
$script:RebootRequired = $false
try {
    $packages = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'driver-packages.json') -Raw | ConvertFrom-Json
    foreach ($package in $packages) {
        if ($package.IsRuntime) {
            Ensure-VCRuntime $package (Join-Path $PSScriptRoot 'Drivers') (Split-Path -Parent $LogPath)
        } else {
            Ensure-Driver $package (Join-Path $PSScriptRoot 'Drivers') (Split-Path -Parent $LogPath)
        }
    }
    # Whitelist the exact installed executable before any device is hidden. Never enable
    # global inverse hiding: it would expose only the selected applications to EVERY device.
    $appPath = Join-Path $InstallDir 'RlRemapper.exe'
    if (-not (Test-Path -LiteralPath $appPath -PathType Leaf)) { throw "Installed remapper not found: $appPath" }
    Write-Host 'Locating HidHideCLI.exe and registering the installed remapper...'
    $cli = Find-HidHideCli
    if (-not $cli) { throw 'HidHideCLI.exe not found after installation; restart Windows if prompted and rerun setup.' }
    # Keep the exact CLI location for the non-elevated GUI; the install directory is admin-writable only.
    [IO.File]::WriteAllText((Join-Path $InstallDir 'hidhide-cli.path'), $cli)
    $registered = & $cli --app-list
    if ($LASTEXITCODE -ne 0) { throw 'Cannot read HidHide application whitelist.' }
    if (-not @($registered | Where-Object { $_.Trim() -ieq ('--app-reg "' + $appPath + '"') }).Count) {
        & $cli --app-reg $appPath
        if ($LASTEXITCODE -ne 0) { throw 'Could not whitelist the remapper in HidHide.' }
    }
    $registered = & $cli --app-list
    if ($LASTEXITCODE -ne 0 -or -not @($registered | Where-Object { $_.Trim() -ieq ('--app-reg "' + $appPath + '"') }).Count) {
        throw 'HidHide whitelist verification failed; no device was hidden.'
    }
    Write-Host 'Driver checks and remapper whitelist complete. Select the physical device in the app before starting.'
    $status = 0
    if ($script:RebootRequired) { $status = 3010 }
} catch {
    Write-Host "Driver setup failed: $($_.Exception.Message)" -ForegroundColor Red
    if ($script:RebootRequired) { Write-Host 'A vendor installer requested a restart. Restart Windows and rerun setup to finish dependency configuration.' }
} finally {
    Stop-Transcript | Out-Null
}
exit $status
