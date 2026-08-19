using System.Globalization;
using System.Text.RegularExpressions;

namespace GoldBar.Windows.Core;

public static partial class WeightParser
{
    [GeneratedRegex(@"[-+]?\d+(?:[\.,]\d+)?", RegexOptions.CultureInvariant)]
    private static partial Regex NumberRegex();

    public static double? Parse(string? raw, int decimals)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var matches = NumberRegex().Matches(raw);
        if (matches.Count == 0) return null;
        var token = matches[^1].Value.Replace(',', '.');
        if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) return null;
        return Math.Round(value, Math.Clamp(decimals, 0, 6), MidpointRounding.AwayFromZero);
    }
}

public sealed class MedianStabilizer
{
    private readonly int _window;
    private readonly Queue<double> _values = new();
    private readonly object _gate = new();

    public MedianStabilizer(int window = 3) => _window = Math.Max(1, window);

    public double Push(double value)
    {
        lock (_gate)
        {
            _values.Enqueue(value);
            while (_values.Count > _window) _values.Dequeue();
            var a = _values.OrderBy(v => v).ToArray();
            var m = a.Length / 2;
            return a.Length % 2 == 1 ? a[m] : (a[m - 1] + a[m]) / 2.0;
        }
    }

    public void Reset()
    {
        lock (_gate) _values.Clear();
    }
}
