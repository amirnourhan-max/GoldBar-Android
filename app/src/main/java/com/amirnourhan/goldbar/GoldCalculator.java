package com.amirnourhan.goldbar;

import java.util.List;

/**
 * Gold assay calculations ported from "Golde Bar edite.xlsx".
 * Assays are per-thousand (e.g. 747, 995).
 *
 * Important: raising and lowering assay are two different mixing equations.
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

    /** Excel ROUNDDOWN(number,digits): truncation toward zero. */
    public static double roundDownTowardZero(double value, int digits) {
        if (!Double.isFinite(value)) return Double.NaN;
        double factor = Math.pow(10.0, digits);
        double scaled = value * factor;
        double truncated = scaled >= 0 ? Math.floor(scaled) : Math.ceil(scaled);
        return truncated / factor;
    }

    /**
     * RAISE assay using a high-assay bar:
     *
     * (W*A + X*H) / (W+X) = T
     * X = W*(T-A)/(H-T)
     *
     * Workbook Table1 uses ROUNDDOWN(...,1).
     * If A >= T, raising is not needed and X is zero (never negative).
     */
    public static Adjustment requiredHighAssayBar(Summary s, double targetAssay, double barAssay) {
        if (s.weight <= 0 || !Double.isFinite(s.averageAssay)
                || targetAssay <= 0 || barAssay <= targetAssay) {
            return new Adjustment(Double.NaN, Double.NaN, Double.NaN);
        }

        double differenceNeeded = targetAssay - s.averageAssay;
        double denominator = barAssay - targetAssay;

        if (differenceNeeded <= 0.0) {
            return new Adjustment(0.0, denominator, 0.0);
        }

        double required = roundDownTowardZero(
                (s.weight * differenceNeeded) / denominator, 1);
        return new Adjustment(differenceNeeded, denominator, Math.max(0.0, required));
    }

    /**
     * LOWER assay by adding zero-gold alloy (the exact assumption used by Table14):
     *
     * (W*A) / (W+X) = T
     * X = W*A/T - W = W*(A-T)/T
     *
     * If A <= T, lowering is not needed and every output is zero.
     */
    public static Alloy requiredAlloy(Summary s, double targetAssay, double silverPercent,
                                      double globalWeight) {
        if (s.weight <= 0 || !Double.isFinite(s.averageAssay)
                || targetAssay <= 0 || silverPercent < 0) {
            return new Alloy(Double.NaN, Double.NaN, Double.NaN,
                    Double.NaN, Double.NaN, Double.NaN);
        }

        if (s.averageAssay <= targetAssay) {
            return new Alloy(0.0, 0.0, 0.0, 0.0, 0.0, s.weight);
        }

        double total = s.weight * s.averageAssay / targetAssay - s.weight;
        double silver = (silverPercent / 100.0) * total;
        double nonSilver = total - silver;
        double fourPerThousand = Math.max(0.0, globalWeight) * 0.004;
        double finalOther = total - silver - fourPerThousand;
        double after = s.weight + total;

        return new Alloy(total, silver, nonSilver, fourPerThousand, finalOther, after);
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
