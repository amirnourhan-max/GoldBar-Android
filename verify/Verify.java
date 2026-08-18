import com.amirnourhan.goldbar.*;
import java.util.*;

public class Verify {
    private static void near(String name, double actual, double expected, double eps) {
        if (!Double.isFinite(actual) || Math.abs(actual - expected) > eps) {
            throw new AssertionError(name + " expected=" + expected + " actual=" + actual);
        }
    }

    public static void main(String[] args) {
        // Exact visible sample in "Golde Bar edite.xlsx":
        // 183.95 @ 750 + 316.05 @ 720 = 500 g, weighted sum 365518.5, avg 731.037.
        List<GoldEntry> low = Arrays.asList(
                new GoldEntry(1, 183.95, 750),
                new GoldEntry(1, 316.05, 720));

        GoldCalculator.Summary lowS = GoldCalculator.summarize(low, null);
        near("edited workbook weight", lowS.weight, 500.0, 1e-9);
        near("edited workbook weighted", lowS.weightedSum, 365518.5, 1e-9);
        near("edited workbook average", lowS.averageAssay, 731.037, 1e-9);

        GoldCalculator.Adjustment raise =
                GoldCalculator.requiredHighAssayBar(lowS, 747, 995);
        near("raise difference", raise.assayDifference, 15.963, 1e-9);
        near("required 995 bar", raise.requiredBar, 32.1, 1e-9);

        GoldCalculator.Alloy noLower =
                GoldCalculator.requiredAlloy(lowS, 746, 32, lowS.weight);
        near("no negative lower alloy", noLower.totalAlloyRequired, 0.0, 1e-12);
        near("no negative silver", noLower.silverRequired, 0.0, 1e-12);

        List<GoldEntry> high = Arrays.asList(
                new GoldEntry(1,84.38,749), new GoldEntry(1,86.69,750),
                new GoldEntry(1,14,749), new GoldEntry(1,23.48,778),
                new GoldEntry(1,36.26,977), new GoldEntry(1,66.07,749),
                new GoldEntry(1,42.23,757));
        GoldCalculator.Summary highS = GoldCalculator.summarize(high, null);

        GoldCalculator.Adjustment noRaise =
                GoldCalculator.requiredHighAssayBar(highS, 747, 995);
        near("no negative high bar", noRaise.requiredBar, 0.0, 1e-12);

        GoldCalculator.Alloy lower =
                GoldCalculator.requiredAlloy(highS, 746, 32, highS.weight);
        near("lower total alloy", lower.totalAlloyRequired, 13.983994638069703, 1e-9);
        near("lower silver 32%", lower.silverRequired, 4.474878284182305, 1e-9);
        near("lower non-silver", lower.nonSilverRequired, 9.509116353887398, 1e-9);
        near("lower 0.4%", lower.fourPerThousand, 1.41244, 1e-9);
        near("lower final other", lower.finalOtherAlloy, 8.096676353887398, 1e-9);
        near("lower final weight", lower.totalAfterAlloy, 367.0939946380697, 1e-9);

        near("36.79%", GoldCalculator.split3679(800), 294.32, 1e-9);
        near("correction addition",
                GoldCalculator.correctionAddition(250, 750, 1),
                0.3337783711615634, 1e-9);

        System.out.println("Business logic PASS: separate raise/lower formulas");
    }
}
