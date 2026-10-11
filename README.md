# RL Remapper (Windows x64)

Controller remapper with left-stick cardinal snapping, angle remapping, circle-to-square transformation, deadzones and configurable button/axis mappings. It reads a physical controller through SDL2 and sends a virtual Xbox 360 controller through ViGEm. HidHide keeps the physical controller out of games while letting the remapper read it.

## Download and install

Download [RLRemapper-Setup.exe from the latest release](https://github.com/Brendan-Cobb/rl-joystick-mapper/releases/latest/download/RLRemapper-Setup.exe). This repository contains source and build/test code; the release contains the built installer.

Run setup as administrator. It includes the self-contained .NET runtime, SDL2, ViGEmClient, signed ViGEmBus and HidHide installers, and Microsoft's x64 Visual C++ runtime. Missing or incomplete dependencies are installed or repaired. The project installer is unsigned; embedded vendor packages are hash-checked and signature-verified.

Setup uses native 64-bit PowerShell and kernel-driver detection. It checks startup configuration and required files, locates HidHide's CLI and allowlists the installed remapper. Logs are saved under `%ProgramData%\RL Remapper` (`setup-drivers.log` and per-dependency installer logs). If Windows needs a restart before configuration finishes, restart and rerun setup. Setup does not hide devices until you select/start a controller in the app.

## Remap controls

1. Connect the physical controller. With multiple eligible gamepads, use **Select physical device**; otherwise selection is automatic on **Start**.
2. Stop remapping and open **Remap controls → Button setup → Start guided setup**.
3. Follow **Press A / Cross**, **Press B / Circle**, **Press X / Square**, **Press Y / Triangle**, then the remaining prompts. Release each button before the next prompt. Use **Skip this button** for unavailable controls. Physical inputs are identified and assigned automatically.
4. Use **Sticks and triggers → Learn** to identify each stick direction or trigger. **Advanced button mappings** provides manual output selection and disabling individual buttons. D-pad hats are forwarded automatically and recognized in guided setup.
5. **Save mappings** keeps edits; **Cancel** discards them, including a pending **Reset mappings**. Press **Start** when finished. Assign game actions such as Jump and Boost in Rocket League's controls menu.

Snap/deadzone and polling settings remain in the settings grid; **Save / Apply** publishes those settings to the running loop. Settings live in `%LOCALAPPDATA%\RlRemapper\settings.json`. Polling supports 50–1000 Hz without reading config from disk per frame. Turbo jump is not included.

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
