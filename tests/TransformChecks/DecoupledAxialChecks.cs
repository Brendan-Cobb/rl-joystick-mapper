using System.Text.Json;
using RlRemapper;

internal static class DecoupledAxialChecks
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
    static Settings Basic() => new() { MappingMode = StickMappingMode.DecoupledAxial, SquareStick = false };
    static void Invalid(Action<Settings> change)
    {
        var settings = Basic(); change(settings);
        bool threw = false;
        try { settings.Validate(); } catch (ArgumentException) { threw = true; }
        Check(threw, "Invalid settings must be rejected");
    }

    public static void Run()
    {
        var cfg = Basic(); cfg.Validate();
        var p = Transform.ApplyStick(.06, .06, cfg);
        Check(p.X > 0 && p.Y > 0, "Diagonal input inside the old square must be recovered");
        Near(p.X, p.Y, "Symmetric diagonal stays symmetric");
        Near(Length(Transform.ApplyStick(.03, .04, cfg)), 0, "Circular inner boundary is zero");
        Near(Length(Transform.ApplyStick(0, 0, cfg)), 0, "Origin is zero");
        Near(Length(Transform.ApplyStick(double.NaN, .5, cfg)), 0, "Invalid input becomes neutral");

        // Golden exterior values from the normalized DS4 axial equations.
        p = Transform.ApplyStick(.6, .3, cfg);
        Near(p.X, 5.0 / 9, "Exterior X matches ordinary axial response");
        Near(p.Y, 2.0 / 9, "Exterior Y matches ordinary axial response");
        p = Transform.ApplyStick(.05, .6, cfg);
        Near(p.X, 0, "Vertical strip suppresses X"); Near(p.Y, 5.0 / 9, "Vertical strip keeps Y");
        p = Transform.ApplyStick(.6, .05, cfg);
        Near(p.X, 5.0 / 9, "Horizontal strip keeps X"); Near(p.Y, 0, "Horizontal strip suppresses Y");
        cfg.XSnapWidth = .1; cfg.YSnapWidth = .2; cfg.YMaxZone = .8;
        p = Transform.ApplyStick(.6, .3, cfg);
        Near(p.X, 5.0 / 9, "Independent X strip"); Near(p.Y, 1.0 / 6, "Independent Y strip/max zone");
        cfg.XAntiDeadzone = .2; cfg.YAntiDeadzone = .1; cfg.XMaxOutput = .4; cfg.YMaxOutput = .6;
        p = Transform.ApplyStick(.6, .3, cfg);
        Near(p.X, .2 + .8 * (.4 / .99), "Output cap precedes anti-deadzone");
        Near(p.Y, .25, "Y anti-deadzone lifts surviving response");
        cfg = Basic(); cfg.XMaxZone = .5; cfg.YMaxZone = .7;
        Near(Transform.ApplyStick(.5, 0, cfg).X, 1, "X max zone still saturates at its input value");
        Near(Transform.ApplyStick(0, .7, cfg).Y, 1, "Y max zone still saturates at its input value");

        p = Transform.Square(.5, .5, 0);
        Near(p.X, Math.Sqrt(.5), "Roundness zero fully squares");
        Near(p.Y, p.X, "Square symmetry");
        p = Transform.Square(.5, .5, 5);
        Near(p.X, .5366116523516815, "Roundness five retains a rounder center");
        cfg = Basic(); cfg.SquareStick = true; cfg.SquareRoundness = 5;
        var unsquared = Basic();
        var a = Transform.ApplyStick(.6, .3, unsquared);
        var expectedSquare = Transform.Square(a.X, a.Y, 5);
        p = Transform.ApplyStick(.6, .3, cfg);
        Near(p.X, expectedSquare.X, "Squaring runs after axial mapping");
        Near(p.Y, expectedSquare.Y, "Squaring uses transformed radius");

        // Exhaust radial/angle sweeps to catch holes, seams, zero-width strip
        // degeneracy, symmetry failures and non-monotonic recovered magnitude.
        foreach (var widths in new[] { (0.0, 0.0), (0.0, .2), (.2, 0.0), (.1, .1), (.05, .35), (.4, .6) })
        {
            cfg = Basic(); cfg.XSnapWidth = widths.Item1; cfg.YSnapWidth = widths.Item2; cfg.Validate();
            var swapped = cfg.Snapshot(); swapped.XSnapWidth = cfg.YSnapWidth; swapped.YSnapWidth = cfg.XSnapWidth;
            for (int angle = 0; angle <= 360; angle += 3)
            {
                double rad = angle * Math.PI / 180, ux = Math.Cos(rad), uy = Math.Sin(rad);
                double previous = 0;
                for (int step = 0; step <= 300; step++)
                {
                    double r = step / 300.0;
                    p = Transform.ApplyStick(r * ux, r * uy, cfg);
                    double magnitude = Length(p);
                    Check(double.IsFinite(magnitude), "Output must remain finite");
                    if (r < cfg.InnerDeadzone - 1e-12) Near(magnitude, 0, "Only inner circle suppresses all output");
                    if (r > cfg.InnerDeadzone + 1e-12) Check(magnitude > 0, "No second zero pocket outside inner circle");
                    Check(magnitude + 1e-10 >= previous, "Recovered pre-square travel must not reverse along a ray");
                    previous = magnitude;
                    var mirror = Transform.ApplyStick(-r * ux, -r * uy, cfg);
                    Near(mirror.X, -p.X, "X sign symmetry"); Near(mirror.Y, -p.Y, "Y sign symmetry");
                    var transposed = Transform.ApplyStick(r * uy, r * ux, swapped);
                    Near(transposed.X, p.Y, "Axis swap symmetry X"); Near(transposed.Y, p.X, "Axis swap symmetry Y");
                }

                double band = cfg.CenterBlendWidth;
                double boundary = Math.Max(cfg.InnerDeadzone + band, Math.Min(
                    Math.Abs(ux) > 1e-15 ? (cfg.XSnapWidth + band) / Math.Abs(ux) : double.PositiveInfinity,
                    Math.Abs(uy) > 1e-15 ? (cfg.YSnapWidth + band) / Math.Abs(uy) : double.PositiveInfinity));
                var before = Transform.ApplyStick((boundary - 1e-8) * ux, (boundary - 1e-8) * uy, cfg);
                var after = Transform.ApplyStick((boundary + 1e-8) * ux, (boundary + 1e-8) * uy, cfg);
                Near(before.X, after.X, "Center splice X continuity", 1e-6);
                Near(before.Y, after.Y, "Center splice Y continuity", 1e-6);
            }
        }

        // Anti-deadzone on a component fading into a cardinal strip must not
        // leave a jump at the new center splice.
        cfg = Basic(); cfg.XAntiDeadzone = .25; cfg.YAntiDeadzone = .4;
        double direction = 10 * Math.PI / 180;
        double boundaryRadius = .15 / Math.Cos(direction);
        a = Transform.ApplyStick((boundaryRadius - 1e-8) * Math.Cos(direction), (boundaryRadius - 1e-8) * Math.Sin(direction), cfg);
        p = Transform.ApplyStick((boundaryRadius + 1e-8) * Math.Cos(direction), (boundaryRadius + 1e-8) * Math.Sin(direction), cfg);
        Near(a.X, p.X, "Anti floor X splice continuity", 1e-6);
        Near(a.Y, p.Y, "Disappearing anti floor tapers to zero", 1e-6);

        // Crossing the direction where an anchor component becomes inactive
        // must not introduce an angular seam within the recovered center.
        cfg = Basic(); cfg.XAntiDeadzone = .1;
        direction = Math.Atan(1.5);
        boundaryRadius = .15 / Math.Sin(direction);
        double middleRadius = (cfg.InnerDeadzone + boundaryRadius) / 2;
        a = Transform.ApplyStick(middleRadius * Math.Cos(direction - 1e-8), middleRadius * Math.Sin(direction - 1e-8), cfg);
        p = Transform.ApplyStick(middleRadius * Math.Cos(direction + 1e-8), middleRadius * Math.Sin(direction + 1e-8), cfg);
        Near(a.X, p.X, "Anti floor is continuous across anchor activation angle", 1e-6);
        Near(a.Y, p.Y, "Y stays continuous across anchor activation angle", 1e-6);

        // Changing the inner cutoff cannot change established exterior mapping.
        cfg = Basic(); a = Transform.ApplyStick(.7, .35, cfg);
        cfg.InnerDeadzone = .2; cfg.Validate(); p = Transform.ApplyStick(.7, .35, cfg);
        Near(a.X, p.X, "Independent inner cutoff leaves exterior X unchanged");
        Near(a.Y, p.Y, "Independent inner cutoff leaves exterior Y unchanged");

        var old = JsonSerializer.Deserialize<Settings>("{\"InnerDeadzone\":0.12,\"OuterRingThreshold\":0.6,\"SnapAngleDeg\":13,\"StickXAxis\":5,\"ButtonMappings\":\"0=b\"}")!;
        old.Validate(); Check(old.MappingMode == StickMappingMode.LegacyAngular, "Old JSON must retain legacy mode");
        var random = new Random(7349);
        for (int i = 0; i < 1000; i++)
        {
            double x = random.NextDouble() * 2 - 1, y = random.NextDouble() * 2 - 1;
            a = Transform.Apply(x, y, old.InnerDeadzone, old.OuterRingThreshold, old.SnapAngleDeg);
            a = Transform.Square(a.X, a.Y); p = Transform.ApplyStick(x, y, old);
            Near(p.X, a.X, "Legacy X compatibility", 0); Near(p.Y, a.Y, "Legacy Y compatibility", 0);
        }
        cfg = Basic(); cfg.XSnapWidth = .13; cfg.YSnapWidth = .18; cfg.XAntiDeadzone = .2; cfg.YAntiDeadzone = .3;
        cfg.XMaxZone = .7; cfg.YMaxZone = .8; cfg.XMaxOutput = .6; cfg.YMaxOutput = .9;
        cfg.SquareStick = true; cfg.SquareRoundness = 5; cfg.StickXAxis = 5; cfg.ButtonMappings = "0=b";
        var copy = cfg.Snapshot(); copy.Validate();
        Check(JsonSerializer.Serialize(copy) == JsonSerializer.Serialize(cfg), "All settings survive snapshot/JSON round-trip");
        Check(copy.StickXAxis == 5 && copy.ParseButtons()[0].Mask == 0x2000, "Mappings survive new settings");

        foreach (string name in new[] { nameof(Settings.InnerDeadzone), nameof(Settings.OuterRingThreshold), nameof(Settings.SnapAngleDeg), nameof(Settings.XSnapWidth), nameof(Settings.YSnapWidth), nameof(Settings.XAntiDeadzone), nameof(Settings.YAntiDeadzone), nameof(Settings.XMaxZone), nameof(Settings.YMaxZone), nameof(Settings.XMaxOutput), nameof(Settings.YMaxOutput), nameof(Settings.SquareRoundness), nameof(Settings.CenterBlendWidth) })
            foreach (double bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
                Invalid(s => typeof(Settings).GetProperty(name)!.SetValue(s, bad));
        Invalid(s => s.MappingMode = (StickMappingMode)42);
        Invalid(s => s.CenterBlendWidth = 1e-20);
        Invalid(s => s.SquareRoundness = 1001);
        Invalid(s => s.InnerDeadzone = 1);
        Invalid(s => s.XSnapWidth = s.XMaxZone);
        Invalid(s => s.YMaxZone = s.YSnapWidth + s.CenterBlendWidth);
        Invalid(s => s.XMaxZone = s.InnerDeadzone + s.CenterBlendWidth);
        Console.WriteLine($"Decoupled axial checks passed ({checks:N0} assertions).");
    }
}
