using System.IO;
using GoldBar.Windows.Models;

namespace GoldBar.Windows.Core;

public static class SelfTest
{
    public static int Run(TextWriter output)
    {
        var failures = new List<string>();
        void Check(bool condition, string name)
        {
            if (condition) output.WriteLine($"PASS: {name}");
            else { output.WriteLine($"FAIL: {name}"); failures.Add(name); }
        }

        Check(WeightParser.Parse("ST,+ 214.373 g", 3) == 214.373, "WeightParser parses scale payload");
        Check(WeightParser.Parse("WT=102,500", 3) == 102.5, "WeightParser accepts comma decimal");
        Check(WeightParser.Parse("garbage", 3) is null, "WeightParser rejects nonnumeric payload");

        var median = new MedianStabilizer(3);
        median.Push(100.0); median.Push(999.0);
        Check(Math.Abs(median.Push(101.0) - 101.0) < 0.000001, "Median stabilizer rejects spike");

        var s = new ScaleSettings
        {
            Port = " ", BaudRate = 50, DataBits = 99, Parity = "bad", StopBits = 9,
            FlowControl = "bad", ReadIntervalMs = 2, Decimals = 99, RequestCommand = null!
        }.Normalize();
        Check(s.Port == "COM4", "Settings default COM port");
        Check(s.BaudRate == 300 && s.DataBits == 7 && s.Parity == "Even" && s.StopBits == 2,
              "Serial settings normalize safely");
        Check(s.ReadIntervalMs == 100 && s.Decimals == 6, "Numeric settings clamp safely");
        Check(s.RequestCommand == string.Empty, "Null request command normalized");

        output.WriteLine(failures.Count == 0 ? "SELF-TEST: PASS" : $"SELF-TEST: FAIL ({failures.Count})");
        return failures.Count == 0 ? 0 : 1;
    }
}
