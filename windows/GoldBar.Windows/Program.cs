using System.Diagnostics;
using System.Drawing.Imaging;

namespace GoldBar.Windows;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        if (Environment.GetEnvironmentVariable("GOLDBAR_SETTINGS_SELFTEST") == "1")
        {
            RunSettingsSelfTest();
            return;
        }
        if (Environment.GetEnvironmentVariable("GOLDBAR_SCALE_SELFTEST") == "1")
        {
            RunScaleSelfTest();
            return;
        }

        using var splash = new ModernSplashForm();
        splash.Show();
        splash.Refresh();
        Application.DoEvents();

        var watch = Stopwatch.StartNew();
        ModernMainForm main;
        try
        {
            main = new ModernMainForm();
            ModernLayoutPolish.Attach(main);
            ApplyTestSize(main);

            var page = Environment.GetEnvironmentVariable("GOLDBAR_UI_PAGE");
            if (!string.IsNullOrWhiteSpace(page))
            {
                main.Shown += (_, _) =>
                {
                    try { main.BeginInvoke((Action)(() => main.ShowPageForTest(page))); }
                    catch { }
                };
            }

            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GOLDBAR_UI_SIZE")))
                main.WindowState = FormWindowState.Maximized;
        }
        catch (Exception ex)
        {
            splash.Close();
            MessageBox.Show("راه‌اندازی Gold Bar انجام نشد:\n" + ex.Message,
                "Gold Bar", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        ConfigureUiScreenshot(main);

        while (watch.ElapsedMilliseconds < 380)
        {
            Application.DoEvents();
            Thread.Sleep(8);
        }

        splash.Close();
        Application.Run(main);
    }

    private static void RunSettingsSelfTest()
    {
        var reportPath = Path.Combine(Path.GetTempPath(), "GoldBar-Should-Not-Be-Created", Guid.NewGuid().ToString("N"));
        if (Directory.Exists(reportPath)) Directory.Delete(reportPath, true);
        var s = new AppSettings
        {
            ReportFolder = reportPath,
            AutoRead = false,
            StableAutoReadOnly = true,
            StableSampleCount = 4,
            StableToleranceGrams = 0.015,
            PortName = "COM9",
            BaudRate = 2400,
            DashboardEntryPercent = 63
        };
        s.Save();
        var loaded = AppSettings.Load();
        if (loaded.ReportFolder != reportPath || loaded.AutoRead || loaded.StableSampleCount != 4
            || Math.Abs(loaded.StableToleranceGrams - 0.015) > 1e-9 || loaded.DashboardEntryPercent != 63)
            throw new InvalidOperationException("Settings round-trip failed.");
        if (Directory.Exists(reportPath))
            throw new InvalidOperationException("Saving settings must not create the report directory.");
        var output = Environment.GetEnvironmentVariable("GOLDBAR_SELFTEST_OUT");
        if (!string.IsNullOrWhiteSpace(output)) File.WriteAllText(output, "SETTINGS SELFTEST PASS");
    }

    private static void RunScaleSelfTest()
    {
        if (new AppSettings().AutoRead)
            throw new InvalidOperationException("AutoRead must be OFF by default.");
        if (ScaleReader.IsStableSeries(new[] { 100.00, 100.12, 99.96 }, 3, 0.02, out _))
            throw new InvalidOperationException("Noisy scale series was incorrectly accepted.");
        if (!ScaleReader.IsStableSeries(new[] { 100.000, 100.008, 100.012 }, 3, 0.02, out var stable))
            throw new InvalidOperationException("Stable scale series was rejected.");
        if (Math.Abs(stable - 100.0066666667) > 0.0001)
            throw new InvalidOperationException("Stable weight average is incorrect.");
        var output = Environment.GetEnvironmentVariable("GOLDBAR_SELFTEST_OUT");
        if (!string.IsNullOrWhiteSpace(output)) File.WriteAllText(output, "SCALE STABILITY SELFTEST PASS");
    }

    private static void ApplyTestSize(Form main)
    {
        var raw = Environment.GetEnvironmentVariable("GOLDBAR_UI_SIZE");
        if (string.IsNullOrWhiteSpace(raw)) return;
        var parts = raw.ToLowerInvariant().Split('x');
        if (parts.Length == 2 && int.TryParse(parts[0], out var w) && int.TryParse(parts[1], out var h))
        {
            main.WindowState = FormWindowState.Normal;
            main.Size = new Size(Math.Max(main.MinimumSize.Width, w), Math.Max(main.MinimumSize.Height, h));
        }
    }

    private static void ConfigureUiScreenshot(Form main)
    {
        var path = Environment.GetEnvironmentVariable("GOLDBAR_UI_SCREENSHOT");
        if (string.IsNullOrWhiteSpace(path)) return;

        main.Shown += (_, _) =>
        {
            var timer = new System.Windows.Forms.Timer { Interval = 1800 };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                timer.Dispose();
                try
                {
                    main.Activate();
                    main.Refresh();
                    Application.DoEvents();

                    // Capture the actual Windows desktop pixels rather than relying on
                    // DrawToBitmap. This verifies overlays such as the integrated
                    // Settings drawer exactly as the operator sees them on screen.
                    var bounds = main.Bounds;
                    using var bitmap = new Bitmap(bounds.Width, bounds.Height);
                    using (var g = Graphics.FromImage(bitmap))
                    {
                        g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size, CopyPixelOperation.SourceCopy);
                    }
                    var directory = Path.GetDirectoryName(path);
                    if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
                    bitmap.Save(path, ImageFormat.Png);
                }
                catch { }

                if (Environment.GetEnvironmentVariable("GOLDBAR_SCREENSHOT_EXIT") == "1")
                    main.Close();
            };
            timer.Start();
        };
    }
}
