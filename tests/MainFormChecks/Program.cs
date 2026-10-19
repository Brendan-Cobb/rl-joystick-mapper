using System.Reflection;
using RlRemapper;

internal static class Checks
{
    [STAThread]
    static void Main(string[] args)
    {

        VirtualControllerChecks();
        ApplicationConfiguration.Initialize();
        var type = typeof(Settings).Assembly.GetType("RlRemapper.MainForm", true)!;
        using var form = (Form)Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null, [false], null)!;
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(-32000, -32000);
        form.ShowInTaskbar = false;
        form.Show(); Application.DoEvents();
        T Field<T>(string name) => (T)type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
        void Call(string name, params object[] values) => type.GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, values);
        var start = Field<Button>("start");
        var apply = Field<Button>("save");
        Assert(start.Text == "Start remapping" && !apply.Enabled, "Idle actions incorrect");
        var grid = Field<PropertyGrid>("grid");
        var properties = System.ComponentModel.TypeDescriptor.GetProperties(grid.SelectedObject);
        foreach (string category in new[] { "Radial", "Axial", "Angular" })
            Assert(properties.Cast<System.ComponentModel.PropertyDescriptor>().Any(p => p.Category == category && p.DisplayName == "Inner deadzone"), "Profile missing its deadzone setting");
        Assert(properties.Cast<System.ComponentModel.PropertyDescriptor>().Where(p => p.Name.Contains("XSnapWidth") || p.Name.Contains("XMaxZone") || p.Name.Contains("YMaxOutput")).All(p => p.Category == "Axial"), "Axial settings are scattered");
        properties["Radial.RadialMaxOutput"]!.SetValue(grid.SelectedObject, .8);
        Assert(Field<Settings>("current").RadialMaxOutput == .8, "Grouped setting did not edit the underlying profile");
        Assert(properties["Angular.AngularMaxZone"]?.Category == "Angular" && properties["Angular.AngularSquareOutput"]?.Category == "Angular", "Angular controls missing from grouped settings");
        properties["Angular.AngularSquareOutput"]!.SetValue(grid.SelectedObject, false);
        Assert(!Field<Settings>("current").AngularSquareOutput && Field<Settings>("current").SquareStick, "Angular shape changed another profile");
        Assert(Enum.GetNames<StickMappingMode>().Order().SequenceEqual(new[] { "Angular", "Axial", "Radial" }), "Old profile names remain");

        typeof(PropertyGrid).GetMethod("OnPropertyValueChanged", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(grid, [new PropertyValueChangedEventArgs(null!, null)]);
        Assert(apply.Enabled && Field<Label>("state").Text == "Remapping off", "Editing overwrote status or failed to enable Apply");
        Call("SetBusy", true);
        Assert(start.Text == "Stop remapping" && !Field<Button>("select").Enabled && !Field<Button>("remap").Enabled, "Running controls incorrect");
        using var cancel = new CancellationTokenSource();
        type.GetField("cancellation", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(form, cancel);
        var closing = new FormClosingEventArgs(CloseReason.UserClosing, false);
        Call("OnFormClosing", closing);
        Assert(closing.Cancel && cancel.IsCancellationRequested && !start.Enabled, "Close did not wait for cleanup");
        Call("SetBusy", false);
        Field<Label>("state").Text = "Remapping off";
        Field<Label>("controller").Text = "Controller: Xbox Controller - Connected";
        Field<Label>("notice").Text = "Visibility unchanged. Use Troubleshooting to restore it.";
        grid.Enabled = true;
        var rootItem = grid.SelectedGridItem;
        while (rootItem?.Parent != null) rootItem = rootItem.Parent;
        if (rootItem?.GridItems.Count > 0) grid.SelectedGridItem = rootItem.GridItems[0].GridItems[0];
        form.CreateControl(); form.PerformLayout();
        Directory.CreateDirectory(args[0]);
        foreach (int width in new[] { 650, 540 }) {
            form.Width = width; form.PerformLayout();
            using var bitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            bitmap.Save(Path.Combine(args[0], $"main-{width}.png"));
        }
        int restorations = 0;
        Func<string, Task<string?>> restore = _ => { restorations++; return Task.FromResult<string?>(null); };
        using var manual = (Form)Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null, [false, restore], null)!;
        type.GetMethod("Stop", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(manual, null);
        Assert(restorations == 0, "Stop restored visibility automatically");
        var idleClose = new FormClosingEventArgs(CloseReason.UserClosing, false);
        type.GetMethod("OnFormClosing", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(manual, [idleClose]);
        Assert(!idleClose.Cancel && restorations == 0, "Idle close restored visibility or was delayed");
        ((Task)type.GetMethod("RestoreControllerVisibility", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(manual, null)!).GetAwaiter().GetResult();
        Assert(restorations == 1, "Explicit restoration did not run exactly once");
        Console.WriteLine("Main form, manual-only visibility, and virtual-controller lifecycle checks passed.");
    }
    sealed class FakeConnection : IVirtualControllerConnection
    {
        public uint Slot => 0;
        public int Closes;
        public XusbReport Last;
        public void Send(XusbReport report) { Last = report; }
        public void Dispose() { Closes++; }
    }
    static void VirtualControllerChecks()
    {
        var connections = new List<FakeConnection>();
        using var output = new VirtualController(() => { var c = new FakeConnection(); connections.Add(c); return c; });
        output.Neutralize(); // Closing without ever starting must not create a controller.
        Assert(connections.Count == 0, "Idle cleanup created a controller");
        for (int i = 0; i < 3; i++) {
            output.EnsureConnected();
            output.Send(new XusbReport { Buttons = 0x1000, LeftX = 20000, LeftTrigger = 255 });
            output.Neutralize();
            Assert(connections.Count == 1 && connections[0].Closes == 0 && output.Slot == 0, "Stop/restart released the reserved slot");
            Assert(connections[0].Last.Buttons == 0 && connections[0].Last.LeftX == 0 && connections[0].Last.LeftTrigger == 0, "Stop left inputs held");
        }
        output.Dispose(); output.Dispose();
        Assert(connections[0].Closes == 1, "Closing did not release the virtual controller exactly once");
        output.EnsureConnected();
        Assert(connections.Count == 2, "Player 1 repair could not recreate the controller");
        output.Dispose();
        int attempts = 0;
        using var retry = new VirtualController(() => { if (attempts++ == 0) throw new InvalidOperationException(); return new FakeConnection(); });
        try { retry.EnsureConnected(); } catch (InvalidOperationException) { }
        retry.EnsureConnected();
        Assert(attempts == 2, "Failed creation prevented retry");
    }
    static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
}
