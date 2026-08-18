package com.amirnourhan.goldbar;

import java.util.List;

/**
 * Exact business logic ported from Golde Bar1-1.xlsx.
 * Assays are expressed in per-thousand (e.g. 747, 995).
 */
public final class GoldCalculator {
    private GoldCalculator() {}

    public static final class Summary {
        public final int count;
        public final double weight;
        public final double weightedSum;
        public final double averageAssay;

        Summary(int count, double weight, double weightedSum, double averageAssay) {
            this.count = count;
            this.weight = weight;
            this.weightedSum = weightedSum;
            this.averageAssay = averageAssay;
        }
    }

    public static final class Adjustment {
        public final double assayDifference;
        public final double denominator;
        public final double requiredBar;

        Adjustment(double assayDifference, double denominator, double requiredBar) {
            this.assayDifference = assayDifference;
            this.denominator = denominator;
            this.requiredBar = requiredBar;
        }
    }

    public static final class Alloy {
        public final double totalAlloyRequired;
        public final double silverRequired;
        public final double nonSilverRequired;
        public final double fourPerThousand;
        public final double finalOtherAlloy;
        public final double totalAfterAlloy;

        Alloy(double totalAlloyRequired, double silverRequired, double nonSilverRequired,
              double fourPerThousand, double finalOtherAlloy, double totalAfterAlloy) {
            this.totalAlloyRequired = totalAlloyRequired;
            this.silverRequired = silverRequired;
            this.nonSilverRequired = nonSilverRequired;
            this.fourPerThousand = fourPerThousand;
            this.finalOtherAlloy = finalOtherAlloy;
            this.totalAfterAlloy = totalAfterAlloy;
        }
    }

    public static Summary summarize(List<GoldEntry> entries, Integer guideOrNull) {
        int count = 0;
        double weight = 0.0;
        double weighted = 0.0;
        for (GoldEntry e : entries) {
            if (guideOrNull == null || e.guide == guideOrNull) {
                if (e.weight > 0 && e.assay > 0) {
                    count++;
                    weight += e.weight;
                    weighted += e.weight * e.assay;
                }
            }
        }
        double avg = weight == 0.0 ? Double.NaN : weighted / weight;
        return new Summary(count, weight, weighted, avg);
    }

    /** Excel: ROUNDDOWN(number, digits), which truncates toward zero. */
    public static double roundDownTowardZero(double value, int digits) {
        if (!Double.isFinite(value)) return Double.NaN;
        double factor = Math.pow(10.0, digits);
        double scaled = value * factor;
        double truncated = scaled >= 0 ? Math.floor(scaled) : Math.ceil(scaled);
        return truncated / factor;
    }

    /**
     * Excel Table1:
     * difference = castingAssay - averageAssay
     * denominator = barAssay - castingAssay
     * requiredBar = ROUNDDOWN(weight * difference / denominator, 1)
     */
    public static Adjustment requiredHighAssayBar(Summary s, double castingAssay, double barAssay) {
        if (s.weight <= 0 || !Double.isFinite(s.averageAssay)) {
            return new Adjustment(Double.NaN, Double.NaN, Double.NaN);
        }
        double diff = castingAssay - s.averageAssay;
        double denominator = barAssay - castingAssay;
        if (denominator == 0.0) {
            return new Adjustment(diff, denominator, Double.NaN);
        }
        double required = roundDownTowardZero((s.weight * diff) / denominator, 1);
        return new Adjustment(diff, denominator, required);
    }

    /**
     * Excel Table14:
     * total alloy = weight * avgAssay / castingAssay - weight
     * silver = silverPercent / 100 * total alloy
     * 0.4% item = global total weight * 0.004 (matches workbook N3*0.004)
     * final other = total alloy - silver - 0.4% item
     */
    public static Alloy requiredAlloy(Summary selected, double castingAssay, double silverPercent,
                                      double globalWeight) {
        if (selected.weight <= 0 || !Double.isFinite(selected.averageAssay) || castingAssay == 0.0) {
            return new Alloy(Double.NaN, Double.NaN, Double.NaN, Double.NaN, Double.NaN, Double.NaN);
        }
        double total = selected.weight * selected.averageAssay / castingAssay - selected.weight;
        double silver = (silverPercent / 100.0) * total;
        double other = total - silver;
        double fourPerThousand = globalWeight * 0.004;
        double finalOther = total - silver - fourPerThousand;
        double after = selected.weight + total;
        return new Alloy(total, silver, other, fourPerThousand, finalOther, after);
    }

    public static double split3679(double base) {
        return base * 0.3679;
    }

    public static double correctionAddition(double baseWeight, double targetAssay, double assayDrop) {
        double denominator = targetAssay - assayDrop;
        if (denominator == 0.0) return Double.NaN;
        return (baseWeight * targetAssay) / denominator - baseWeight;
    }
}
