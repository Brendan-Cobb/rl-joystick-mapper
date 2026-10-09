using System.Runtime.InteropServices;

namespace RlRemapper;

internal static class Sdl
{
    const string Dll = "SDL2.dll";
    public const uint Joystick = 0x200;
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern int SDL_Init(uint flags);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern void SDL_Quit();
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern int SDL_NumJoysticks();
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr SDL_JoystickOpen(int index);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern void SDL_JoystickClose(IntPtr joystick);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern int SDL_JoystickGetAttached(IntPtr joystick);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern void SDL_JoystickUpdate();
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern short SDL_JoystickGetAxis(IntPtr joystick, int axis);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern byte SDL_JoystickGetButton(IntPtr joystick, int button);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern byte SDL_JoystickGetHat(IntPtr joystick, int hat);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern int SDL_JoystickNumAxes(IntPtr joystick);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern int SDL_JoystickNumButtons(IntPtr joystick);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern int SDL_JoystickNumHats(IntPtr joystick);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr SDL_JoystickName(IntPtr joystick);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern GuidBytes SDL_JoystickGetDeviceGUID(int index);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern void SDL_JoystickGetGUIDString(GuidBytes guid, System.Text.StringBuilder buffer, int size);
    [StructLayout(LayoutKind.Sequential)] public struct GuidBytes { public ulong A, B; }
    public static string Guid(int index) { var b = new System.Text.StringBuilder(33); SDL_JoystickGetGUIDString(SDL_JoystickGetDeviceGUID(index), b, b.Capacity); return b.ToString(); }
    public static bool Virtual(string guid) => guid.Length >= 20 && guid.Substring(8, 4).Equals("5e04", StringComparison.OrdinalIgnoreCase) && guid.Substring(16, 4).Equals("8e02", StringComparison.OrdinalIgnoreCase);
    public static IntPtr OpenPhysical(int index, string? match = null)
    {
        int count = 0;
        for (int i = 0; i < SDL_NumJoysticks(); i++)
        {
            string guid = Guid(i);
            if (Virtual(guid) || (match != null && guid != match)) continue;
            if (match == null && count++ != index) continue;
            return SDL_JoystickOpen(i);
        }
        return IntPtr.Zero;
    }
    public static double Axis(IntPtr js, int index) => index < SDL_JoystickNumAxes(js) ? Math.Clamp(SDL_JoystickGetAxis(js, index) / 32767.0, -1, 1) : 0;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct XusbReport
{
    public ushort Buttons;
    public byte LeftTrigger, RightTrigger;
    public short LeftX, LeftY, RightX, RightY;
}
internal static class Vigem
{
    const string Dll = "ViGEmClient.dll";
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr vigem_alloc();
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern void vigem_free(IntPtr client);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern uint vigem_connect(IntPtr client);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern void vigem_disconnect(IntPtr client);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern IntPtr vigem_target_x360_alloc();
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern void vigem_target_free(IntPtr target);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern uint vigem_target_add(IntPtr client, IntPtr target);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern void vigem_target_remove(IntPtr client, IntPtr target);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] public static extern uint vigem_target_x360_update(IntPtr client, IntPtr target, ref XusbReport report);
    // ViGEm success is 0x20000000 (VIGEM_ERROR_NONE).
    public static void Check(uint result) { if (result != 0x20000000) throw new InvalidOperationException($"ViGEm error 0x{result:X8}"); }
}
