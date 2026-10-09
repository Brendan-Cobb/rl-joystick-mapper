# RL Remapper (Windows x64)

C#/.NET 8 Windows Forms port of the supplied Python prototype. Left-stick cardinal snap, angle remap and circle-to-square; deadzone, right stick, triggers, button mapping and hat D-pad forwarding; reconnect zeroes output. Turbo jump intentionally removed. Settings live in `%LOCALAPPDATA%\RlRemapper\settings.json` and are applied with **Save / Apply** while running. 50–1000 Hz polling with native SDL2 joystick reads and a ViGEm Xbox 360 target; the loop never reads config from disk. Runs without elevation after driver installation.

## Build and package (Windows x64)

Install .NET 8 SDK and Inno Setup 6. Obtain **x64** `SDL2.dll` from the official SDL2 release and **x64** `ViGEmClient.dll` from the official ViGEmClient SDK release; review provenance and licenses before redistribution. Put both DLLs in `publish/` next to the exe after publishing. (They are NOT present in this source repository.) Do not use untrusted DLL download sites. Then:

```powershell
dotnet publish src/RlRemapper/RlRemapper.csproj -c Release -r win-x64 --self-contained true -o publish
# Copy the verified x64 SDL2.dll and ViGEmClient.dll to publish\
if (!(Test-Path publish/SDL2.dll) -or !(Test-Path publish/ViGEmClient.dll)) { throw 'Missing native DLLs' }
iscc installer/RlRemapper.iss
```

The elevated Inno installer checks the ViGEmBus and HidHide services, downloads missing x64 installers from the latest official `nefarius` GitHub releases, validates Authenticode publisher/signature, launches the vendor installers and verifies service presence. It then locates HidHide's CLI and registers the exact installed remapper executable in its application allowlist. It **does not** hide every controller or turn on global inverse mode. Setup fails closed if download, signing, driver checks or allowlisting fails. A vendor installer may still show prompts/UAC; a reboot may be required. ViGEmBus is archived: assess its trust/support before installation. The app and driver installer require Windows 10+ x64. No manual HidHide configuration client is required.

## First run

1. Run the installer as administrator. Once installed, plug in a physical controller. Start RL Remapper, then **Start**: if exactly one eligible physical device is detected, it is selected automatically. For multiple devices, use **Select physical device** first. The app uses HidHideCLI to hide only that device and enable the cloak, after confirming its own allowlist entry. If Windows requires it, unplug/replug the device once to apply filtering. You never need to open HidHide or ViGEm manually.
2. Use **Inspect controller** to observe live axis/button indices and hat; edit settings in the property grid, including `ButtonMappings` (`index=name` per line), then **Save / Apply** and **Start**. Stop before inspecting again. Default axis layout matches the supplied INI, but varies by controller. **Restore device visibility** removes the selected device from HidHide (global cloak remains unchanged; other tools might rely on it). If the process crashes, use that button after restarting to restore visibility.
3. Select a non-virtual controller with `ControllerIndex` (zero-based). A real Microsoft Xbox 360 pad with VID/PID 045e:028e is excluded along with ViGEm targets to avoid feedback loops. Reconnect matches SDL GUID; if slot/name changes, stop and restart. For Rocket League keep in-game deadzone at 0–0.01 if using the app deadzone.
4. Stop before quitting. Uninstalling the app does not uninstall system drivers or remove hidden device entries; restore visibility in the app before uninstalling.

No Windows hardware tests were possible in the Linux development environment. Check SDL2/ViGEmClient ABI and driver behavior on a real Windows x64 system before distributing. The driver script requires signed official installers; release asset names can change and installation can fail safely. There is no automatic migration of the old INI; copy axis/button values into the GUI manually. No telemetry or network activity in the application loop.
