using System.IO;
using System.IO.Compression;
using GoldBar.Windows.Models;
using GoldBar.Windows.Services;

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
        Check(Math.Abs(median.Push(101.0) - 101.0) < 0.000001, "Median stabilizer helper rejects spike");

        using (var scale = new ScaleService())
        {
            var diagnostic = scale.TestAsync(new ScaleSettings
            {
                ScaleName = "ترازوی تست",
                Port = "COM65535",
                AutoRead = false
            }, 500).GetAwaiter().GetResult();
            Check(!diagnostic.Ok && diagnostic.Message.Contains("پیدا نشد", StringComparison.Ordinal),
                "Scale test reports a useful missing-COM diagnostic");
        }

        var s = new ScaleSettings
        {
            ScaleName = "  ترازو کارگاه  ", Port = " ", BaudRate = 50, DataBits = 99, Parity = "bad", StopBits = 9,
            FlowControl = "bad", ReadIntervalMs = 2, Decimals = 99, RequestCommand = null!,
            ReportDirectory = " "
        }.Normalize();
        Check(s.ScaleName == "ترازو کارگاه", "Scale name normalizes safely");
        Check(s.Port == "COM4", "Settings default COM port");
        Check(s.BaudRate == 300 && s.DataBits == 7 && s.Parity == "Even" && s.StopBits == 2,
              "Serial settings normalize safely");
        Check(s.ReadIntervalMs == 100 && s.Decimals == 6, "Numeric settings clamp safely");
        Check(s.RequestCommand == string.Empty, "Null request command normalized");
        Check(!string.IsNullOrWhiteSpace(s.ReportDirectory) && Path.IsPathFullyQualified(s.ReportDirectory),
              "Report directory has a safe absolute default");

        var reportDir = Path.Combine(Path.GetTempPath(), $"GoldBar-Report-Test-{Guid.NewGuid():N}");
        try
        {
            var request = new ReportRequest
            {
                Entries =
                [
                    new ReportEntry { Id = "1", Weight = 100, Assay = 740, Description = "تست اول", CreatedAt = "1405/05/28 - 12:00" },
                    new ReportEntry { Id = "2", Weight = 50, Assay = 760, Description = "تست دوم", CreatedAt = "1405/05/28 - 12:01" }
                ]
            };
            var path = new ReportService().SaveXlsx(reportDir, request);
            Check(File.Exists(path) && new FileInfo(path).Length > 1000 && string.Equals(Path.GetExtension(path), ".xlsx", StringComparison.OrdinalIgnoreCase),
                "Report is a real XLSX file");

            using var zip = ZipFile.OpenRead(path);
            var contentTypes = zip.GetEntry("[Content_Types].xml");
            var workbook = zip.GetEntry("xl/workbook.xml");
            var sheet = zip.GetEntry("xl/worksheets/sheet1.xml");
            Check(contentTypes is not null && workbook is not null && sheet is not null,
                  "Report XLSX has required OpenXML parts");

            var sheetXml = string.Empty;
            if (sheet is not null)
            {
                using var reader = new StreamReader(sheet.Open());
                sheetXml = reader.ReadToEnd();
            }
            Check(sheetXml.Contains("وزن (g)", StringComparison.Ordinal) &&
                  sheetXml.Contains("عیار (‰)", StringComparison.Ordinal) &&
                  sheetXml.Contains(">740<", StringComparison.Ordinal) &&
                  sheetXml.Contains(">760<", StringComparison.Ordinal) &&
                  sheetXml.Contains("746.666", StringComparison.Ordinal),
                  "Report XLSX contains entries and weighted summary");

            var imported = new ReportImportService().LoadXlsx(path);
            Check(imported.Entries.Count == 2 &&
                  Math.Abs(imported.Entries[0].Weight - 100) < 0.000001 && imported.Entries[0].Assay == 740 &&
                  Math.Abs(imported.Entries[1].Weight - 50) < 0.000001 && imported.Entries[1].Assay == 760,
                  "Saved XLSX imports back without changing melt values");
        }
        catch (Exception ex)
        {
            output.WriteLine("FAIL: Report export/import exception: " + ex);
            failures.Add("Report export/import exception");
        }
        finally
        {
            try { if (Directory.Exists(reportDir)) Directory.Delete(reportDir, true); } catch { }
        }

        output.WriteLine(failures.Count == 0 ? "SELF-TEST: PASS" : $"SELF-TEST: FAIL ({failures.Count})");
        return failures.Count == 0 ? 0 : 1;
    }
}
