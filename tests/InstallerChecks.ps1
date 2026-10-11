# Regression checks with simulated Windows/installer state. Never install real drivers.
$ErrorActionPreference = 'Stop'
$tokens = $null
$parseErrors = $null
$source = Join-Path (Split-Path $PSScriptRoot -Parent) 'installer\Install-Drivers.ps1'
$ast = [Management.Automation.Language.Parser]::ParseFile($source, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count) { throw ($parseErrors | Out-String) }
foreach ($definition in $ast.FindAll({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] }, $false)) {
    Invoke-Expression $definition.Extent.Text
}
function Assert($condition, $message) { if (-not $condition) { throw $message } }

# Exercise the real completeness predicate before mocking it for vendor orchestration.
function Get-KernelDriver { param($service) if ($script:ServiceExists) { [pscustomobject]@{ ServiceName = $service; Status = 'Running' } } }
function Get-ItemProperty { param($LiteralPath, $ErrorAction) [pscustomobject]@{ Start = $script:StartMode } }
function Test-Path { param($LiteralPath, $PathType) return $script:FileExists }
function Find-HidHideCli { if ($script:CliExists) { 'C:\Vendor\HidHideCLI.exe' } }
$script:ServiceExists = $true
$script:FileExists = $true
$script:CliExists = $false
$script:StartMode = 1
Assert (-not (Test-DriverComplete 'HidHide')) 'Existing HidHide service must not mask a missing CLI.'
$script:CliExists = $true
$script:StartMode = 4
Assert (-not (Test-DriverComplete 'HidHide')) 'Disabled HidHide must not be considered complete.'
$script:StartMode = 1
Assert (Test-DriverComplete 'HidHide') 'A complete existing HidHide package should be reused.'
$script:FileExists = $false
Assert (-not (Test-DriverComplete 'ViGEmBus')) 'A service without its driver binary must be repaired.'
$script:FileExists = $true
$script:ServiceExists = $false
Assert (-not (Test-DriverComplete 'ViGEmBus')) 'Missing service must trigger installation.'

# Simulate the vendor process and trust checks; record every process launch.
function Test-DriverComplete { param($service) return $script:Complete }
function Get-DriverProduct { param($displayName) return $script:Product }
function Get-FileHash { param($LiteralPath, $Algorithm) [pscustomobject]@{ Hash = $script:Hash } }
function Get-AuthenticodeSignature {
    param($FilePath)
    [pscustomobject]@{ Status = $script:SignatureStatus; SignerCertificate = [pscustomobject]@{ Subject = $script:Publisher } }
}
function Start-Process {
    param($FilePath, $ArgumentList, $WindowStyle, [switch]$Wait, [switch]$PassThru)
    $script:Launches += [pscustomobject]@{ FilePath = $FilePath; Arguments = $ArgumentList }
    $script:Complete = $script:CompleteAfterInstall
    [pscustomobject]@{ ExitCode = $script:VendorExitCode }
}
$package = [pscustomobject]@{ Service = 'HidHide'; DisplayName = 'HidHide'; Version = '1.5.230'; FileName = 'HidHide.exe'; Sha256 = 'trusted-hash'; PublisherPattern = 'Nefarius' }
function Reset-Case {
    $script:Complete = $false
    $script:CompleteAfterInstall = $true
    $script:Product = $null
    $script:FileExists = $true
    $script:Hash = 'trusted-hash'
    $script:SignatureStatus = 'Valid'
    $script:Publisher = 'CN=Nefarius Software Solutions e.U.'
    $script:VendorExitCode = 0
    $script:Launches = @()
    $script:RebootRequired = $false
}
function Expect-Failure($message) {
    $failure = $null
    try { Ensure-Driver $package 'C:\Packages' 'C:\Logs' } catch { $failure = $_ }
    Assert ($null -ne $failure) $message
}
Reset-Case
$script:Complete = $true
Ensure-Driver $package 'C:\Packages' 'C:\Logs'
Assert ($script:Launches.Count -eq 0) 'Healthy drivers should not launch an installer.'

Reset-Case
Ensure-Driver $package 'C:\Packages' 'C:\Logs'
Assert ($script:Launches.Count -eq 1) 'Incomplete installation must run the bundled vendor package.'
Assert ($script:Launches[0].Arguments -match '/norestart' -and $script:Launches[0].Arguments -match '/L\*v') 'Vendor installation must defer reboot and capture a log.'

Reset-Case
$script:Product = [pscustomobject]@{ DisplayVersion = '1.5.230'; WindowsInstaller = 1 }
Ensure-Driver $package 'C:\Packages' 'C:\Logs'
Assert ($script:Launches[0].Arguments -match 'REINSTALL=ALL REINSTALLMODE=amus') 'Same-version partial MSI needs explicit repair.'

Reset-Case
$script:Product = [pscustomobject]@{ DisplayVersion = '1.4.0'; WindowsInstaller = 1 }
Ensure-Driver $package 'C:\Packages' 'C:\Logs'
Assert ($script:Launches[0].Arguments -notmatch 'REINSTALL=ALL') 'Older incomplete product should receive a normal vendor upgrade.'

Reset-Case
$script:Product = [pscustomobject]@{ DisplayVersion = '2.0.0'; WindowsInstaller = 1; PSChildName = '{11111111-1111-1111-1111-111111111111}' }
Ensure-Driver $package 'C:\Packages' 'C:\Logs'
Assert ($script:Launches[0].FilePath -like '*msiexec.exe' -and $script:Launches[0].Arguments -match '/famus') 'Newer product must be repaired without downgrade.'

Reset-Case
$script:Hash = 'tampered'
Expect-Failure 'Tampered package should fail.'
Assert ($script:Launches.Count -eq 0) 'Hash failure must prevent execution.'
Reset-Case
$script:SignatureStatus = 'NotSigned'
Expect-Failure 'Unsigned package should fail.'
Assert ($script:Launches.Count -eq 0) 'Unsigned package must not execute.'
Reset-Case
$script:Publisher = 'CN=Unrelated Publisher'
Expect-Failure 'Unexpected publisher should fail.'
Assert ($script:Launches.Count -eq 0) 'Wrong publisher must not execute.'
Reset-Case
$script:FileExists = $false
Expect-Failure 'Missing bundled package should fail.'
Assert ($script:Launches.Count -eq 0) 'Missing package must not execute.'
Reset-Case
$script:VendorExitCode = 1603
Expect-Failure 'Vendor install failure must propagate.'
Reset-Case
$script:CompleteAfterInstall = $false
Expect-Failure 'Vendor exit zero must not mask missing required files.'
Reset-Case
$script:VendorExitCode = 3010
Ensure-Driver $package 'C:\Packages' 'C:\Logs'
Assert $script:RebootRequired 'Vendor restart request must reach setup.'
Reset-Case
$script:VendorExitCode = 3010
$script:CompleteAfterInstall = $false
Expect-Failure 'Restart request must not mask an incomplete installation.'
Assert $script:RebootRequired 'Incomplete install should retain restart diagnostics.'
# Validate VC runtime detection (x64 registration and minimum version), then installation.
$runtimePredicate = $ast.FindAll({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Test-VCRuntimeComplete' }, $false)[0]
Invoke-Expression $runtimePredicate.Extent.Text
function Get-ItemProperty { param($LiteralPath, $ErrorAction) return $script:RuntimeRegistry }
$script:FileExists = $true
$script:RuntimeRegistry = [pscustomobject]@{ Installed = 1; Version = 'v14.44.35211.0' }
Assert (Test-VCRuntimeComplete ([version]'14.30.30704.0')) 'Newer VC runtime should be reused.'
$script:RuntimeRegistry = [pscustomobject]@{ Installed = 1; Version = 'v14.20.0.0' }
Assert (-not (Test-VCRuntimeComplete ([version]'14.30.30704.0'))) 'Older VC runtime must be upgraded.'
$script:RuntimeRegistry = [pscustomobject]@{ Installed = 0; Version = 'v14.44.35211.0' }
Assert (-not (Test-VCRuntimeComplete ([version]'14.30.30704.0'))) 'Uninstalled VC registration is insufficient.'
function Test-VCRuntimeComplete { param($minimumVersion) return $script:Complete }
$runtimePackage = [pscustomobject]@{ Service = 'VCRuntime'; Version = '14.30.30704.0'; FileName = 'VC_redist.x64.exe'; Sha256 = 'trusted-hash'; PublisherPattern = 'Microsoft Corporation' }
Reset-Case
$script:Publisher = 'CN=Microsoft Corporation'
Ensure-VCRuntime $runtimePackage 'C:\Packages' 'C:\Logs'
Assert ($script:Launches.Count -eq 1 -and $script:Launches[0].Arguments -match '/install /passive /norestart') 'Missing VC runtime must install before driver packages.'
Reset-Case
$script:Complete = $true
Ensure-VCRuntime $runtimePackage 'C:\Packages' 'C:\Logs'
Assert ($script:Launches.Count -eq 0) 'Existing compatible VC runtime must not be downgraded.'

# Validate modern, legacy, and registered custom CLI locations without touching disk.
$cliFinder = $ast.FindAll({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Find-HidHideCli' }, $false)[0]
Invoke-Expression $cliFinder.Extent.Text
function Test-Path {
    param($LiteralPath, $PathType)
    if ($LiteralPath -like 'HKLM:*') { return $false }
    if ($PathType -eq 'Leaf') { return $LiteralPath -eq $script:ExpectedCli }
    return $true
}
function Get-ChildItem {
    param($LiteralPath, $Filter, $ErrorAction, [switch]$Directory, [switch]$Recurse, [switch]$File)
    if ($Directory -and $LiteralPath -eq $env:ProgramFiles) { [pscustomobject]@{ FullName = 'C:\Vendor' } }
    if ($Recurse -and $script:ExpectedCli -like 'C:\Vendor\*') { [pscustomobject]@{ FullName = $script:ExpectedCli } }
}
$script:Product = $null
$script:ExpectedCli = 'C:\Vendor\HidHide\HidHideCLI.exe'
Assert ((Find-HidHideCli) -eq $script:ExpectedCli) 'Modern vendor layout must be discovered.'
$script:ExpectedCli = 'C:\Vendor\HidHide\x64\HidHideCLI.exe'
Assert ((Find-HidHideCli) -eq $script:ExpectedCli) 'Legacy vendor layout must be discovered.'
$script:Product = [pscustomobject]@{ InstallLocation = 'C:\Custom HidHide' }
$script:ExpectedCli = 'C:\Custom HidHide\x64\HidHideCLI.exe'
Assert ((Find-HidHideCli) -eq $script:ExpectedCli) 'Registered custom install location must be discovered.'
$script:ExpectedCli = $null
Assert (-not (Find-HidHideCli)) 'Absent CLI must remain detectable.'
Write-Host 'Installer dependency regression checks passed.'
