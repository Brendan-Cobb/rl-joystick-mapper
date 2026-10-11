using System.Text;

namespace RlRemapper;

internal static class Program
{
    [STAThread]
    static void Main() { ApplicationConfiguration.Initialize(); Application.Run(new MainForm()); }
}
internal sealed class MainForm : Form
{
    Settings current;
    readonly PropertyGrid grid = new() { Dock = DockStyle.Fill, HelpVisible = true };
    readonly Label state = new() { AutoSize = true, Text = "Stopped" };
    readonly Button start = new() { Text = "Start", AutoSize = true };
    readonly Button stop = new() { Text = "Stop", AutoSize = true, Enabled = false };
    CancellationTokenSource? cancellation;
    Task? task;
    public MainForm()
    {
        Text = "RL Remapper"; Width = 650; Height = 680;
        try { current = Settings.Load(); current.Validate(); }
        catch (Exception e) { current = new Settings(); MessageBox.Show("Invalid saved settings; using defaults: " + e.Message); }
        grid.SelectedObject = current;
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
        var save = new Button { Text = "Save / Apply", AutoSize = true };
        save.Click += (_, _) => {
            try { grid.Focus(); current.Save(); Volatile.Write(ref active, current.Snapshot()); state.Text = "Settings saved/applied"; }
            catch (Exception e) { MessageBox.Show(e.Message, "Invalid settings"); }
        };
        var detect = new Button { Text = "Inspect controller", AutoSize = true };
        var remap = new Button { Text = "Remap controls", AutoSize = true };
        remap.Click += (_, _) => {
            if (task is { IsCompleted: false }) { MessageBox.Show("Stop remapping before editing controls."); return; }
            using var editor = new ControlMappingForm(current);
            if (editor.ShowDialog(this) != DialogResult.OK) return;
            try { editor.Result.Save(); current = editor.Result; grid.SelectedObject = current; Volatile.Write(ref active, current.Snapshot()); state.Text = "Control mappings saved"; }
            catch (Exception error) { MessageBox.Show(error.Message, "Could not save mappings"); }
        };
        detect.Click += (_, _) => Inspect();
        var select = new Button { Text = "Select physical device", AutoSize = true };
        select.Click += (_, _) => SelectDevice();
        var unhide = new Button { Text = "Restore device visibility", AutoSize = true };
        unhide.Click += (_, _) => {
            if (task is { IsCompleted: false }) { MessageBox.Show("Stop first."); return; }
            try { HidHide.Unhide(current.DevicePath); state.Text = "Device visible again"; }
            catch (Exception e) { MessageBox.Show(e.Message); }
        };
        start.Click += (_, _) => Start();
        var prepare = new Button { Text = "Prepare Player 1", AutoSize = true };
        prepare.Click += (_, _) => Start(true);
        stop.Click += (_, _) => cancellation?.Cancel();
        bar.Controls.AddRange(new Control[] { start, prepare, stop, remap, save, select, unhide, detect, state });
        Controls.Add(grid); Controls.Add(bar);
        active = current.Snapshot();
    }
    Settings active;
    void SelectDevice()
    {
        if (task is { IsCompleted: false }) { MessageBox.Show("Stop remapping first."); return; }
        try
        {
            var devices = HidHide.GamingDevices();
            if (devices.Count == 0) throw new InvalidOperationException("No physical gaming devices found. Plug in the controller and restart if drivers were just installed.");
            using var dialog = new Form { Text = "Select only the controller to remap", Width = 850, Height = 260 };
            var list = new ListBox { Dock = DockStyle.Fill, DataSource = devices };
            var confirm = new Button { Dock = DockStyle.Bottom, Text = "Use selected device", DialogResult = DialogResult.OK };
            dialog.Controls.Add(list); dialog.Controls.Add(confirm); dialog.AcceptButton = confirm;
            if (dialog.ShowDialog(this) != DialogResult.OK || list.SelectedItem is not HidHide.Device chosen) return;
            current.DevicePath = chosen.Path; grid.Refresh(); current.Save();
            Volatile.Write(ref active, current.Snapshot()); state.Text = $"Selected: {chosen.Name}";
        }
        catch (Exception e) { MessageBox.Show(e.Message, "Controller selection failed"); }
    }
    void Start(bool preparePlayerOne = false)
    {
        if (task is { IsCompleted: false }) { MessageBox.Show("Stop remapping first."); return; }
        try { current.Validate(); current.Save(); Volatile.Write(ref active, current.Snapshot()); }
        catch (Exception e) { MessageBox.Show(e.Message); return; }
        try
        {
            var candidates = HidHide.GamingDevices();
            if (preparePlayerOne)
            {
                if (candidates.Count != 0) throw new InvalidOperationException("Close Rocket League, unplug the physical controller, then click Prepare Player 1. Reconnect it once the virtual controller is ready.");
                HidHide.PrepareDisconnected(current.DevicePath);
            }
            else
            {
            if (!candidates.Any(device => device.Path.Equals(current.DevicePath, StringComparison.OrdinalIgnoreCase)))
            {
                if (candidates.Count != 1) throw new InvalidOperationException("Select the physical gamepad before starting (automatic selection requires exactly one candidate). The saved selection may be an old or non-gamepad interface.");
                current.DevicePath = candidates[0].Path;
                current.Save(); grid.Refresh(); Volatile.Write(ref active, current.Snapshot());
            }
            HidHide.Configure(current.DevicePath);
            }
        }
        catch (Exception e) { MessageBox.Show(e.Message, "HidHide setup failed"); return; }
        cancellation = new CancellationTokenSource();
        var ct = cancellation.Token;
        start.Enabled = false; stop.Enabled = true;
        task = Task.Run(() => new Runner(() => Volatile.Read(ref active), text => {
            if (!IsDisposed && IsHandleCreated) BeginInvoke(() => state.Text = text);
        }, preparePlayerOne).Run(ct));
        task.ContinueWith(t => {
            if (IsDisposed || !IsHandleCreated) return;
            BeginInvoke(() => { state.Text = t.Exception?.GetBaseException().Message ?? "Stopped"; start.Enabled = true; stop.Enabled = false; });
        }, TaskScheduler.Default);
    }
    void Inspect()
    {
        if (task is { IsCompleted: false }) { MessageBox.Show("Stop remapping before inspecting the controller."); return; }
        try
        {
            Environment.SetEnvironmentVariable("SDL_JOYSTICK_RAWINPUT", "0");
            if (Sdl.SDL_Init(Sdl.Joystick) != 0) throw new InvalidOperationException("SDL initialization failed");
            using var dialog = new Form { Text = "Axis / button inspector (move controls)", Width = 700, Height = 320 };
            var label = new Label { Dock = DockStyle.Fill, Font = new Font("Consolas", 10), AutoSize = false };
            dialog.Controls.Add(label);
            IntPtr js = Sdl.OpenPhysical(current.ControllerIndex);
            if (js == IntPtr.Zero) throw new InvalidOperationException("No physical controller found");
            using var timer = new System.Windows.Forms.Timer { Interval = 50 };
            timer.Tick += (_, _) => {
                Sdl.SDL_JoystickUpdate();
                if (Sdl.SDL_JoystickGetAttached(js) == 0) { label.Text = "Disconnected"; return; }
                var b = new StringBuilder();
                b.AppendLine(System.Runtime.InteropServices.Marshal.PtrToStringUTF8(Sdl.SDL_JoystickName(js)));
                for (int i = 0; i < Sdl.SDL_JoystickNumAxes(js); i++) b.Append($"Axis {i}: {Sdl.Axis(js, i):+0.000;-0.000;0.000}    ");
                b.AppendLine();
                for (int i = 0; i < Sdl.SDL_JoystickNumButtons(js); i++) if (Sdl.SDL_JoystickGetButton(js, i) != 0) b.Append($"Button {i}   ");
                if (Sdl.SDL_JoystickNumHats(js) > 0) b.Append($"Hat: {Sdl.SDL_JoystickGetHat(js, 0)}");
                label.Text = b.ToString();
            };
            timer.Start(); dialog.ShowDialog(this); timer.Stop(); Sdl.SDL_JoystickClose(js);
        }
        catch (Exception e) { MessageBox.Show(e.Message); }
        finally { Sdl.SDL_Quit(); }
    }
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        cancellation?.Cancel();
        if (task is { IsCompleted: false }) { e.Cancel = true; state.Text = "Stopping…"; return; }
        base.OnFormClosing(e);
    }
}
