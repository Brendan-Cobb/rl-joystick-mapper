using System.ComponentModel;
using System.Text.Json;

namespace RlRemapper;

public sealed class Settings
{
    [Category("Snap")] public double InnerDeadzone { get; set; } = 0.05;
    [Category("Snap")] public double OuterRingThreshold { get; set; } = 0;
    [Category("Snap")] public double SnapAngleDeg { get; set; } = 8;
    [Category("Controller")] public int ControllerIndex { get; set; }
    [Category("Controller")] public int StickXAxis { get; set; } = 0;
    [Category("Controller")] public int StickYAxis { get; set; } = 1;
    [Category("Controller")] public int RightStickXAxis { get; set; } = 3;
    [Category("Controller")] public int RightStickYAxis { get; set; } = 4;
    [Category("Controller")] public int LeftTriggerAxis { get; set; } = 2;
    [Category("Controller")] public int RightTriggerAxis { get; set; } = 5;
    [Category("Controller")][ReadOnly(true)][Description("Set with Select physical device. HidHide hides only this device.")] public string DevicePath { get; set; } = "";
    [Category("Performance")] public int PollRateHz { get; set; } = 1000;
    [Category("Buttons")][Description("One physical index=virtual name per line; names: a b x y lb rb back start l3 r3 guide dpad_up dpad_down dpad_left dpad_right none")]
    public string ButtonMappings { get; set; } = "0=a\n1=b\n2=x\n3=y\n4=lb\n5=rb\n6=back\n7=start\n8=l3\n9=r3";

    public static string PathOnDisk => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RlRemapper", "settings.json");
    public static Settings Load() => File.Exists(PathOnDisk) ? JsonSerializer.Deserialize<Settings>(File.ReadAllText(PathOnDisk)) ?? new() : new();
    public void Save()
    {
        Validate();
        Directory.CreateDirectory(Path.GetDirectoryName(PathOnDisk)!);
        var temp = PathOnDisk + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, PathOnDisk, true);
    }
    public void Validate()
    {
        if (InnerDeadzone is < 0 or > 1 || OuterRingThreshold is < 0 or > 1 || SnapAngleDeg is < 0 or >= 45 || PollRateHz is < 50 or > 1000 || ControllerIndex < 0)
            throw new ArgumentException("Deadzone/threshold must be 0–1, angle 0–<45 degrees, poll rate 50–1000 Hz, controller index >= 0.");
        foreach (int axis in new[] { StickXAxis, StickYAxis, RightStickXAxis, RightStickYAxis, LeftTriggerAxis, RightTriggerAxis })
            if (axis < 0 || axis > 255) throw new ArgumentException("Axis indices must be 0–255.");
        _ = ParseButtons();
    }
    public (int Index, ushort Mask)[] ParseButtons()
    {
        var map = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase) {
            ["a"]=0x1000,["b"]=0x2000,["x"]=0x4000,["y"]=0x8000,["lb"]=0x0100,["rb"]=0x0200,
            ["back"]=0x0020,["start"]=0x0010,["l3"]=0x0040,["r3"]=0x0080,["guide"]=0x0400,
            ["dpad_up"]=1,["dpad_down"]=2,["dpad_left"]=4,["dpad_right"]=8,["none"]=0
        };
        var result = new Dictionary<int, ushort>();
        foreach (var line in ButtonMappings.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || !int.TryParse(parts[0], out int index) || index < 0 || index > 255 || !map.TryGetValue(parts[1], out ushort mask) || !result.TryAdd(index, mask))
                throw new ArgumentException($"Invalid or duplicate button mapping: {line}");
        }
        return result.Select(pair => (pair.Key, pair.Value)).ToArray();
    }
    public Settings Snapshot() => JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(this))!;
}
