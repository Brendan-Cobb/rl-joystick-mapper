# Read-only probe: run in a fresh, non-allowlisted PowerShell process.
$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class RemapperXInputProbe {
    [DllImport("xinput1_4.dll", ExactSpelling = true)]
    public static extern uint XInputGetState(uint index, IntPtr state);
}
'@
$state = [Runtime.InteropServices.Marshal]::AllocHGlobal(16)
try {
    $connected = @(
        foreach ($index in 0..3) {
            if ([RemapperXInputProbe]::XInputGetState($index, $state) -eq 0) { $index }
        }
    )
    [pscustomobject]@{ ConnectedCount = $connected.Count; Slots = $connected } | ConvertTo-Json -Compress
} finally { [Runtime.InteropServices.Marshal]::FreeHGlobal($state) }
