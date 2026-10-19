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
    readonly PropertyGrid grid = new() { Dock = DockStyle.Fill, HelpVisible = true, PropertySort = PropertySort.Categorized };
    readonly Label state = new() { AutoSize = true, Text = "Remapping off", Margin = new Padding(8) };
    readonly Label controller = new() { AutoSize = true, Text = "Controller: connect a controller", Margin = new Padding(8) };
    readonly Label notice = new() { AutoSize = true, Margin = new Padding(8) };
    readonly Button start = new() { Text = "Start remapping", AutoSize = true, BackColor = Color.LightBlue, Padding = new Padding(8, 4, 8, 4) };
    readonly Button save = new() { Text = "Apply changes", AutoSize = true, Enabled = false };
    readonly Button remap = new() { Text = "Remap controls", AutoSize = true };
    readonly Button select = new() { Text = "Change…", AutoSize = true };
    readonly Button troubleshooting = new() { Text = "Troubleshooting ▾", AutoSize = true };
    bool busy, closeRequested, allowClose;
    CancellationTokenSource? cancellation;
    Task? task;
    readonly VirtualController output = new();
    readonly Func<string, Task<string?>> restoreVisibility;
    public MainForm() : this(true) { }
    internal MainForm(bool refreshDevices) : this(refreshDevices, RestoreVisibility) { }
    internal MainForm(bool refreshDevices, Func<string, Task<string?>> restoreVisibility)
    {
        this.restoreVisibility = restoreVisibility;
        Text = "RL Remapper"; Width = 650; Height = 680;
        try { current = Settings.Load(); current.Validate(); }
        catch (Exception e) { current = Settings.CreateDefault(); MessageBox.Show("Invalid saved settings; using defaults: " + e.Message); }
        grid.SelectedObject = new SettingsView(current);
        MinimumSize = new Size(540, 480);
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(8) };
        grid.PropertyValueChanged += (_, _) => { save.Enabled = true; notice.Text = "Unapplied changes"; };
        save.Click += (_, _) => {
            try { grid.Focus(); current.Save(); Volatile.Write(ref active, current.Snapshot()); notice.Text = "Changes applied"; save.Enabled = false; }
            catch (Exception e) { MessageBox.Show(e.Message, "Invalid settings"); }
        };
        remap.Click += (_, _) => {
            if (task is { IsCompleted: false }) { MessageBox.Show("Stop remapping before editing controls."); return; }
            using var editor = new ControlMappingForm(current);
            if (editor.ShowDialog(this) != DialogResult.OK) return;
            try { editor.Result.Save(); current = editor.Result; grid.SelectedObject = new SettingsView(current); Volatile.Write(ref active, current.Snapshot()); notice.Text = "Control mappings saved"; save.Enabled = false; }
            catch (Exception error) { MessageBox.Show(error.Message, "Could not save mappings"); }
        };
        select.Click += (_, _) => SelectDevice();
        start.Click += (_, _) => { if (busy) Stop(); else Start(); };
        var menu = new ContextMenuStrip();
        menu.Items.Add("Fix Player 2 assignment…", null, (_, _) => {
            if (string.IsNullOrWhiteSpace(current.DevicePath) && !SelectDevice()) return;
            if (MessageBox.Show(this, "1. Close Rocket League.\n2. Unplug your controller.\n3. Click OK and wait for the reconnect prompt.\n4. Reconnect your controller, then launch Rocket League once remapping is active.", "Fix Player 2 assignment", MessageBoxButtons.OKCancel) == DialogResult.OK) Start(true);
        });
        menu.Items.Add("View controller inputs…", null, (_, _) => Inspect());
        menu.Items.Add("Restore controller visibility…", null, async (_, _) => await RestoreControllerVisibility());
        troubleshooting.Click += (_, _) => menu.Show(troubleshooting, new Point(0, troubleshooting.Height));
        var deviceRow = new FlowLayoutPanel { AutoSize = true };
        deviceRow.Controls.AddRange([controller, select]);
        var actions = new FlowLayoutPanel { AutoSize = true };
        actions.Controls.AddRange([start, remap, troubleshooting]);
        bar.Controls.AddRange([deviceRow, actions, state]);
        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(8) };
        footer.Controls.AddRange([save, notice]);
        Controls.Add(grid); Controls.Add(footer); Controls.Add(bar);
        void FitLabels() {
            int width = Math.Max(200, ClientSize.Width - 70);
            controller.MaximumSize = new Size(width - select.Width - 20, 0);
            state.MaximumSize = notice.MaximumSize = new Size(width, 0);
        }
        Resize += (_, _) => FitLabels();
        FitLabels();
        active = current.Snapshot();
        if (refreshDevices) Shown += (_, _) => RefreshController();
        FormClosed += (_, _) => { menu.Dispose(); cancellation?.Dispose(); output.Dispose(); };
    }
    Settings active;
    bool SelectDevice()
    {
        if (busy) return false;
        try
        {
            var devices = HidHide.GamingDevices();
            if (devices.Count == 0) throw new InvalidOperationException("No physical gaming devices found. Plug in the controller and restart if drivers were just installed.");
            using var dialog = new Form { Text = "Choose your controller", Width = 540, Height = 260, StartPosition = FormStartPosition.CenterParent };
            var list = new ListBox { Dock = DockStyle.Fill, DataSource = devices, DisplayMember = "Name" };
            var confirm = new Button { Dock = DockStyle.Bottom, Text = "Use selected device", DialogResult = DialogResult.OK };
            dialog.Controls.Add(list); dialog.Controls.Add(confirm); dialog.AcceptButton = confirm;
            if (dialog.ShowDialog(this) != DialogResult.OK || list.SelectedItem is not HidHide.Device chosen) return false;
            current.DevicePath = chosen.Path; grid.Refresh(); current.Save();
            Volatile.Write(ref active, current.Snapshot()); controller.Text = $"Controller: {chosen.Name} · Connected";
            save.Enabled = false; notice.Text = "Settings saved"; return true;
        }
        catch (Exception e) { MessageBox.Show(e.Message, "Controller selection failed"); }
        return false;
    }
    void RefreshController()
    {
        try {
            var devices = HidHide.GamingDevices();
            var chosen = devices.FirstOrDefault(d => d.Path.Equals(current.DevicePath, StringComparison.OrdinalIgnoreCase));
            if (chosen == null && devices.Count == 1) chosen = devices[0];
            controller.Text = chosen != null ? $"Controller: {chosen.Name} - Connected" : "Controller: " + (devices.Count == 0 ? "not connected" : "choose a controller");
        } catch { controller.Text = "Controller: connection unavailable"; }
    }
    void SetBusy(bool value)
    {
        busy = value; remap.Enabled = select.Enabled = troubleshooting.Enabled = !value;
        start.Enabled = true; start.Text = value ? "Stop remapping" : "Start remapping";
    }
    void Stop()
    {
        start.Enabled = false; state.Text = "Stopping remapping..."; cancellation?.Cancel();
    }
    async Task RestoreControllerVisibility()
    {
        if (busy) return;
        SetBusy(true); start.Enabled = false;
        notice.Text = "Restoring controller visibility...";
        try {
            output.Dispose();
            notice.Text = await restoreVisibility(current.DevicePath) ?? "Controller available to other apps";
        }
        catch (Exception e) { notice.Text = "Could not restore controller visibility: " + e.Message; }
        finally {
            SetBusy(false);
            if (closeRequested) { allowClose = true; Close(); }
        }
    }
    static async Task<string?> RestoreVisibility(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try { await Task.Run(() => HidHide.Unhide(path)); return null; }
        catch (Exception e) { return "Could not restore controller visibility: " + e.Message; }
    }
    async void Start(bool preparePlayerOne = false)
    {
        if (busy) return;
        try {
            var candidates = HidHide.GamingDevices();
            if (preparePlayerOne) {
                if (candidates.Count != 0) throw new InvalidOperationException("Unplug your controller before continuing with Player 1 setup.");
            } else if (!candidates.Any(d => d.Path.Equals(current.DevicePath, StringComparison.OrdinalIgnoreCase))) {
                if (candidates.Count == 1) current.DevicePath = candidates[0].Path;
                else if (!SelectDevice()) return;
            }
            grid.Focus(); current.Validate(); current.Save(); Volatile.Write(ref active, current.Snapshot());
            save.Enabled = false; grid.Refresh();
        }
        catch (Exception e) { MessageBox.Show(this, e.Message, "Controller setup"); return; }
        string path = current.DevicePath;
        cancellation?.Dispose(); cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        SetBusy(true); state.Text = "Starting remapping..."; notice.Text = "";
        try {
            task = Task.Run(() => {
                if (preparePlayerOne) { output.Dispose(); HidHide.PrepareDisconnected(path); } else HidHide.Configure(path);
                token.ThrowIfCancellationRequested();
                new Runner(() => Volatile.Read(ref active), text => {
                    if (!IsDisposed && IsHandleCreated) BeginInvoke(() => { if (!token.IsCancellationRequested) state.Text = text; });
                }, output, preparePlayerOne).Run(token);
            });
            await task;
            state.Text = "Remapping off";
        }
        catch (OperationCanceledException) { state.Text = "Remapping off"; }
        catch (Exception e) { output.Dispose(); state.Text = "Remapping stopped: " + e.Message; }
        finally {
            notice.Text = "Visibility unchanged. Use Troubleshooting to restore it.";
            SetBusy(false); RefreshController();
            if (closeRequested) {
                allowClose = true; Close();
            }
        }
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
        if (allowClose || !busy) { base.OnFormClosing(e); return; }
        e.Cancel = true;
        base.OnFormClosing(e);
        if (closeRequested) return;
        closeRequested = true;
        grid.Enabled = save.Enabled = false;
        Stop(); // The run (or an explicit restoration) finishes before closing.
    }
}
