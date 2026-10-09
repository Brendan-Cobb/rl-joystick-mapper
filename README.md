# RL Remapper (Windows x64)

C#/.NET 8 Windows Forms port of the supplied Python prototype. Left-stick cardinal snap, angle remap and circle-to-square; deadzone, right stick, triggers, button mapping and hat D-pad forwarding; reconnect zeroes output. Turbo jump intentionally removed. Settings live in `%LOCALAPPDATA%\RlRemapper\settings.json` and are applied with **Save / Apply** while running. 50–1000 Hz polling with native SDL2 joystick reads and a ViGEm Xbox 360 target; the loop never reads config from disk. Runs without elevation after driver installation.

## Build and package (Windows x64)

For maintainers building from source: install .NET 8 SDK and Inno Setup 6 or 7 on Windows x64, then from the repository root run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File build/Build-Installer.ps1
```

The build script downloads pinned x64 SDL2 2.32.10 from the upstream SDL release and x64 ViGEmClient.dll from a fixed ViGEm.NET upstream commit, verifies SHA-256 hashes, copies their licenses, performs a self-contained `win-x64` publish and compiles `installer/Output/RLRemapper-Setup.exe`. Native DLLs **are included in the resulting installer**; end users do not fetch them. The Windows build-script workflow has not been executed on Windows. A setup executable was cross-compiled with Inno Setup 7 under Wine on Linux; actual driver installation and controller behavior still require testing on Windows hardware. The compiled installer is not code-signed by this project, so Windows may show an unrecognized-app warning.

The elevated Inno installer checks the ViGEmBus and HidHide services, downloads missing x64 installers from the latest official `nefarius` GitHub releases, validates Authenticode publisher/signature, launches the vendor installers and verifies service presence. It then locates HidHide's CLI and registers the exact installed remapper executable in its application allowlist. It **does not** hide every controller or turn on global inverse mode. Setup fails closed if download, signing, driver checks or allowlisting fails. A vendor installer may still show prompts/UAC; a reboot may be required. ViGEmBus is archived: assess its trust/support before installation. The app and driver installer require Windows 10+ x64. No manual HidHide configuration client is required.

## First run

1. Run the installer as administrator. Once installed, plug in a physical controller. Start RL Remapper, then **Start**: if exactly one eligible physical device is detected, it is selected automatically. For multiple devices, use **Select physical device** first. The app uses HidHideCLI to hide only that device and enable the cloak, after confirming its own allowlist entry. If Windows requires it, unplug/replug the device once to apply filtering. You never need to open HidHide or ViGEm manually.
2. Use **Inspect controller** to observe live axis/button indices and hat; edit settings in the property grid, including `ButtonMappings` (`index=name` per line), then **Save / Apply** and **Start**. Stop before inspecting again. Default axis layout matches the supplied INI, but varies by controller. **Restore device visibility** removes the selected device from HidHide (global cloak remains unchanged; other tools might rely on it). If the process crashes, use that button after restarting to restore visibility.
3. Select a non-virtual controller with `ControllerIndex` (zero-based). A real Microsoft Xbox 360 pad with VID/PID 045e:028e is excluded along with ViGEm targets to avoid feedback loops. Reconnect matches SDL GUID; if slot/name changes, stop and restart. For Rocket League keep in-game deadzone at 0–0.01 if using the app deadzone.
4. Stop before quitting. Uninstalling the app does not uninstall system drivers or remove hidden device entries; restore visibility in the app before uninstalling.

No Windows hardware tests were possible in the Linux development environment. Check SDL2/ViGEmClient ABI and driver behavior on a real Windows x64 system before distributing. The driver script requires signed official installers; release asset names can change and installation can fail safely. There is no automatic migration of the old INI; copy axis/button values into the GUI manually. No telemetry or network activity in the application loop.
