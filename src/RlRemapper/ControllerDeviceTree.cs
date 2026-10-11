using System.Runtime.InteropServices;
using System.Text;

namespace RlRemapper;

internal static class ControllerDeviceTree
{
    internal sealed record Node(string Path, string Class);
    // XInput opens the XnaComposite parent, not only its synthetic HID gamepad.
    // Never hide the outer USB composite container: it may also carry a mouse/keyboard.
    internal static string[] HidePaths(string path, IEnumerable<Node> ancestors) =>
        new[] { path }.Concat(ancestors.Where(n => n.Class.Equals("XnaComposite", StringComparison.OrdinalIgnoreCase))
            .Take(1).Select(n => n.Path)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    internal static string[] HidePaths(string path) => HidePaths(path, Ancestors(path));

    static IEnumerable<Node> Ancestors(string path)
    {
        // PHANTOM permits Restore visibility to work after the HID interface changes.
        uint result = CM_Locate_DevNodeW(out uint node, path, 1);
        if (result != 0) throw new InvalidOperationException($"Cannot locate controller in Windows device tree (0x{result:X}).");
        for (int depth = 0; depth < 16; depth++)
        {
            result = CM_Get_Parent(out uint parent, node, 0);
            if (result == 0x0D) yield break; // CR_NO_SUCH_DEVNODE at root
            if (result != 0) throw new InvalidOperationException($"Cannot read controller parent (0x{result:X}).");
            var id = new StringBuilder(512);
            result = CM_Get_Device_IDW(parent, id, id.Capacity, 0);
            if (result != 0) throw new InvalidOperationException($"Cannot read controller parent ID (0x{result:X}).");
            byte[] buffer = new byte[512];
            uint length = (uint)buffer.Length;
            // CM_DRP_CLASS = 8, REG_SZ encoded as UTF-16.
            result = CM_Get_DevNode_Registry_PropertyW(parent, 8, out _, buffer, ref length, 0);
            if (result != 0) throw new InvalidOperationException($"Cannot read controller parent class (0x{result:X}).");
            string deviceClass = Encoding.Unicode.GetString(buffer, 0, (int)length).TrimEnd('\0');
            yield return new Node(id.ToString(), deviceClass);
            if (deviceClass.Equals("XnaComposite", StringComparison.OrdinalIgnoreCase) ||
                deviceClass.Equals("USB", StringComparison.OrdinalIgnoreCase)) yield break;
            node = parent;
        }
    }

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    static extern uint CM_Locate_DevNodeW(out uint node, string id, uint flags);
    [DllImport("cfgmgr32.dll", ExactSpelling = true)]
    static extern uint CM_Get_Parent(out uint parent, uint node, uint flags);
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    static extern uint CM_Get_Device_IDW(uint node, StringBuilder id, int length, uint flags);
    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    static extern uint CM_Get_DevNode_Registry_PropertyW(uint node, uint property, out uint type, [Out] byte[] buffer, ref uint length, uint flags);
}
