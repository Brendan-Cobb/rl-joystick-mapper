using System.Reflection;
using System.Runtime.InteropServices;
using RlRemapper;

internal static class Checks
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        string root = args[0];
        NativeLibrary.SetDllImportResolver(typeof(Settings).Assembly, (name, _, _) =>
            name == "SDL2.dll" ? NativeLibrary.Load(Path.Combine(root, "publish", name)) : IntPtr.Zero);
        var editorType = typeof(Settings).Assembly.GetType("RlRemapper.ControlMappingForm", true)!;
        var original = new Settings { SnapAngleDeg = 12, ButtonMappings = "0=a\n1=none\n12=start" };
        using var form = (Form)Activator.CreateInstance(editorType, original)!;
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(-32000, -32000);
        form.ShowInTaskbar = false;
        form.Show();
        Application.DoEvents();
        var controls = Descendants(form).ToArray();
        var grid = controls.OfType<DataGridView>().Single();
        if (grid.Rows.Count < 13 || (string)grid.Rows[12].Cells[1].Value != "start") throw new Exception("Existing high-index mappings were lost.");
        if ((string)grid.Rows[1].Cells[1].Value != "none") throw new Exception("Disabled mappings were lost.");
        var tabs = controls.OfType<TabControl>().Single();
        form.PerformLayout();
        Directory.CreateDirectory(Path.Combine(root, "artifacts", "mapping-editor"));
        editorType.GetMethod("BeginGuidedSetup", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null);
        if (!controls.OfType<Label>().Any(label => label.Text == "Press A / Cross")) throw new Exception("Guided setup did not prompt for A/Cross.");
        var apply = editorType.GetMethod("ApplyGuidedButton", BindingFlags.Instance | BindingFlags.NonPublic)!;
        apply.Invoke(form, [3]);
        if (!controls.OfType<Label>().Any(label => label.Text == "Press A / Cross")) throw new Exception("Held button advanced the prompt before release.");
        editorType.GetField("waitForRelease", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(form, false);
        apply.Invoke(form, [3]);
        if ((string)grid.Rows[3].Cells[1].Value != "a" || (string)grid.Rows[0].Cells[1].Value != "none") throw new Exception("Guided assignment retained duplicate A binding.");
        if (!controls.OfType<Label>().Any(label => label.Text == "Press B / Circle")) throw new Exception("Guided setup did not advance to B/Circle.");
        foreach (int tab in new[] { 0, 1, 2 }) {
            tabs.SelectedIndex = tab;
            form.PerformLayout();
            using var bitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
            bitmap.Save(Path.Combine(root, "artifacts", "mapping-editor", $"tab-{tab}.png"));
        }
        grid.Rows[0].Cells[1].Value = "b";
        var save = controls.OfType<Button>().Single(button => button.Text == "Save mappings");
        typeof(Button).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(save, [EventArgs.Empty]);
        var result = (Settings)editorType.GetProperty("Result")!.GetValue(form)!;
        if (result.ParseButtons().Single(pair => pair.Index == 0).Mask != 0x2000) throw new Exception("Edited output did not serialize to the engine mapping.");
        if (original.ButtonMappings != "0=a\n1=none\n12=start" || result.SnapAngleDeg != 12) throw new Exception("Editing changed the source settings or unrelated snap settings.");
        if (form.DialogResult != DialogResult.OK) throw new Exception("Save did not accept the editor.");
        using var cancelled = (Form)Activator.CreateInstance(editorType, original)!;
        cancelled.StartPosition = FormStartPosition.Manual;
        cancelled.Location = new Point(-32000, -32000); cancelled.ShowInTaskbar = false;
        cancelled.Show(); Application.DoEvents();
        var reset = Descendants(cancelled).OfType<Button>().Single(button => button.Text == "Reset mappings");
        typeof(Button).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(reset, [EventArgs.Empty]);
        var resetGrid = Descendants(cancelled).OfType<DataGridView>().Single();
        if ((string)resetGrid.Rows[7].Cells[1].Value != "start") throw new Exception("Reset did not restore default button outputs.");
        cancelled.DialogResult = DialogResult.Cancel; cancelled.Close();
        if (original.ButtonMappings != "0=a\n1=none\n12=start") throw new Exception("Cancelling reset changed saved mappings.");
        Console.WriteLine("Mapping editor guided prompts/release gate/assignment/save/cancel/reset checks passed; all tabs rendered.");
    }
    static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls) {
            yield return child;
            foreach (var nested in Descendants(child)) yield return nested;
        }
    }
}
