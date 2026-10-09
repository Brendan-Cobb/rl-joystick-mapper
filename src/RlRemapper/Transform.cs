namespace RlRemapper;

public static class Transform
{
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
