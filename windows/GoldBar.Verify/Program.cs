using GoldBar.Core;

static void Near(string name, double actual, double expected, double eps = 1e-9)
{
    if (!double.IsFinite(actual) || Math.Abs(actual - expected) > eps)
        throw new Exception($"{name}: expected={expected} actual={actual}");
}

var low = new List<GoldEntry>
{
    new(183.95, 750),
    new(316.05, 720)
};
var lowSummary = GoldCalculator.Summarize(low);
Near("edited workbook weight", lowSummary.Weight, 500.0);
Near("edited workbook weighted", lowSummary.WeightedSum, 365518.5);
Near("edited workbook average", lowSummary.AverageAssay, 731.037);

var raise = GoldCalculator.RequiredHighAssayBar(lowSummary, 747, 995);
Near("raise difference", raise.DifferenceNeeded, 15.963);
Near("required 995 bar", raise.RequiredHighBar, 32.1);

var noLower = GoldCalculator.RequiredAlloy(lowSummary, 746, 32, lowSummary.Weight);
Near("no negative lower alloy", noLower.TotalAlloyRequired, 0.0, 1e-12);

var high = new List<GoldEntry>
{
    new(84.38,749), new(86.69,750), new(14,749), new(23.48,778),
    new(36.26,977), new(66.07,749), new(42.23,757)
};
var highSummary = GoldCalculator.Summarize(high);
var noRaise = GoldCalculator.RequiredHighAssayBar(highSummary, 747, 995);
Near("no negative high bar", noRaise.RequiredHighBar, 0.0, 1e-12);

var lower = GoldCalculator.RequiredAlloy(highSummary, 746, 32, highSummary.Weight);
Near("lower total alloy", lower.TotalAlloyRequired, 13.983994638069703);
Near("lower silver", lower.SilverRequired, 4.474878284182305);
Near("lower non-silver", lower.NonSilverRequired, 9.509116353887398);
Near("lower 0.4%", lower.FourPerThousand, 1.41244);
Near("lower final other", lower.FinalOtherAlloy, 8.096676353887398);
Near("lower final weight", lower.TotalAfterAlloy, 367.0939946380697);

Near("36.79%", GoldCalculator.Split3679(800), 294.32);
Near("correction", GoldCalculator.CorrectionAddition(250, 750, 1), 0.3337783711615634);

Console.WriteLine("Windows Gold Bar business logic PASS");
