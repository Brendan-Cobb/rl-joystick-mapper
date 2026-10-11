namespace RlRemapper;

internal sealed class ControlMappingForm : Form
{
    sealed record Output(string Value, string Label);
    static readonly Output[] Outputs = [
        new("none", "Disabled"), new("a", "A / Cross"), new("b", "B / Circle"),
        new("x", "X / Square"), new("y", "Y / Triangle"), new("lb", "Left bumper"),
        new("rb", "Right bumper"), new("back", "Back / View"), new("start", "Start / Menu"),
        new("l3", "Left stick click"), new("r3", "Right stick click"), new("guide", "Guide"),
        new("dpad_up", "D-pad up"), new("dpad_down", "D-pad down"),
        new("dpad_left", "D-pad left"), new("dpad_right", "D-pad right") ];
    readonly Settings draft;
    readonly DataGridView buttons = new() { Dock = DockStyle.Fill, AllowUserToAddRows = false,
        AllowUserToDeleteRows = false, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
    readonly Label hint = new() { Dock = DockStyle.Bottom, Height = 55, Padding = new Padding(8), AutoEllipsis = true };
    readonly System.Windows.Forms.Timer timer = new() { Interval = 40 };
    readonly List<(ComboBox Combo, Action<int> Assign)> axes = [];
    readonly Label guidePrompt = new() { Dock = DockStyle.Top, Height = 85, Font = new Font("Segoe UI", 20), TextAlign = ContentAlignment.MiddleCenter, Text = "Set up your controller buttons" };
    readonly Label guideProgress = new() { Dock = DockStyle.Top, Height = 30, TextAlign = ContentAlignment.MiddleCenter };
    readonly ListBox guideSummary = new() { Dock = DockStyle.Fill, IntegralHeight = false, Font = new Font("Segoe UI", 11) };
    readonly HashSet<string> learned = [];
    readonly Button skip = new() { Text = "Skip this button", AutoSize = true, Enabled = false };
    int guideStep = -1;
    bool waitForRelease;
    byte previousHat;
    IntPtr joystick;
    bool initialized;
    bool[] previous = [];
    ComboBox? learning;
    double[] baseline = [];
    public Settings Result => draft;

    public ControlMappingForm(Settings settings)
    {
        draft = settings.Snapshot();
        Text = "Remap controls"; Width = 760; Height = 600; MinimumSize = new Size(640, 450);
        StartPosition = FormStartPosition.CenterParent;
        var tabs = new TabControl { Dock = DockStyle.Fill };
        var guideTab = new TabPage("Button setup");
        var buttonTab = new TabPage("Advanced button mappings");
        var axisTab = new TabPage("Sticks and triggers");
        tabs.TabPages.AddRange([guideTab, axisTab, buttonTab]);
        tabs.SelectedIndexChanged += (_, _) => {
            if (tabs.SelectedIndex != 0 && guideStep >= 0) {
                guideStep = -1; skip.Enabled = false; guidePrompt.Text = "Set up your controller buttons";
                guideProgress.Text = "Your identified buttons are kept. Start guided setup to repeat.";
            }
        };
        var guideInstructions = new Label { Dock = DockStyle.Top, Height = 62, Padding = new Padding(10),
            Text = "Start guided setup, then press the button shown below. Release it before the next prompt.\nSkip buttons your controller doesn't have. Changes are kept only when you Save mappings." };
        var guideActions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 45, Padding = new Padding(10, 4, 10, 4) };
        var begin = new Button { Text = "Start guided setup", AutoSize = true };
        begin.Click += (_, _) => {
            if (joystick == IntPtr.Zero || Sdl.SDL_JoystickGetAttached(joystick) == 0) { hint.Text = "Connect the controller and reopen this editor to run guided setup."; return; }
            BeginGuidedSetup();
        };
        skip.Click += (_, _) => NextGuidedStep();
        guideActions.Controls.AddRange([begin, skip]);
        guideTab.Controls.Add(guideSummary); guideTab.Controls.Add(guideActions);
        guideTab.Controls.Add(guideProgress); guideTab.Controls.Add(guidePrompt); guideTab.Controls.Add(guideInstructions);
        var instructions = new Label { Dock = DockStyle.Top, Height = 62, Padding = new Padding(8),
            Text = "Press a button on your controller to highlight its row, then choose its output.\nOutputs are controller buttons; assign Rocket League actions in the game's controls menu." };
        buttons.Columns.Add(new DataGridViewTextBoxColumn { Name = "Input", HeaderText = "Physical button", ReadOnly = true });
        buttons.Columns.Add(new DataGridViewComboBoxColumn { Name = "Output", HeaderText = "Send as", DataSource = Outputs,
            DisplayMember = "Label", ValueMember = "Value", DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox });
        buttons.EditMode = DataGridViewEditMode.EditOnEnter;
        buttonTab.Controls.Add(buttons); buttonTab.Controls.Add(instructions);
        var axisPanel = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3, Padding = new Padding(12) };
        axisPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        axisPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        axisPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        axisTab.Controls.Add(axisPanel);
        axisTab.Controls.Add(new Label { Dock = DockStyle.Bottom, Height = 65, Padding = new Padding(12),
            Text = "Click Learn, then move only that stick direction or pull that trigger.\nD-pad hats are forwarded automatically. Button-based D-pads can be assigned on the Buttons tab." });
        var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
        var save = new Button { Text = "Save mappings", AutoSize = true };
        save.Click += (_, _) => {
            try {
                buttons.EndEdit();
                draft.ButtonMappings = string.Join("\n", buttons.Rows.Cast<DataGridViewRow>().Select(row => $"{row.Index}={row.Cells[1].Value ?? "none"}"));
                foreach (var axis in axes) axis.Assign(axis.Combo.SelectedIndex);
                draft.Validate(); DialogResult = DialogResult.OK; Close();
            } catch (Exception error) { MessageBox.Show(this, error.Message, "Check mappings"); }
        };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        var defaults = new Button { Text = "Reset mappings", AutoSize = true };
        defaults.Click += (_, _) => {
            var original = new Settings();
            FillButtons(original);
            int[] indices = [original.StickXAxis, original.StickYAxis, original.RightStickXAxis, original.RightStickYAxis, original.LeftTriggerAxis, original.RightTriggerAxis];
            for (int i = 0; i < axes.Count; i++) axes[i].Combo.SelectedIndex = indices[i];
            guideStep = -1; skip.Enabled = false; learned.Clear(); guidePrompt.Text = "Set up your controller buttons"; guideProgress.Text = ""; RefreshGuideSummary();
            learning = null; hint.Text = "Default mappings restored in this editor. Save to keep them, or Cancel to discard.";
        };
        actions.Controls.AddRange([save, cancel, defaults]);
        CancelButton = cancel;
        Controls.Add(tabs); Controls.Add(hint); Controls.Add(actions);
        Shown += (_, _) => {
            try {
                Environment.SetEnvironmentVariable("SDL_JOYSTICK_RAWINPUT", "0");
                if (Sdl.SDL_Init(Sdl.Joystick) != 0) throw new InvalidOperationException("Cannot initialize controller input.");
                initialized = true;
                joystick = Sdl.OpenPhysical(draft.ControllerIndex);
                FillButtons(draft);
                int count = Math.Max(6, joystick == IntPtr.Zero ? 0 : Sdl.SDL_JoystickNumAxes(joystick));
                AddAxis("Left stick — horizontal", draft.StickXAxis, value => draft.StickXAxis = value, count, axisPanel);
                AddAxis("Left stick — vertical", draft.StickYAxis, value => draft.StickYAxis = value, count, axisPanel);
                AddAxis("Right stick — horizontal", draft.RightStickXAxis, value => draft.RightStickXAxis = value, count, axisPanel);
                AddAxis("Right stick — vertical", draft.RightStickYAxis, value => draft.RightStickYAxis = value, count, axisPanel);
                AddAxis("Left trigger", draft.LeftTriggerAxis, value => draft.LeftTriggerAxis = value, count, axisPanel);
                AddAxis("Right trigger", draft.RightTriggerAxis, value => draft.RightTriggerAxis = value, count, axisPanel);
                if (joystick != IntPtr.Zero) {
                    previous = new bool[Sdl.SDL_JoystickNumButtons(joystick)];
                    timer.Tick += (_, _) => ReadInput(); timer.Start();
                    hint.Text = "Controller connected. Press a button to identify it, or open Sticks and triggers to learn an axis.";
                } else hint.Text = "No physical controller connected. You can edit and save mappings; reconnect and reopen to identify controls live.";
            } catch (Exception error) {
                FillButtons(draft); save.Enabled = axes.Count == 6;
                hint.Text = error.Message;
            }
        };
    }
    void FillButtons(Settings settings)
    {
        var mapped = settings.ParseButtons().ToDictionary(pair => pair.Index, pair => pair.Mask);
        int count = Math.Max(10, mapped.Count == 0 ? 0 : mapped.Keys.Max() + 1);
        if (joystick != IntPtr.Zero) count = Math.Max(count, Sdl.SDL_JoystickNumButtons(joystick));
        buttons.Rows.Clear();
        for (int i = 0; i < count; i++) {
            ushort mask = mapped.GetValueOrDefault(i);
            string value = Outputs.First(output => new Settings { ButtonMappings = $"0={output.Value}" }.ParseButtons()[0].Mask == mask).Value;
            buttons.Rows.Add($"Button {i + 1}", value);
        }
        RefreshGuideSummary();
    }
    void RefreshGuideSummary()
    {
        guideSummary.Items.Clear();
        foreach (var output in Outputs.Skip(1)) {
            bool assigned = buttons.Rows.Cast<DataGridViewRow>().Any(row => (string?)row.Cells[1].Value == output.Value);
            guideSummary.Items.Add($"{output.Label} — {(learned.Contains(output.Value) ? "Identified" : assigned ? "Existing mapping" : output.Value.StartsWith("dpad_") ? "Automatic for D-pad hats" : "Not assigned")}");
        }
    }
    void BeginGuidedSetup()
    {
        guideStep = 0; learned.Clear(); learning = null; waitForRelease = true;
        skip.Enabled = true; ShowGuidedStep();
    }
    void ShowGuidedStep()
    {
        guidePrompt.Text = $"Press {Outputs[guideStep + 1].Label}";
        guideProgress.Text = $"Button {guideStep + 1} of {Outputs.Length - 1}";
        guideSummary.SelectedIndex = guideStep;
        hint.Text = "Release all buttons, then press the requested button. You can skip it if unavailable.";
    }
    void NextGuidedStep()
    {
        guideStep++; waitForRelease = true;
        if (guideStep >= Outputs.Length - 1) {
            guideStep = -1; skip.Enabled = false; guidePrompt.Text = "Button setup complete";
            guideProgress.Text = "Save mappings to keep your changes"; hint.Text = "Review your mappings, then Save. You can repeat setup or adjust advanced mappings.";
        } else ShowGuidedStep();
    }
    void ApplyGuidedButton(int index)
    {
        if (guideStep < 0 || waitForRelease) return;
        var output = Outputs[guideStep + 1];
        // A named output has one identified physical source; remove its old binding.
        foreach (DataGridViewRow row in buttons.Rows)
            if ((string?)row.Cells[1].Value == output.Value) row.Cells[1].Value = "none";
        buttons.Rows[index].Cells[1].Value = output.Value;
        learned.Add(output.Value); RefreshGuideSummary(); NextGuidedStep();
    }
    void AddAxis(string name, int selected, Action<int> assign, int count, TableLayoutPanel panel)
    {
        var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        for (int i = 0; i < Math.Max(count, selected + 1); i++) combo.Items.Add($"Axis {i + 1}");
        combo.SelectedIndex = selected;
        var learn = new Button { Text = "Learn", AutoSize = true };
        learn.Click += (_, _) => {
            if (joystick == IntPtr.Zero || Sdl.SDL_JoystickGetAttached(joystick) == 0) { hint.Text = "Reconnect the controller and reopen this editor to learn inputs."; return; }
            Sdl.SDL_JoystickUpdate();
            baseline = Enumerable.Range(0, Sdl.SDL_JoystickNumAxes(joystick)).Select(i => Sdl.Axis(joystick, i)).ToArray();
            learning = combo; hint.Text = $"Learning {name}: move only this control now.";
        };
        int row = panel.RowCount++;
        panel.Controls.Add(new Label { Text = name, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 10, 3, 10) }, 0, row);
        panel.Controls.Add(combo, 1, row); panel.Controls.Add(learn, 2, row);
        axes.Add((combo, assign));
    }
    void ReadInput()
    {
        if (joystick == IntPtr.Zero || Sdl.SDL_JoystickGetAttached(joystick) == 0) { hint.Text = "Controller disconnected. Save your edits or reopen this editor after reconnecting."; return; }
        Sdl.SDL_JoystickUpdate();
        bool[] pressedButtons = Enumerable.Range(0, previous.Length).Select(i => Sdl.SDL_JoystickGetButton(joystick, i) != 0).ToArray();
        byte hat = Sdl.SDL_JoystickNumHats(joystick) > 0 ? Sdl.SDL_JoystickGetHat(joystick, 0) : (byte)0;
        if (guideStep >= 0 && !pressedButtons.Any(value => value) && hat == 0) waitForRelease = false;
        bool accepted = false;
        for (int i = 0; i < previous.Length; i++) {
            bool pressed = pressedButtons[i];
            if (guideStep >= 0 && pressed && !previous[i] && !waitForRelease && !accepted) {
                ApplyGuidedButton(i); accepted = true;
            } else if (guideStep < 0 && pressed && !previous[i] && !buttons.IsCurrentCellInEditMode) {
                buttons.CurrentCell = buttons.Rows[i].Cells[0];
                hint.Text = $"Button {i + 1} pressed. Choose its output in the Send as column.";
            }
            previous[i] = pressed;
        }
        if (guideStep >= 0 && !waitForRelease && hat != previousHat && hat != 0) {
            string value = Outputs[guideStep + 1].Value;
            byte expected = value switch { "dpad_up" => 1, "dpad_right" => 2, "dpad_down" => 4, "dpad_left" => 8, _ => 0 };
            if (expected != 0 && hat == expected) { learned.Add(value); RefreshGuideSummary(); NextGuidedStep(); }
        }
        previousHat = hat;
        if (learning != null && baseline.Length > 0) {
            var moved = Enumerable.Range(0, baseline.Length).Select(i => (Index: i, Delta: Math.Abs(Sdl.Axis(joystick, i) - baseline[i]))).OrderByDescending(axis => axis.Delta).First();
            if (moved.Delta > 0.35) { learning.SelectedIndex = moved.Index; learning = null; hint.Text = $"Detected Axis {moved.Index + 1}. Save mappings when finished."; }
        }
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) {
            timer.Stop(); timer.Dispose();
            if (joystick != IntPtr.Zero) { Sdl.SDL_JoystickClose(joystick); joystick = IntPtr.Zero; }
            if (initialized) { Sdl.SDL_Quit(); initialized = false; }
        }
        base.Dispose(disposing);
    }
}
