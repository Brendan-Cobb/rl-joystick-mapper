using System.Text.Json;
using System.Text.Json.Serialization;
using RlRemapper;

internal static class RadialChecks
{
    static int checks;
    static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception(message);
    }
    static void Near(double actual, double expected, string message, double tolerance = 1e-10) =>
        Check(double.IsFinite(actual) && Math.Abs(actual - expected) <= tolerance, $"{message}: expected {expected}, got {actual}");
    static double Length((double X, double Y) p) => Math.Sqrt(p.X * p.X + p.Y * p.Y);
    static Settings Basic() => new() { MappingMode = StickMappingMode.Radial, SquareStick = false, InnerDeadzone = .1 };
    static void Invalid(Action<Settings> change)
    {
        var cfg = Basic(); change(cfg);
        bool threw = false;
        try { cfg.Validate(); } catch (ArgumentException) { threw = true; }
        Check(threw, "Invalid radial settings must be rejected");
    }

    public static void Run()
    {
        var cfg = Basic(); cfg.Validate();
        Near(Length(Transform.ApplyStick(0, 0, cfg)), 0, "Radial origin is neutral");
        Near(Length(Transform.ApplyStick(.03, .04, cfg)), 0, "Radial inner circle suppresses input");
        Near(Length(Transform.ApplyStick(.1, 0, cfg)), 0, "Radial inner boundary is neutral");
        Near(Length(Transform.ApplyStick(.1 + 1e-9, 0, cfg)), 1e-9 / .9, "Zero anti-deadzone starts continuously");
        var p = Transform.ApplyStick(.3, .4, cfg);
        Near(p.X, 4.0 / 15, "Radial golden X rescales available travel");
        Near(p.Y, 16.0 / 45, "Radial golden Y rescales available travel");
        Near(p.Y / p.X, 4.0 / 3, "Radial golden angle stays unchanged");

        cfg.RadialMaxZone = .9; cfg.RadialAntiDeadzone = .2; cfg.RadialMaxOutput = .8; cfg.Validate();
        p = Transform.ApplyStick(.3, .4, cfg);
        Near(p.X, .3, "Shared anti/max response golden X");
        Near(p.Y, .4, "Shared anti/max response golden Y");
        Near(Transform.ApplyStick(.1 + 1e-9, 0, cfg).X, .2 + .6e-9 / .8, "Anti-deadzone is the onset floor");
        Near(Transform.ApplyStick(.1, 0, cfg).X, 0, "Anti-deadzone does not activate at the inner boundary");
        Near(Transform.ApplyStick(.9, 0, cfg).X, .8, "Max zone reaches max output");
        Near(Transform.ApplyStick(1, 0, cfg).X, .8, "Beyond max zone keeps max output");
        p = Transform.ApplyStick(1, 1, cfg);
        Near(p.X, .8 / Math.Sqrt(2), "Square-gate input saturates radius");
        Near(p.Y, p.X, "Square-gate input keeps its angle");

        cfg.SquareStick = true;
        p = Transform.ApplyStick(.3, .4, cfg);
        Near(p.X, .375, "Full squaring follows radial response X");
        Near(p.Y, .5, "Full squaring follows radial response Y");
        p = Transform.ApplyStick(1, 1, cfg);
        Near(p.X, .8, "Full square corner respects radial max output per component");
        Near(p.Y, .8, "Full square corner is symmetric");
        cfg.RadialMaxOutput = 0; cfg.RadialAntiDeadzone = 0; cfg.Validate();
        Near(Length(Transform.ApplyStick(.5, .6, cfg)), 0, "Zero max output disables output including squaring");
        cfg.RadialMaxOutput = .25; cfg.RadialAntiDeadzone = .25; cfg.SquareStick = false; cfg.Validate();
        Near(Length(Transform.ApplyStick(.11, .02, cfg)), .25, "Equal anti/max values give a constant active radius");

        foreach (double bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            Near(Length(Transform.ApplyStick(bad, .5, cfg)), 0, "Nonfinite X becomes neutral");
            Near(Length(Transform.ApplyStick(.5, bad, cfg)), 0, "Nonfinite Y becomes neutral");
        }
        var clamped = Transform.ApplyStick(1, -.5, cfg);
        p = Transform.ApplyStick(4, -.5, cfg);
        Near(p.X, clamped.X, "Finite oversized X is clamped before radial mapping");
        Near(p.Y, clamped.Y, "Finite oversized input uses the clamped input angle");

        // Angle, symmetry, saturation and monotonic travel are independent
        // requirements, including when roundness changes the output shape.
        foreach (var response in new[] { (0.0, 1.0, 0.0, 1.0), (.1, .9, .2, .8), (.4, .6, .0, .35) })
        foreach (double roundness in new[] { -1.0, 0.0, 5.0, 1000.0 })
        {
            cfg = Basic();
            (cfg.InnerDeadzone, cfg.RadialMaxZone, cfg.RadialAntiDeadzone, cfg.RadialMaxOutput) = response;
            cfg.SquareStick = roundness >= 0;
            cfg.SquareRoundness = Math.Max(0, roundness); cfg.Validate();
            foreach (double angle in new[] { 0.0, 3.0, 15.0, 45.0, 89.9, 90.0, 120.0, 179.0, 225.0, 270.0, 315.0 })
            {
                double radians = angle * Math.PI / 180, ux = Math.Cos(radians), uy = Math.Sin(radians);
                double previous = 0;
                for (int step = 0; step <= 40; step++)
                {
                    double radius = step / 40.0, x = radius * ux, y = radius * uy;
                    p = Transform.ApplyStick(x, y, cfg);
                    double length = Length(p);
                    Check(double.IsFinite(length), "Radial grid output stays finite");
                    Check(Math.Max(Math.Abs(p.X), Math.Abs(p.Y)) <= cfg.RadialMaxOutput + 1e-12, "Radial output component bound survives squaring");
                    Near(x * p.Y - y * p.X, 0, "Radial direction is preserved including Square Stick");
                    Check(length + 1e-10 >= previous, "Radial response does not reverse along a ray");
                    previous = length;
                    if (radius < cfg.InnerDeadzone - 1e-12) Near(length, 0, "Radial grid inner circle stays neutral");
                    if (radius > cfg.InnerDeadzone + 1e-12) Check(length > 0, "Radial has no additional dead strips");
                    if (radius >= cfg.RadialMaxZone && !cfg.SquareStick)
                        Near(length, cfg.RadialMaxOutput, "Unsquared radial output saturates magnitude");
                    var mirror = Transform.ApplyStick(-x, -y, cfg);
                    Near(mirror.X, -p.X, "Radial X sign symmetry");
                    Near(mirror.Y, -p.Y, "Radial Y sign symmetry");
                    var swapped = Transform.ApplyStick(y, x, cfg);
                    Near(swapped.X, p.Y, "Radial axis interchange X");
                    Near(swapped.Y, p.X, "Radial axis interchange Y");
                }
            }
        }

        cfg = Basic(); cfg.SquareStick = true; cfg.SquareRoundness = 3;
        var before = Transform.ApplyStick(.4, .07, cfg);
        cfg.OuterRingThreshold = .8; cfg.SnapAngleDeg = 30;
        cfg.XSnapWidth = .3; cfg.YSnapWidth = .4; cfg.CenterBlendWidth = .12;
        cfg.XMaxZone = .7; cfg.YMaxZone = .8; cfg.XAntiDeadzone = .1; cfg.YAntiDeadzone = .3;
        cfg.XMaxOutput = .4; cfg.YMaxOutput = .5; cfg.Validate();
        p = Transform.ApplyStick(.4, .07, cfg);
        Near(p.X, before.X, "Other modes' controls do not affect radial X", 0);
        Near(p.Y, before.Y, "Other modes' controls do not affect radial Y", 0);

        Check((int)StickMappingMode.Angular == 0 && (int)StickMappingMode.Axial == 1 && (int)StickMappingMode.Radial == 2, "Serialized mode numbers remain compatible");
        foreach (var mode in new[] { StickMappingMode.Angular, StickMappingMode.Axial, StickMappingMode.Radial })
        {
            var loaded = JsonSerializer.Deserialize<Settings>($"{{\"MappingMode\":{(int)mode}}}")!;
            loaded.Validate(); Check(loaded.MappingMode == mode, "Numeric JSON selects the intended mapping mode");
        }
        Check(JsonSerializer.Deserialize<Settings>("{}")!.MappingMode == StickMappingMode.Angular, "Profiles without a mode retain legacy mapping");
        cfg.RadialAntiDeadzone = .15; cfg.RadialMaxZone = .75; cfg.RadialMaxOutput = .65; cfg.Validate();
        var copy = cfg.Snapshot(); copy.Validate();
        Check(JsonSerializer.Serialize(cfg) == JsonSerializer.Serialize(copy), "Radial settings survive JSON/snapshot round-trip");

        foreach (string name in new[] { nameof(Settings.InnerDeadzone), nameof(Settings.RadialAntiDeadzone), nameof(Settings.RadialMaxZone), nameof(Settings.RadialMaxOutput), nameof(Settings.SquareRoundness) })
            foreach (double bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
                Invalid(s => typeof(Settings).GetProperty(name)!.SetValue(s, bad));
        Invalid(s => s.InnerDeadzone = 1);
        Invalid(s => s.RadialMaxZone = s.InnerDeadzone);
        Invalid(s => s.RadialMaxZone = .05);
        Invalid(s => s.RadialMaxZone = 1.01);
        Invalid(s => s.RadialAntiDeadzone = -.01);
        Invalid(s => { s.RadialAntiDeadzone = .6; s.RadialMaxOutput = .5; });
        Invalid(s => s.RadialMaxOutput = -.01);
        Invalid(s => s.RadialMaxOutput = 1.01);
        Invalid(s => s.SquareRoundness = -.01);
        Invalid(s => s.SquareRoundness = 1001);
        Console.WriteLine($"Radial checks passed ({checks:N0} assertions).");
    }

    // Optional production-code oracle for the browser visualizer. Each profile
    // carries settings once, keeping the fixture compact and easy to compare.
    public static void ExportVisualizerCases(string path)
    {
        var profiles = new List<(string Name, Settings Settings)>();
        foreach (var response in new[] { (0.0, 1.0, 0.0, 1.0), (.1, .75, .2, .8), (.4, .6, 0.0, .35) })
        foreach (double roundness in new[] { -1.0, 0.0, 5.0, 1000.0 })
        {
            var cfg = Basic();
            (cfg.InnerDeadzone, cfg.RadialMaxZone, cfg.RadialAntiDeadzone, cfg.RadialMaxOutput) = response;
            cfg.SquareStick = roundness >= 0; cfg.SquareRoundness = Math.Max(0, roundness);
            profiles.Add(($"radial-{profiles.Count}", cfg));
        }
        profiles.Add(("radial-zero", new() { MappingMode = StickMappingMode.Radial, RadialMaxOutput = 0 }));
        profiles.Add(("radial-constant", new() { MappingMode = StickMappingMode.Radial, RadialAntiDeadzone = .3, RadialMaxOutput = .3 }));
        profiles.Add(("axial-default", new() { MappingMode = StickMappingMode.Axial }));
        profiles.Add(("axial-center", new() { MappingMode = StickMappingMode.Axial, InnerDeadzone = .1, XSnapWidth = 20.0 / 127, YSnapWidth = 20.0 / 127, XMaxZone = .5, YMaxZone = .5 }));
        profiles.Add(("axial-asymmetric", new() { MappingMode = StickMappingMode.Axial, XSnapWidth = 0, YSnapWidth = .2, XAntiDeadzone = .1, YAntiDeadzone = .25, XMaxZone = .7, YMaxZone = .8, XMaxOutput = .6, YMaxOutput = .9, SquareRoundness = 5 }));
        profiles.Add(("legacy-default", new()));
        profiles.Add(("legacy-outer-ring", new() { InnerDeadzone = .12, OuterRingThreshold = .6, SnapAngleDeg = 13 }));
        profiles.Add(("legacy-no-snap", new() { InnerDeadzone = .05, SnapAngleDeg = 0 }));

        foreach (double zone in new[] { .5, .8, 1.0 })
        foreach (bool square in new[] { false, true })
            profiles.Add(($"angular-{zone}-{square}", new() { AngularMaxZone = zone, AngularSquareOutput = square, OuterRingThreshold = .3 }));

        var inputs = new List<double[]> { new[] { 0.0, 0.0 }, new[] { 1.0, 1.0 }, new[] { -.9, .7 }, new[] { .03, .04 }, new[] { .6, .3 }, new[] { .05, .6 }, new[] { 1.0, -.3 } };
        foreach (double radius in new[] { .025, .05, .050001, .1, .100001, .3, .65, 1.0 })
        foreach (double angle in new[] { 0.0, 7.9, 8.0, 8.1, 10.0, 30.0, 45.0, 60.0, 89.0, 90.0, 120.0, 180.0, 225.0, 270.0, 315.0 })
            inputs.Add(new[] { radius * Math.Cos(angle * Math.PI / 180), radius * Math.Sin(angle * Math.PI / 180) });

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        options.Converters.Add(new JsonStringEnumConverter());
        var data = profiles.Select(profile =>
        {
            profile.Settings.Validate();
            return new
            {
                name = profile.Name,
                settings = profile.Settings,
                cases = inputs.Select(input =>
                {
                    var output = Transform.ApplyStick(input[0], input[1], profile.Settings);
                    return new { input, output = new[] { output.X, output.Y }, stick = new[] { Transform.Stick(output.X), Transform.Stick(output.Y) } };
                }).ToArray()
            };
        }).ToArray();
        File.WriteAllText(path, JsonSerializer.Serialize(new { profiles = data }, options));
        Console.WriteLine($"Exported {profiles.Count * inputs.Count:N0} visualizer reference cases across {profiles.Count} profiles to {path}");
    }
}
