# RL Remapper (Windows x64)

Controller remapper with left-stick cardinal snapping, independent center and axial deadzones, optional Square Stick output, and configurable button/axis mappings. It reads a physical controller through SDL2 and sends a virtual Xbox 360 controller through ViGEm. HidHide keeps the physical controller out of games while letting the remapper read it.

## Download and install

Download [RLRemapper-Setup.exe from the latest release](https://github.com/Brendan-Cobb/rl-joystick-mapper/releases/latest/download/RLRemapper-Setup.exe). This repository contains source and build/test code; the release contains the built installer.

The latest release also includes a source ZIP. Version 0.1.9 adds the optional DecoupledAxial stick mapping and the browser visualizer below. Existing profiles keep LegacyAngular behavior until you select the new mapping mode.

Run setup as administrator. It includes the self-contained .NET runtime, SDL2, ViGEmClient, signed ViGEmBus and HidHide installers, and Microsoft's x64 Visual C++ runtime. Missing or incomplete dependencies are installed or repaired. The project installer is unsigned; embedded vendor packages are hash-checked and signature-verified.

Setup uses native 64-bit PowerShell and kernel-driver detection. It checks startup configuration and required files, locates HidHide's CLI and allowlists the installed remapper. Logs are saved under `%ProgramData%\RL Remapper` (`setup-drivers.log` and per-dependency installer logs). If Windows needs a restart before configuration finishes, restart and rerun setup. Setup does not hide devices until you select/start a controller in the app.

## Remap controls

1. Connect the physical controller. With multiple eligible gamepads, use **Select physical device**; otherwise selection is automatic on **Start**.
2. Stop remapping and open **Remap controls → Button setup → Start guided setup**.
3. Follow **Press A / Cross**, **Press B / Circle**, **Press X / Square**, **Press Y / Triangle**, then the remaining prompts. Release each button before the next prompt. Use **Skip this button** for unavailable controls. Physical inputs are identified and assigned automatically.
4. Use **Sticks and triggers → Learn** to identify each stick direction or trigger. **Advanced button mappings** provides manual output selection and disabling individual buttons. D-pad hats are forwarded automatically and recognized in guided setup.
5. **Save mappings** keeps edits; **Cancel** discards them, including a pending **Reset mappings**. Press **Start** when finished. Assign game actions such as Jump and Boost in Rocket League's controls menu.

Snap/deadzone and polling settings remain in the settings grid; **Save / Apply** publishes those settings to the running loop. Settings live in `%LOCALAPPDATA%\RlRemapper\settings.json`. Polling supports 50–1000 Hz without reading config from disk per frame. Turbo jump is not included.

## Stick mapping

Choose **Stick mapping → Mapping mode → DecoupledAxial**, then **Save / Apply** to use the new controls. The default remains **LegacyAngular**, including for existing settings files without a mode: their angular snapping, outer-ring threshold and always-on square conversion keep their original behavior. The categories marked **Decoupled** apply only in the new mode; **Legacy angular mode** controls apply only in the old mode.

These transformations apply to the **left stick**. The right stick retains its existing circular cutoff using the same **Inner deadzone** value. Input and output controls use fractions of full travel (`0.10` means 10%), except **Square roundness** and the legacy angle in degrees. The output response is linear; no selectable response curves are included.

| Control | Default | Effect in DecoupledAxial |
| --- | --- | --- |
| Inner deadzone | `0.05` | Circular region with zero output, independent of the two axial strip widths. |
| X snap width (north/south) | `0.10` | Suppresses X in a vertical strip near north/south outside the center transition. |
| Y snap width (east/west) | `0.10` | Suppresses Y in a horizontal strip near east/west outside the center transition. |
| Center blend width | `0.05` | Size of the small transition around the overlapping strips and inner circle. Larger values restore center response over a larger area. |
| X / Y anti-deadzone | `0` | Separate minimum active output on each axis, useful when the receiving game has a deadzone. |
| X / Y max zone | `1` | Input travel where that axis reaches full scaled output. Lower values reach it sooner. |
| X / Y max output | `1` | Caps each scaled axis before anti-deadzone and squaring; it is not a final output ceiling. |
| Square Stick | `True` | Enables DS4-style circle-to-square expansion after the axis response. |
| Square roundness | `0` | `0` applies full squaring throughout travel; higher values retain a rounder center. Range: `0–1000`. |

The center transition recovers diagonal output that ordinary axial deadzones would suppress. Its outer boundary is the union of a rectangle with half-widths **X snap width + Center blend width** and **Y snap width + Center blend width**, and a circle with radius **Inner deadzone + Center blend width**. Within that boundary, a smooth direction blend connects the input direction near the inner circle to the ordinary axial direction at the boundary, while the pre-response magnitude rises linearly along each input ray. This changes a small surrounding region as well as the exact strip overlap so the recovered output can join the exterior mapping continuously.

Outside this transition, each axis uses the unchanged normalized DS4-style formula: subtract its snap width, divide by **max zone − snap width**, and clamp to `0–1`. Processing then applies the DS4-style max-output cap (including its `/ 0.99` adjustment), anti-deadzone, optional Square Stick, and final clamping. Calculations stay in floating point until virtual-controller encoding; this does not reproduce DS4Windows' intermediate byte rounding. A nonzero anti-deadzone introduces a minimum active output when leaving the inner circle. In the center transition, its floor fades for a component that must become zero at an exterior cardinal strip. Anti-deadzone and squaring can both raise a component above the chosen **max output** value.

**Center blend width** must be `0.000001–1`. Each **snap width + blend width** must be below its matching **max zone**, and **inner deadzone + blend width** must be below both max zones. Anti-deadzones and max outputs must be `0–1`. **Save / Apply** explains which constraint fails if the settings leave no room for the center transition.

## Joystick visualizer

After downloading the source, open [`tools/visualizer/index.html`](tools/visualizer/index.html) in a browser to compare raw input, DS4Windows, and DecoupledAxial. Drag the input point, adjust each mapping's settings, or switch between mapped rings, center zoom, and deadzone/cardinal regions. The visualizer runs independently of the remapper and does not change your profile; its response charts require internet access to the pinned D3 CDN.

The [visualizer guide](tools/visualizer/README.md) covers editing, model validation, and source attribution. Both the editable fragment and standalone browser document are included in the source download.

## Avoid duplicate inputs

The app selects only present gamepad interfaces and hides the selected HID gamepad plus its gamepad-only XInput (`XnaComposite`) parent when applicable. It excludes the outer USB composite container, which may contain keyboard/mouse functions. Restart browsers/games that opened the physical controller before hiding was applied.

**Restore device visibility** removes the selected controller's gamepad hide entries, including a matching current interface after reconnect. It preserves global cloak state and other tools' entries. Restore visibility before uninstalling: uninstalling does not remove shared drivers or hidden-device entries. Real Microsoft Xbox 360 pads with VID/PID `045e:028e` are excluded alongside virtual targets to avoid feedback loops.

## Rocket League Player 1 preparation

Hiding a physical controller does not necessarily free its XInput slot. If the virtual controller is assigned to Player 2:

1. Configure the connected physical controller with **Start** once, then **Stop**.
2. Close Rocket League and unplug the physical controller.
3. Click **Prepare Player 1**. Wait for confirmation that the virtual controller received the first XInput slot.
4. Reconnect the physical controller, wait for **Running**, then launch Rocket League.

Preparation stops with an error if another device occupies the first slot. HID hiding is refreshed after reconnecting. This mode has been built and checked against the ViGEm API, but its full hardware/game workflow still needs validation.

## Build

Requires Windows 10+ x64, .NET 8 SDK and Inno Setup 6 or 7:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File build/Build-Installer.ps1
```

Output: `installer/Output/RLRemapper-Setup.exe`. The build downloads pinned upstream dependencies, verifies SHA-256 hashes and vendor signatures, publishes a self-contained `win-x64` app and bundles dependency installers. Driver/runtime URLs, versions, hashes and publishers are in `installer/driver-packages.json`. SDL2 2.32.10 and the fixed ViGEmClient binary are also hash-pinned in the build script. ViGEmBus is archived.

Optional local tools are recognized at `build/tools/dotnet/dotnet.exe` and `build/tools/inno/ISCC.exe`. Generated binaries, build tools, native dependency caches and diagnostic/render artifacts are ignored by Git. SDL2 and ViGEmClient license notices in `third_party` are included in setup.

## Verification

```powershell
dotnet run --project tests/TransformChecks/TransformChecks.csproj
powershell -NoProfile -ExecutionPolicy Bypass -File tests/InstallerChecks.ps1
```

`MappingEditorChecks` verifies guided prompts, release handling, mapping preservation, save/cancel/reset and renders the editor offscreen. Build first so `publish/SDL2.dll` exists:

```powershell
dotnet run --project tests/MappingEditorChecks/MappingEditorChecks.csproj -- "$PWD"
```

Optional read-only hardware diagnostics: `tests/Inspect-InstalledDrivers.ps1`, `tests/NativePowerShellChecks.ps1` (requires an installed ViGEmBus driver), and `tests/Inspect-XInput.ps1`. Run driver inspection through native 64-bit Windows PowerShell as administrator. Test/render output goes under ignored `artifacts` or `build/tools` directories.

Windows publish, installer compilation, dependency checks and editor checks passed. Live gamepad-only hiding reduced the affected PC's visible XInput controllers from two to one. Guided button capture and Player 1 preparation still require broader hardware testing. No application-loop telemetry or network activity is included. There is no automatic migration of the original Python prototype's INI.
