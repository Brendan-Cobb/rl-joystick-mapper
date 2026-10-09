# Invoked elevated by Inno Setup. Driver binaries are fetched only from official GitHub releases.
param([Parameter(Mandatory=$true)][string]$InstallDir)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
function Ensure-Driver($service, $repository, $pattern) {
    if (Get-Service -Name $service -ErrorAction SilentlyContinue) {
        Write-Host "$service already installed"
        return
    }
    Write-Host "$service missing; retrieving official release metadata..."
    $release = Invoke-RestMethod -Uri "https://api.github.com/repos/nefarius/$repository/releases/latest" -Headers @{ 'User-Agent' = 'RlRemapperInstaller' }
    $asset = @($release.assets | Where-Object { $_.name -match $pattern }) | Select-Object -First 1
    if (-not $asset) { throw "No matching x64 installer in $repository release; install manually from the official release page." }
    $target = Join-Path $env:TEMP ([guid]::NewGuid().ToString() + '.exe')
    try {
        Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $target -UseBasicParsing
        $signature = Get-AuthenticodeSignature -FilePath $target
        if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'Nefarius') {
            throw "Invalid or unexpected publisher signature for $repository; refusing to run."
        }
        # Interactive installer: do not claim successful install until its service appears.
        $process = Start-Process -FilePath $target -Wait -PassThru
        if ($process.ExitCode -notin @(0, 3010, 1641)) { throw "$repository installer failed with exit code $($process.ExitCode)" }
        if (-not (Get-Service -Name $service -ErrorAction SilentlyContinue)) {
            throw "$service not detected after install; restart Windows if prompted and rerun setup."
        }
    } finally { Remove-Item $target -Force -ErrorAction SilentlyContinue }
}
try {
    Ensure-Driver 'ViGEmBus' 'ViGEmBus' '^ViGEmBus_.*_x64_x86_arm64\.exe$'
    Ensure-Driver 'HidHide' 'HidHide' '^HidHide_.*_x64\.exe$'
    # Whitelist the exact installed executable before any device is hidden. Never enable
    # global inverse hiding: it would expose only the selected applications to EVERY device.
    $appPath = Join-Path $InstallDir 'RlRemapper.exe'
    if (-not (Test-Path -LiteralPath $appPath -PathType Leaf)) { throw "Installed remapper not found: $appPath" }
    $cli = @(
        foreach ($root in @($env:ProgramFiles, ${env:ProgramFiles(x86)})) {
            if (-not $root -or -not (Test-Path -LiteralPath $root)) { continue }
            foreach ($vendor in @(Get-ChildItem -LiteralPath $root -Directory -Filter '*Nefarius*' -ErrorAction SilentlyContinue)) {
                Get-ChildItem -LiteralPath $vendor.FullName -Recurse -File -Filter HidHideCLI.exe -ErrorAction SilentlyContinue | ForEach-Object { $_.FullName }
            }
        }
    ) | Select-Object -First 1
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
    exit 0
} catch {
    Write-Error $_
    exit 1
}
