import com.amirnourhan.goldbar.*;
import java.util.*;

public class Verify {
  private static void near(String name, double actual, double expected, double eps) {
    if (!Double.isFinite(actual) || Math.abs(actual - expected) > eps) {
      throw new AssertionError(name + " expected=" + expected + " actual=" + actual);
    }
  }

  public static void main(String[] args) {
    List<GoldEntry> e = Arrays.asList(
      new GoldEntry(1,84.38,749), new GoldEntry(1,86.69,750), new GoldEntry(1,14,749),
      new GoldEntry(1,23.48,778), new GoldEntry(1,36.26,977), new GoldEntry(1,66.07,749),
      new GoldEntry(1,42.23,757));

    GoldCalculator.Summary s = GoldCalculator.summarize(e, 1);
    GoldCalculator.Adjustment a = GoldCalculator.requiredHighAssayBar(s, 747, 995);
    GoldCalculator.Alloy x = GoldCalculator.requiredAlloy(s, 747, 45, s.weight);

    near("weight", s.weight, 353.11, 1e-9);
    near("average assay", s.averageAssay, 775.5433717538444, 1e-9);
    near("required 995 bar", a.requiredBar, -40.6, 1e-9);
    near("total alloy", x.totalAlloyRequired, 13.492570281124529, 1e-9);
    near("silver", x.silverRequired, 6.071656626506038, 1e-9);
    near("non-silver", x.nonSilverRequired, 7.420913654618491, 1e-9);
    near("0.4%", x.fourPerThousand, 1.41244, 1e-9);
    near("final other", x.finalOtherAlloy, 6.0084736546184905, 1e-9);

    System.out.println("Business logic PASS");
  }
}
