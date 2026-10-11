# Read-only check of the actual kernel drivers, using setup's production predicates.
# Run with Windows PowerShell 5.1 as administrator; does not run any vendor installer.
$ErrorActionPreference = 'Stop'
Write-Host "Read-only inspection process: $([IntPtr]::Size * 8)-bit"
$tokens = $null
$parseErrors = $null
$source = Join-Path (Split-Path $PSScriptRoot -Parent) 'installer\Install-Drivers.ps1'
$ast = [Management.Automation.Language.Parser]::ParseFile($source, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count) { throw ($parseErrors | Out-String) }
foreach ($definition in $ast.FindAll({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] }, $false)) {
    Invoke-Expression $definition.Extent.Text
}
foreach ($name in @('ViGEmBus', 'HidHide')) {
    $driver = Get-KernelDriver $name
    Write-Host "$name kernel driver registered: $($null -ne $driver)"
    Write-Host "$name dependency complete: $(Test-DriverComplete $name)"
}
$packages = Get-Content (Join-Path (Split-Path $PSScriptRoot -Parent) 'installer\driver-packages.json') -Raw | ConvertFrom-Json
foreach ($package in @($packages | Where-Object { -not $_.IsRuntime })) {
    $product = Get-DriverProduct $package.DisplayName
    Write-Host "$($package.Service) vendor product: $($product.DisplayName) $($product.DisplayVersion)"
}
