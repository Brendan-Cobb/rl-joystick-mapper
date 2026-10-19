using System.ComponentModel;
using System.Text.Json;

namespace RlRemapper;

public enum StickMappingMode
{
    Angular = 0,
    Axial = 1,
    Radial = 2
}

public sealed class Settings
{
    [Category("Stick mapping")][DisplayName("Profile")]
    [Description("Left-stick transformation. Radial preserves input direction with a shared magnitude response. Angular keeps the original angular snapping and square output. Axial separates the center deadzone from axial strips.")]
    public StickMappingMode MappingMode { get; set; } = StickMappingMode.Angular;
    [Category("Stick mapping")][DisplayName("Inner deadzone")]
    [Description("Circular center deadzone as a fraction of full stick travel (0–1). Also supplies the unchanged right-stick cutoff. Radial rescales travel from this boundary to Radial max zone. Axial requires room for the center blend.")]
    public double InnerDeadzone { get; set; } = 0.05;
    [Category("Angular")][DisplayName("Outer ring threshold")]
    [Description("Angular only: input radius where angular snapping begins, from 0 to 1. Square conversion still applies below this threshold.")]
    public double OuterRingThreshold { get; set; } = 0;
    [Category("Angular")][DisplayName("Snap angle (degrees)")]
    [Description("Angular only: cardinal angular tolerance, from 0 to less than 45 degrees.")]
    public double SnapAngleDeg { get; set; } = 8;
    [Category("Angular")][DisplayName("Max zone")]
    [Description("Angular only: input radius that reaches full output (above Inner deadzone, up to 1). Lower values reach full output sooner. Deadzone and snapping thresholds remain in input space.")]
    public double AngularMaxZone { get; set; } = 1;
    [Category("Angular")][DisplayName("Square output")]
    [Description("Angular only: expand output into a square. Disable for circular output while keeping angular snapping. Independent of Radial and Axial Square Stick.")]
    public bool AngularSquareOutput { get; set; } = true;
    [Category("Radial")][DisplayName("Radial anti-deadzone")]
    [Description("Radial only: minimum active output radius just outside the inner deadzone (0–Radial max output). A nonzero value introduces an onset step. Applied before optional Square Stick.")]
    public double RadialAntiDeadzone { get; set; }
    [Category("Radial")][DisplayName("Radial max zone")]
    [Description("Radial only: input radius that reaches Radial max output, above Inner deadzone and up to 1. Larger input radii keep the same output magnitude and input direction.")]
    public double RadialMaxZone { get; set; } = 1;
    [Category("Radial")][DisplayName("Radial max output")]
    [Description("Radial only: maximum output radius before optional Square Stick (Radial anti-deadzone–1). Response rises linearly from the anti-deadzone to this value. Squaring can increase the final radius.")]
    public double RadialMaxOutput { get; set; } = 1;
    [Category("Axial")][DisplayName("X snap width (north/south)")]
    [Description("Axial only: suppresses X near north/south outside the center blend. Half-width of the vertical strip as a fraction of full axis travel (0–1), below X max zone.")]
    public double XSnapWidth { get; set; } = 0.10;
    [Category("Axial")][DisplayName("Y snap width (east/west)")]
    [Description("Axial only: suppresses Y near east/west outside the center blend. Half-width of the horizontal strip as a fraction of full axis travel (0–1), below Y max zone.")]
    public double YSnapWidth { get; set; } = 0.10;
    [Category("Axial")][DisplayName("Center blend width")]
    [Description("Axial only: small transition outside the overlap, as a fraction of full axis travel (0.000001–1). Larger values restore more center response. Must leave space before the max zones.")]
    public double CenterBlendWidth { get; set; } = 0.05;
    [Category("Axial")][DisplayName("X anti-deadzone")]
    [Description("Axial only: X output floor (0–1) applied to active output. Within the center blend its onset is softened. Applied after the max-output cap, as in DS4Windows.")]
    public double XAntiDeadzone { get; set; }
    [Category("Axial")][DisplayName("X max zone")]
    [Description("Axial only: X input travel that reaches full scaled output (above X snap width, up to 1). Must exceed the center blend end.")]
    public double XMaxZone { get; set; } = 1;
    [Category("Axial")][DisplayName("X max output")]
    [Description("Axial only: X output cap before anti-deadzone and Square Stick (0–1). Those later stages can raise the final component above this value.")]
    public double XMaxOutput { get; set; } = 1;
    [Category("Axial")][DisplayName("Y anti-deadzone")]
    [Description("Axial only: Y output floor (0–1) applied to active output. Within the center blend its onset is softened. Applied after the max-output cap, as in DS4Windows.")]
    public double YAntiDeadzone { get; set; }
    [Category("Axial")][DisplayName("Y max zone")]
    [Description("Axial only: Y input travel that reaches full scaled output (above Y snap width, up to 1). Must exceed the center blend end.")]
    public double YMaxZone { get; set; } = 1;
    [Category("Axial")][DisplayName("Y max output")]
    [Description("Axial only: Y output cap before anti-deadzone and Square Stick (0–1). Those later stages can raise the final component above this value.")]
    public double YMaxOutput { get; set; } = 1;
    [Category("Radial / axial square stick")][DisplayName("Square Stick")]
    [Description("Radial and Axial: enables circle-to-square expansion after the mode's output response. Angular has its own independent Square output setting.")]
    public bool SquareStick { get; set; } = true;
    [Category("Radial / axial square stick")][DisplayName("Square roundness")]
    [Description("Radial and Axial: exponent from 0 to 1000 controlling Square Stick strength by radius. 0 applies full squaring throughout travel; larger values preserve a rounder center.")]
    public double SquareRoundness { get; set; } = 0;
    [Category("Controller")] public int ControllerIndex { get; set; }
    [Category("Controller")] public int StickXAxis { get; set; } = 0;
    [Category("Controller")] public int StickYAxis { get; set; } = 1;
    [Category("Controller")] public int RightStickXAxis { get; set; } = 3;
    [Category("Controller")] public int RightStickYAxis { get; set; } = 4;
    [Category("Controller")] public int LeftTriggerAxis { get; set; } = 2;
    [Category("Controller")] public int RightTriggerAxis { get; set; } = 5;
    [Category("Controller")][ReadOnly(true)][Description("Set with Change next to the controller name. This controller stays hidden until you explicitly choose Restore controller visibility under Troubleshooting.")] public string DevicePath { get; set; } = "";
    [Category("Performance")] public int PollRateHz { get; set; } = 1000;
    [Browsable(false)]
    public string ButtonMappings { get; set; } = "0=a\n1=b\n2=x\n3=y\n4=lb\n5=rb\n6=back\n7=start\n8=l3\n9=r3";

    public static string PathOnDisk => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RlRemapper", "settings.json");
    // Keep property initializers compatible with older saved profiles that omit fields.
    // Apply the new preset only when starting without a saved profile.
    public static Settings CreateDefault() => new()
    {
        MappingMode = StickMappingMode.Axial,
        InnerDeadzone = 0.02,
        XSnapWidth = 0.2,
        YSnapWidth = 0.2,
        CenterBlendWidth = 0.5,
        XAntiDeadzone = 0,
        YAntiDeadzone = 0,
        XMaxZone = 0.707,
        YMaxZone = 0.707,
        XMaxOutput = 1,
        YMaxOutput = 1,
        SquareStick = false,
        SquareRoundness = 0
    };
    public static Settings Load() => File.Exists(PathOnDisk) ? JsonSerializer.Deserialize<Settings>(File.ReadAllText(PathOnDisk)) ?? CreateDefault() : CreateDefault();
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
        if (!Enum.IsDefined(typeof(StickMappingMode), MappingMode))
            throw new ArgumentException("Choose a valid stick mapping mode.");
        if (!UnitRange(InnerDeadzone) || !UnitRange(OuterRingThreshold) || !double.IsFinite(SnapAngleDeg) || SnapAngleDeg is < 0 or >= 45 || PollRateHz is < 50 or > 1000 || ControllerIndex < 0)
            throw new ArgumentException("Deadzone/threshold must be 0–1, angle 0–<45 degrees, poll rate 50–1000 Hz, controller index >= 0.");
        if (MappingMode == StickMappingMode.Angular && (!UnitRange(AngularMaxZone) || AngularMaxZone <= InnerDeadzone))
            throw new ArgumentException("Angular max zone must be above Inner deadzone and at most 1.");
        if (MappingMode == StickMappingMode.Radial)
        {
            if (!UnitRange(RadialMaxZone) || InnerDeadzone >= RadialMaxZone)
                throw new ArgumentException("Radial max zone must be a finite value above Inner deadzone and at most 1.");
            if (!UnitRange(RadialAntiDeadzone) || !UnitRange(RadialMaxOutput) || RadialAntiDeadzone > RadialMaxOutput)
                throw new ArgumentException("Radial anti-deadzone and max output must be finite values from 0 to 1, with anti-deadzone no greater than max output.");
        }
        if (MappingMode == StickMappingMode.Axial)
        {
            if (InnerDeadzone >= 1)
                throw new ArgumentException("Axial requires an inner deadzone below 1.");
            if (!UnitRange(XSnapWidth) || !UnitRange(YSnapWidth) || !UnitRange(XMaxZone) || !UnitRange(YMaxZone) || XSnapWidth >= XMaxZone || YSnapWidth >= YMaxZone)
                throw new ArgumentException("Axial snap widths and max zones must be finite values from 0 to 1, with each snap width below its matching max zone.");
            if (!UnitRange(XAntiDeadzone) || !UnitRange(YAntiDeadzone) || !UnitRange(XMaxOutput) || !UnitRange(YMaxOutput))
                throw new ArgumentException("Anti-deadzones and max outputs must be finite values from 0 to 1.");
            if (!UnitRange(CenterBlendWidth) || CenterBlendWidth < 0.000001)
                throw new ArgumentException("Center blend width must be a finite value from 0.000001 to 1.");
            if (XSnapWidth + CenterBlendWidth >= XMaxZone || YSnapWidth + CenterBlendWidth >= YMaxZone || InnerDeadzone + CenterBlendWidth >= Math.Min(XMaxZone, YMaxZone))
                throw new ArgumentException("There is no room for the center blend: each snap width plus the blend width must be below its matching max zone, and inner deadzone plus blend width must be below both max zones. Reduce the deadzone, snap widths or blend width, or increase the max zones.");
        }
        if (MappingMode != StickMappingMode.Angular && (!double.IsFinite(SquareRoundness) || SquareRoundness is < 0 or > 1000))
            throw new ArgumentException("Square roundness must be a finite value from 0 to 1000.");
        foreach (int axis in new[] { StickXAxis, StickYAxis, RightStickXAxis, RightStickYAxis, LeftTriggerAxis, RightTriggerAxis })
            if (axis < 0 || axis > 255) throw new ArgumentException("Axis indices must be 0–255.");
        _ = ParseButtons();
    }
    static bool UnitRange(double value) => double.IsFinite(value) && value >= 0 && value <= 1;
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
