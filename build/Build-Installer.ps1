# Run from repository root on Windows x64 with .NET 8 SDK and Inno Setup 6.
# Fetches version-pinned upstream native binaries; refuses any hash mismatch.
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$root = Split-Path $PSScriptRoot -Parent
$publish = Join-Path $root 'publish'
$driverDir = Join-Path $root 'installer\Drivers'
$cache = Join-Path $env:TEMP 'rl-remapper-build'
New-Item -ItemType Directory -Force $cache, $publish, $driverDir | Out-Null
function Fetch-Checked([string]$url, [string]$name, [string]$hash) {
    $file = Join-Path $cache $name
    if ((-not (Test-Path $file)) -or (Get-FileHash $file -Algorithm SHA256).Hash -ne $hash) {
        Invoke-WebRequest -Uri $url -OutFile $file -UseBasicParsing
    }
    if ((Get-FileHash $file -Algorithm SHA256).Hash -ne $hash) { throw "SHA256 mismatch for $name; refusing to package" }
    return $file
}
$packages = Get-Content -LiteralPath (Join-Path $root 'installer\driver-packages.json') -Raw | ConvertFrom-Json
foreach ($package in $packages) {
    $driver = Fetch-Checked $package.Url $package.FileName $package.Sha256
    $signature = Get-AuthenticodeSignature -FilePath $driver
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch $package.PublisherPattern) {
        throw "Invalid or unexpected publisher signature for $($package.FileName); refusing to package"
    }
    Copy-Item -LiteralPath $driver -Destination (Join-Path $driverDir $package.FileName) -Force
}
$sdlZip = Fetch-Checked 'https://github.com/libsdl-org/SDL/releases/download/release-2.32.10/SDL2-2.32.10-win32-x64.zip' 'SDL2-2.32.10-win32-x64.zip' '6CF9706EEFD0A4A06DC764007934D428AFAF029FABDD408A9E646048C91E18FB'
$vigem = Fetch-Checked 'https://raw.githubusercontent.com/ViGEm/ViGEm.NET/2197a9a6200a1cc4cbefc19e4ef6c9bbb63ff567/ViGEmClient/costura64/ViGEmClient.dll' 'ViGEmClient.dll' '5FD323A2DDB39E2966D55EB595B782BA1A5E71B01DB07B3A780D9B242FE6DACE'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($sdlZip)
try {
    $entry = $archive.GetEntry('SDL2.dll')
    if (-not $entry) { throw 'SDL2.dll not found in official x64 archive' }
    $sdl = Join-Path $publish 'SDL2.dll'
    $stream = [IO.File]::Create($sdl)
    try { $inputStream = $entry.Open(); try { $inputStream.CopyTo($stream) } finally { $inputStream.Dispose() } } finally { $stream.Dispose() }
} finally { $archive.Dispose() }
if ((Get-FileHash $sdl -Algorithm SHA256).Hash -ne 'B37740A72A7A9706216DF9F0134894BB7A850B356FD149398C67D874CBCFACB4') { throw 'SDL2.dll extraction hash mismatch' }
Copy-Item $vigem (Join-Path $publish 'ViGEmClient.dll') -Force
Copy-Item (Join-Path $root 'third_party\*-LICENSE.txt') $publish -Force
$dotnet = Join-Path $PSScriptRoot 'tools\dotnet\dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) { $dotnet = 'dotnet' }
& $dotnet publish (Join-Path $root 'src\RlRemapper\RlRemapper.csproj') -c Release -r win-x64 --self-contained true -o $publish
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }
$iscc = Join-Path $PSScriptRoot 'tools\inno\ISCC.exe'
if (-not (Test-Path -LiteralPath $iscc)) { $iscc = (Get-Command iscc.exe -ErrorAction SilentlyContinue).Source }
if (-not $iscc) {
    foreach ($dir in @("${env:ProgramFiles(x86)}\Inno Setup 6", "$env:ProgramFiles\Inno Setup 7")) {
        if (Test-Path (Join-Path $dir 'ISCC.exe')) { $iscc = Join-Path $dir 'ISCC.exe'; break }
    }
}
if (-not $iscc -or -not (Test-Path -LiteralPath $iscc)) { throw 'Inno Setup 6 or 7 compiler missing' }
& $iscc (Join-Path $root 'installer\RlRemapper.iss')
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup compilation failed' }
Write-Host "Installer built in $(Join-Path $root 'installer\Output')"
