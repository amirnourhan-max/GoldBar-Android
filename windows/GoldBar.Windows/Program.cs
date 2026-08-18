using System.Diagnostics;
using System.Drawing.Imaging;
using System.Reflection;

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

        using var splash = new SplashForm();
        splash.Show();
        splash.Refresh();
        Application.DoEvents();

        var watch = Stopwatch.StartNew();
        DesktopMainFormV2 main;
        try
        {
            main = new DesktopMainFormV2();
            ApplyTestSize(main);
            ApplyTestPage(main);
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
        var impossibleReportPath = Path.Combine(Path.GetTempPath(), "GoldBar-Should-Not-Be-Created", Guid.NewGuid().ToString("N"));
        if (Directory.Exists(impossibleReportPath)) Directory.Delete(impossibleReportPath, true);
        var s = new AppSettings
        {
            ReportFolder = impossibleReportPath,
            AutoRead = false,
            StableAutoReadOnly = true,
            StableSampleCount = 4,
            StableToleranceGrams = 0.015,
            PortName = "COM9",
            BaudRate = 2400
        };
        s.Save();
        var loaded = AppSettings.Load();
        if (loaded.ReportFolder != impossibleReportPath || loaded.AutoRead || loaded.StableSampleCount != 4 || Math.Abs(loaded.StableToleranceGrams - 0.015) > 1e-9)
            throw new InvalidOperationException("Settings round-trip failed.");
        if (Directory.Exists(impossibleReportPath))
            throw new InvalidOperationException("Saving settings must not create the report directory.");
        var output = Environment.GetEnvironmentVariable("GOLDBAR_SELFTEST_OUT");
        if (!string.IsNullOrWhiteSpace(output)) File.WriteAllText(output, "SETTINGS SELFTEST PASS");
    }

    private static void ApplyTestPage(DesktopMainFormV2 main)
    {
        var page = Environment.GetEnvironmentVariable("GOLDBAR_UI_PAGE");
        if (string.IsNullOrWhiteSpace(page) || page.Equals("dashboard", StringComparison.OrdinalIgnoreCase)) return;
        try
        {
            var method = typeof(DesktopMainFormV2).GetMethod("ShowPage", BindingFlags.Instance | BindingFlags.NonPublic);
            method?.Invoke(main, new object[] { page });
        }
        catch { }
    }

    private static void ApplyTestSize(Form main)
    {
        var raw = Environment.GetEnvironmentVariable("GOLDBAR_UI_SIZE");
        if (string.IsNullOrWhiteSpace(raw)) return;
        var parts = raw.ToLowerInvariant().Split('x');
        if (parts.Length == 2 && int.TryParse(parts[0], out var w) && int.TryParse(parts[1], out var h))
            main.Size = new Size(Math.Max(main.MinimumSize.Width, w), Math.Max(main.MinimumSize.Height, h));
    }

    private static void ConfigureUiScreenshot(Form main)
    {
        var path = Environment.GetEnvironmentVariable("GOLDBAR_UI_SCREENSHOT");
        if (string.IsNullOrWhiteSpace(path)) return;

        main.Shown += (_, _) =>
        {
            var timer = new System.Windows.Forms.Timer { Interval = 900 };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                timer.Dispose();
                try
                {
                    main.Refresh();
                    using var bitmap = new Bitmap(main.Width, main.Height);
                    main.DrawToBitmap(bitmap, new Rectangle(Point.Empty, main.Size));
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
