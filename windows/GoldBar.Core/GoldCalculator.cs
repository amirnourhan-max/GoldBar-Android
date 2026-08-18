namespace GoldBar.Core;

public static class GoldCalculator
{
    public sealed record Summary(int Count, double Weight, double WeightedSum, double AverageAssay);
    public sealed record RaiseResult(double DifferenceNeeded, double Denominator, double RequiredHighBar);
    public sealed record LowerResult(double TotalAlloyRequired, double SilverRequired, double NonSilverRequired,
        double FourPerThousand, double FinalOtherAlloy, double TotalAfterAlloy);

    public static Summary Summarize(IEnumerable<GoldEntry> entries)
    {
        var valid = entries.Where(e => e.Weight > 0 && e.Assay > 0).ToList();
        var weight = valid.Sum(e => e.Weight);
        var weighted = valid.Sum(e => e.Weight * e.Assay);
        var avg = weight == 0 ? double.NaN : weighted / weight;
        return new Summary(valid.Count, weight, weighted, avg);
    }

    // Excel ROUNDDOWN(number,digits): truncation toward zero.
    public static double RoundDownTowardZero(double value, int digits)
    {
        if (!double.IsFinite(value)) return double.NaN;
        var factor = Math.Pow(10, digits);
        var scaled = value * factor;
        var truncated = scaled >= 0 ? Math.Floor(scaled) : Math.Ceiling(scaled);
        return truncated / factor;
    }

    // Raise assay with a high-assay bar:
    // (W*A + X*H)/(W+X)=T  =>  X=W*(T-A)/(H-T)
    public static RaiseResult RequiredHighAssayBar(Summary s, double targetAssay, double barAssay)
    {
        if (s.Weight <= 0 || !double.IsFinite(s.AverageAssay) || targetAssay <= 0 || barAssay <= targetAssay)
            return new RaiseResult(double.NaN, double.NaN, double.NaN);

        var difference = targetAssay - s.AverageAssay;
        var denominator = barAssay - targetAssay;
        if (difference <= 0) return new RaiseResult(0, denominator, 0);

        var required = RoundDownTowardZero((s.Weight * difference) / denominator, 1);
        return new RaiseResult(difference, denominator, Math.Max(0, required));
    }

    // Lower assay by adding zero-gold alloy:
    // (W*A)/(W+X)=T  =>  X=W*A/T-W
    public static LowerResult RequiredAlloy(Summary s, double targetAssay, double silverPercent, double globalWeight)
    {
        if (s.Weight <= 0 || !double.IsFinite(s.AverageAssay) || targetAssay <= 0 || silverPercent < 0)
            return new LowerResult(double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN);

        if (s.AverageAssay <= targetAssay)
            return new LowerResult(0, 0, 0, 0, 0, s.Weight);

        var total = s.Weight * s.AverageAssay / targetAssay - s.Weight;
        var silver = silverPercent / 100.0 * total;
        var nonSilver = total - silver;
        var fourPerThousand = Math.Max(0, globalWeight) * 0.004;
        var finalOther = total - silver - fourPerThousand;
        var after = s.Weight + total;
        return new LowerResult(total, silver, nonSilver, fourPerThousand, finalOther, after);
    }

    public static double Split3679(double value) => value * 0.3679;

    public static double CorrectionAddition(double baseWeight, double targetAssay, double assayDrop)
    {
        var denominator = targetAssay - assayDrop;
        if (denominator == 0) return double.NaN;
        return baseWeight * targetAssay / denominator - baseWeight;
    }
}
