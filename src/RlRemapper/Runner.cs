using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RlRemapper;

internal sealed class Runner
{
    readonly Func<Settings> settings;
    readonly Action<string> status;
    readonly bool waitForPhysical;
    public Runner(Func<Settings> settings, Action<string> status, bool waitForPhysical = false) { this.settings = settings; this.status = status; this.waitForPhysical = waitForPhysical; }
    public void Run(CancellationToken token)
    {
        IntPtr client = IntPtr.Zero, target = IntPtr.Zero, js = IntPtr.Zero;
        bool connected = false, added = false, sdl = false;
        try
        {
            // SDL XInput backend remains accessible to a HidHide-whitelisted process.
            Environment.SetEnvironmentVariable("SDL_JOYSTICK_RAWINPUT", "0");
            Environment.SetEnvironmentVariable("SDL_GAMECONTROLLER_IGNORE_DEVICES", null);
            if (Sdl.SDL_Init(Sdl.Joystick) != 0) throw new InvalidOperationException("SDL joystick initialization failed.");
            sdl = true;
            var cfg = settings(); cfg.Validate();
            // In Player 1 preparation mode the physical pad is unplugged. Allocate
            // the virtual target first so it can claim the first free XInput slot.
            client = Vigem.vigem_alloc(); target = Vigem.vigem_target_x360_alloc();
            if (client == IntPtr.Zero || target == IntPtr.Zero) throw new InvalidOperationException("ViGEm allocation failed.");
            Vigem.Check(Vigem.vigem_connect(client)); connected = true;
            Vigem.Check(Vigem.vigem_target_add(client, target)); added = true;
            var initial = new XusbReport(); Vigem.Check(Vigem.vigem_target_x360_update(client, target, ref initial));
            js = Sdl.OpenPhysical(cfg.ControllerIndex);
            if (waitForPhysical)
            {
                Vigem.Check(Vigem.vigem_target_x360_get_user_index(client, target, out uint slot));
                if (slot != 0) throw new InvalidOperationException($"Virtual controller received slot {slot + 1}, not Player 1. Another controller is reserving the first slot; disconnect it and try again.");
                status("Player 1 virtual controller ready; reconnect the physical controller");
                while (js == IntPtr.Zero && !token.IsCancellationRequested)
                {
                    token.WaitHandle.WaitOne(250);
                    Sdl.SDL_JoystickUpdate(); js = Sdl.OpenPhysical(cfg.ControllerIndex);
                }
                if (token.IsCancellationRequested) return;
                HidHide.ConfigureReconnected(settings().DevicePath);
            }
            if (js == IntPtr.Zero) throw new InvalidOperationException("No physical controller found. Check HidHide whitelist and controller index.");
            string name = Marshal.PtrToStringUTF8(Sdl.SDL_JoystickName(js)) ?? "controller";
            // Record exact SDL GUID for reconnect; never attach to our own ViGEm device.
            string guid = "";
            for (int i = 0, n = 0; i < Sdl.SDL_NumJoysticks(); i++)
                if (!Sdl.Virtual(Sdl.Guid(i)) && n++ == cfg.ControllerIndex) { guid = Sdl.Guid(i); break; }
            status($"Running: {name}");
            var clock = Stopwatch.StartNew();
            long next = clock.ElapsedTicks;
            Settings? parsed = null;
            (int Index, ushort Mask)[] buttons = [];
            while (!token.IsCancellationRequested)
            {
                cfg = settings(); // immutable snapshot published by GUI; no disk I/O per frame
                if (!ReferenceEquals(cfg, parsed)) { buttons = cfg.ParseButtons(); parsed = cfg; }
                Sdl.SDL_JoystickUpdate();
                if (Sdl.SDL_JoystickGetAttached(js) == 0)
                {
                    var zero = new XusbReport(); Vigem.Check(Vigem.vigem_target_x360_update(client, target, ref zero));
                    Sdl.SDL_JoystickClose(js); js = IntPtr.Zero;
                    status("Disconnected; waiting for controller");
                    while (!token.IsCancellationRequested && js == IntPtr.Zero)
                    {
                        Sdl.SDL_JoystickUpdate(); js = Sdl.OpenPhysical(0, guid);
                        if (js == IntPtr.Zero) token.WaitHandle.WaitOne(250);
                    }
                    next = clock.ElapsedTicks;
                    if (js != IntPtr.Zero) status("Reconnected");
                    continue;
                }
                var (x, y) = Transform.ApplyStick(Sdl.Axis(js, cfg.StickXAxis), Sdl.Axis(js, cfg.StickYAxis), cfg);
                double rx = Sdl.Axis(js, cfg.RightStickXAxis), ry = Sdl.Axis(js, cfg.RightStickYAxis);
                if (Math.Sqrt(rx * rx + ry * ry) < cfg.InnerDeadzone) rx = ry = 0;
                var report = new XusbReport {
                    LeftX = Transform.Stick(x), LeftY = Transform.Stick(-y), RightX = Transform.Stick(rx), RightY = Transform.Stick(-ry),
                    LeftTrigger = cfg.LeftTriggerAxis < Sdl.SDL_JoystickNumAxes(js) ? Transform.Trigger(Sdl.Axis(js, cfg.LeftTriggerAxis)) : (byte)0,
                    RightTrigger = cfg.RightTriggerAxis < Sdl.SDL_JoystickNumAxes(js) ? Transform.Trigger(Sdl.Axis(js, cfg.RightTriggerAxis)) : (byte)0
                };
                int count = Sdl.SDL_JoystickNumButtons(js);
                foreach (var (index, mask) in buttons) if (index < count && Sdl.SDL_JoystickGetButton(js, index) != 0) report.Buttons |= mask;
                if (Sdl.SDL_JoystickNumHats(js) > 0)
                {
                    byte hat = Sdl.SDL_JoystickGetHat(js, 0);
                    if ((hat & 1) != 0) report.Buttons |= 1;
                    if ((hat & 4) != 0) report.Buttons |= 2;
                    if ((hat & 8) != 0) report.Buttons |= 4;
                    if ((hat & 2) != 0) report.Buttons |= 8;
                }
                Vigem.Check(Vigem.vigem_target_x360_update(client, target, ref report));
                long period = Stopwatch.Frequency / cfg.PollRateHz;
                next += period;
                long remaining = next - clock.ElapsedTicks;
                if (remaining > Stopwatch.Frequency / 500) token.WaitHandle.WaitOne((int)(remaining * 1000 / Stopwatch.Frequency) - 1);
                while (clock.ElapsedTicks < next && !token.IsCancellationRequested) Thread.SpinWait(32);
                if (clock.ElapsedTicks - next > period) next = clock.ElapsedTicks; // no catch-up burst
            }
        }
        finally
        {
            if (added) { var zero = new XusbReport(); Vigem.vigem_target_x360_update(client, target, ref zero); Vigem.vigem_target_remove(client, target); }
            if (connected) Vigem.vigem_disconnect(client);
            if (target != IntPtr.Zero) Vigem.vigem_target_free(target);
            if (client != IntPtr.Zero) Vigem.vigem_free(client);
            if (js != IntPtr.Zero) Sdl.SDL_JoystickClose(js);
            if (sdl) Sdl.SDL_Quit();
        }
    }
}
