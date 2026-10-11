namespace RlRemapper;

public static class Transform
{
    /// <summary>Maps the left stick. Existing profiles retain their original angular pipeline.</summary>
    public static (double X, double Y) ApplyStick(double x, double y, Settings settings)
    {
        if (settings.MappingMode == StickMappingMode.LegacyAngular)
        {
            var legacy = Apply(x, y, settings.InnerDeadzone, settings.OuterRingThreshold, settings.SnapAngleDeg);
            return Square(legacy.X, legacy.Y);
        }

        // Settings are validated when published, not on every controller poll.
        if (!double.IsFinite(x) || !double.IsFinite(y)) return (0, 0);
        x = Math.Clamp(x, -1, 1);
        y = Math.Clamp(y, -1, 1);
        double radius = Math.Sqrt(x * x + y * y);
        if (radius <= settings.InnerDeadzone || radius == 0) return (0, 0);

        double ux = Math.Abs(x) / radius, uy = Math.Abs(y) / radius;
        double band = settings.CenterBlendWidth;
        // The transition ends at the expanded overlap rectangle or the inner
        // deadzone's surrounding band, whichever this ray leaves last. Expanding
        // both widths before dividing also keeps zero-width strips continuous.
        double rectangleRadius = Math.Min(
            ux > 0 ? (settings.XSnapWidth + band) / ux : double.PositiveInfinity,
            uy > 0 ? (settings.YSnapWidth + band) / uy : double.PositiveInfinity);
        double transitionRadius = Math.Max(settings.InnerDeadzone + band, rectangleRadius);

        double ax, ay, antiWeightX = 1, antiWeightY = 1;
        if (radius >= transitionRadius)
        {
            // Outside the center transition, use the ordinary normalized axial
            // mapping exactly. No byte rounding is introduced between stages.
            ax = AxialTravel(Math.Abs(x), settings.XSnapWidth, settings.XMaxZone);
            ay = AxialTravel(Math.Abs(y), settings.YSnapWidth, settings.YMaxZone);
        }
        else
        {
            double bx = AxialTravel(transitionRadius * ux, settings.XSnapWidth, settings.XMaxZone);
            double by = AxialTravel(transitionRadius * uy, settings.YSnapWidth, settings.YMaxZone);
            double boundaryLength = Math.Sqrt(bx * bx + by * by);
            double t = (radius - settings.InnerDeadzone) / (transitionRadius - settings.InnerDeadzone);
            double blend = t * t * (3 - 2 * t);
            // Fade a disappearing component's anti floor continuously in both
            // radius and direction. A binary test for a zero boundary component
            // would introduce an angular seam inside this transition.
            double remaining = 1 - blend;
            double contributionX = bx + remaining * ux;
            double contributionY = by + remaining * uy;
            antiWeightX = contributionX > 0 ? remaining + blend * bx / contributionX : 0;
            antiWeightY = contributionY > 0 ? remaining + blend * by / contributionY : 0;

            // Recover the input direction near the inner circle, then converge
            // to the exterior axial direction. Magnitude rises linearly along
            // each ray, so recovery cannot create another central dead pocket.
            double vx = ux + (bx / boundaryLength - ux) * blend;
            double vy = uy + (by / boundaryLength - uy) * blend;
            double directionLength = Math.Sqrt(vx * vx + vy * vy);
            double magnitude = t * boundaryLength;
            ax = magnitude * vx / directionLength;
            ay = magnitude * vy / directionLength;
        }

        ax = AxisResponse(ax, settings.XAntiDeadzone * antiWeightX, settings.XMaxOutput) * Math.Sign(x);
        ay = AxisResponse(ay, settings.YAntiDeadzone * antiWeightY, settings.YMaxOutput) * Math.Sign(y);
        if (settings.SquareStick) (ax, ay) = Square(ax, ay, settings.SquareRoundness);
        return (Math.Clamp(ax, -1, 1), Math.Clamp(ay, -1, 1));
    }

    static double AxialTravel(double value, double strip, double maxZone) =>
        Math.Clamp((value - strip) / (maxZone - strip), 0, 1);

    static double AxisResponse(double travel, double antiDeadzone, double maxOutput)
    {
        // Match DS4's ordering: its output cap (including the 0.99 expansion)
        // precedes anti-deadzone and Square Stick, so it is not a final ceiling.
        if (maxOutput != 1) travel = Math.Min(travel, Math.Min(maxOutput / 0.99, 1));
        return travel > 0 ? antiDeadzone + (1 - antiDeadzone) * travel : 0;
    }

    public static (double X, double Y) Square(double x, double y, double roundness)
    {
        if (x == 0 || y == 0) return (x, y);
        double radius = Math.Sqrt(x * x + y * y);
        double expansion = radius / Math.Max(Math.Abs(x), Math.Abs(y));
        double scale = 1 + (expansion - 1) * Math.Pow(radius, roundness);
        return (Math.Clamp(x * scale, -1, 1), Math.Clamp(y * scale, -1, 1));
    }

    public static (double X, double Y) Apply(double x, double y, double inner, double outer, double threshold)
    {
        double magnitude = Math.Sqrt(x * x + y * y);
        if (magnitude < inner || magnitude == 0) return (0, 0);
        if (magnitude < outer) return (x, y);
        double angle = Math.Atan2(y, x) * 180 / Math.PI;
        if (Math.Abs(angle - 90) <= threshold) return (0, magnitude);
        if (Math.Abs(angle + 90) <= threshold) return (0, -magnitude);
        if (Math.Abs(angle) <= threshold) return (magnitude, 0);
        if (Math.Abs(Math.Abs(angle) - 180) <= threshold) return (-magnitude, 0);
        double scale = 90 / (90 - 2 * threshold);
        double remapped = angle > 0
            ? angle < 90 ? (angle - threshold) * scale : 90 + (angle - 90 - threshold) * scale
            : angle > -90 ? (angle + threshold) * scale : -90 + (angle + 90 + threshold) * scale;
        double rad = remapped * Math.PI / 180;
        return (Math.Cos(rad) * magnitude, Math.Sin(rad) * magnitude);
    }
    public static (double X, double Y) Square(double x, double y)
    {
        if (x == 0 || y == 0) return (x, y);
        double scale = Math.Sqrt(x * x + y * y) / Math.Max(Math.Abs(x), Math.Abs(y));
        return (x * scale, y * scale);
    }
    public static short Stick(double value) => (short)(Math.Clamp(value, -1, 1) * 32767);
    public static byte Trigger(double value) => (byte)(Math.Clamp((value + 1) / 2, 0, 1) * 255);
}
