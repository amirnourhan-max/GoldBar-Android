using System.Diagnostics;
using System.Drawing.Imaging;

namespace GoldBar.Windows;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        using var splash = new SplashForm();
        splash.Show();
        splash.Refresh();
        Application.DoEvents();

        var watch = Stopwatch.StartNew();
        DesktopMainForm main;
        try
        {
            main = new DesktopMainForm();
        }
        catch (Exception ex)
        {
            splash.Close();
            MessageBox.Show(
                "راه‌اندازی Gold Bar انجام نشد:\n" + ex.Message,
                "Gold Bar",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        ConfigureUiScreenshot(main);

        // The splash is intentionally short: it covers real initialization time,
        // but never makes a fast PC wait for seconds.
        while (watch.ElapsedMilliseconds < 380)
        {
            Application.DoEvents();
            Thread.Sleep(8);
        }

        splash.Close();
        Application.Run(main);
    }

    private static void ConfigureUiScreenshot(DesktopMainForm main)
    {
        var path = Environment.GetEnvironmentVariable("GOLDBAR_UI_SCREENSHOT");
        if (string.IsNullOrWhiteSpace(path)) return;

        main.Shown += (_, _) =>
        {
            var timer = new System.Windows.Forms.Timer { Interval = 800 };
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
