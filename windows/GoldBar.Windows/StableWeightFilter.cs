namespace GoldBar.Windows;

public sealed class StableWeightFilter
{
    private readonly Queue<double> _values = new();

    public void Reset() => _values.Clear();

    public bool TryAdd(double value, int sampleCount, double tolerance, out double stable)
    {
        stable = double.NaN;
        if (!double.IsFinite(value)) return false;

        sampleCount = Math.Clamp(sampleCount, 2, 10);
        tolerance = Math.Max(0.0001, tolerance);

        _values.Enqueue(value);
        while (_values.Count > sampleCount) _values.Dequeue();
        if (_values.Count < sampleCount) return false;

        var min = _values.Min();
        var max = _values.Max();
        if (max - min > tolerance) return false;

        stable = _values.Average();
        return true;
    }
}
