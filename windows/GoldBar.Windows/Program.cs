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

        using var splash = new SplashForm();
        splash.Show();
        splash.Refresh();
        Application.DoEvents();

        var watch = Stopwatch.StartNew();
        DesktopMainForm main;
        try
        {
            main = new DesktopMainForm();
            ApplyTestPage(main);
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

        while (watch.ElapsedMilliseconds < 380)
        {
            Application.DoEvents();
            Thread.Sleep(8);
        }

        splash.Close();
        Application.Run(main);
    }

    private static void ApplyTestPage(DesktopMainForm main)
    {
        var page = Environment.GetEnvironmentVariable("GOLDBAR_UI_PAGE");
        if (string.IsNullOrWhiteSpace(page) || page.Equals("dashboard", StringComparison.OrdinalIgnoreCase)) return;
        try
        {
            var method = typeof(DesktopMainForm).GetMethod("ShowPage", BindingFlags.Instance | BindingFlags.NonPublic);
            method?.Invoke(main, new object[] { page });
        }
        catch { }
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
