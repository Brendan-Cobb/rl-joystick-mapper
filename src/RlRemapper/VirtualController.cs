namespace RlRemapper;

internal interface IVirtualControllerConnection : IDisposable
{
    void Send(XusbReport report);
    uint Slot { get; }
}

// Owned by the window, not an individual run. A neutral connected target retains
// its XInput slot while the physical controller remains hidden between runs.
internal sealed class VirtualController : IDisposable
{
    readonly Func<IVirtualControllerConnection> connect;
    IVirtualControllerConnection? connection;
    internal VirtualController(Func<IVirtualControllerConnection>? connect = null) => this.connect = connect ?? (() => new NativeConnection());
    internal void EnsureConnected() => connection ??= connect();
    internal uint Slot => (connection ?? throw new InvalidOperationException("Virtual controller is not connected.")).Slot;
    internal void Send(XusbReport report) => (connection ?? throw new InvalidOperationException("Virtual controller is not connected.")).Send(report);
    internal void Neutralize() { if (connection != null) connection.Send(default); }
    public void Dispose()
    {
        var old = connection; connection = null;
        old?.Dispose();
    }

    sealed class NativeConnection : IVirtualControllerConnection
    {
        IntPtr client, target;
        bool connected, added;
        public NativeConnection()
        {
            try {
                client = Vigem.vigem_alloc(); target = Vigem.vigem_target_x360_alloc();
                if (client == IntPtr.Zero || target == IntPtr.Zero) throw new InvalidOperationException("ViGEm allocation failed.");
                Vigem.Check(Vigem.vigem_connect(client)); connected = true;
                Vigem.Check(Vigem.vigem_target_add(client, target)); added = true;
                Send(default);
            } catch { Dispose(); throw; }
        }
        public uint Slot { get { Vigem.Check(Vigem.vigem_target_x360_get_user_index(client, target, out uint slot)); return slot; } }
        public void Send(XusbReport report) => Vigem.Check(Vigem.vigem_target_x360_update(client, target, ref report));
        public void Dispose()
        {
            if (added) { var zero = new XusbReport(); Vigem.vigem_target_x360_update(client, target, ref zero); Vigem.vigem_target_remove(client, target); added = false; }
            if (connected) { Vigem.vigem_disconnect(client); connected = false; }
            if (target != IntPtr.Zero) { Vigem.vigem_target_free(target); target = IntPtr.Zero; }
            if (client != IntPtr.Zero) { Vigem.vigem_free(client); client = IntPtr.Zero; }
        }
    }
}
