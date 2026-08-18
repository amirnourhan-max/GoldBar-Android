namespace GoldBar.Core;

public sealed class GoldEntry
{
    public double Weight { get; set; }
    public double Assay { get; set; }

    public GoldEntry() { }

    public GoldEntry(double weight, double assay)
    {
        Weight = weight;
        Assay = assay;
    }
}
