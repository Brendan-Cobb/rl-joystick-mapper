using RlRemapper;
using System.Text.Json;

internal static class AngularChecks
{
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Near(double actual, double expected) => Check(Math.Abs(actual - expected) < 1e-12, $"Expected {expected}, got {actual}");
    public static void Run()
    {
        var old = JsonSerializer.Deserialize<Settings>("{}")!;
        Check(old.AngularMaxZone == 1 && old.AngularSquareOutput, "Old profiles must keep angular defaults");
        for (int i = -20; i <= 20; i++) for (int j = -20; j <= 20; j++) {
            var before = Transform.Apply(i / 20.0, j / 20.0, old.InnerDeadzone, old.OuterRingThreshold, old.SnapAngleDeg);
            before = Transform.Square(before.X, before.Y);
            var after = Transform.ApplyStick(i / 20.0, j / 20.0, old);
            Near(after.X, before.X); Near(after.Y, before.Y);
        }
        var cfg = new Settings { AngularMaxZone = .5, AngularSquareOutput = false };
        cfg.Validate();
        var output = Transform.ApplyStick(.25, 0, cfg); Near(output.X, .5); Near(output.Y, 0);
        output = Transform.ApplyStick(.5, 0, cfg); Near(output.X, 1);
        output = Transform.ApplyStick(1, 1, cfg); Near(Math.Sqrt(output.X * output.X + output.Y * output.Y), 1);
        output = Transform.ApplyStick(.01, .01, cfg); Near(output.X, 0); Near(output.Y, 0);
        cfg.OuterRingThreshold = .4;
        output = Transform.ApplyStick(.2, .01, cfg); Near(output.Y / output.X, .05); // Raw radius stays below snap threshold.
        cfg.OuterRingThreshold = 0;
        output = Transform.ApplyStick(.2, .01, cfg); Near(output.Y, 0);
        cfg.AngularSquareOutput = true;
        output = Transform.ApplyStick(.5, .5, cfg); Near(output.X, 1); Near(output.Y, 1);
        var copy = cfg.Snapshot(); Check(copy.AngularSquareOutput && copy.AngularMaxZone == .5, "Angular settings did not round-trip");
        foreach (double zone in new[] { 0, -.1, .05, 1.1, double.NaN, double.PositiveInfinity }) {
            cfg.AngularMaxZone = zone;
            bool rejected = false; try { cfg.Validate(); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "Invalid angular max zone accepted");
        }
        Console.WriteLine("Angular compatibility, max zone, shape, threshold, validation and serialization checks passed.");
    }
}
