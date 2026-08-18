using System.Diagnostics;

namespace GoldBar.Windows;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        using var splash = new SplashForm();
        splash.Show();
        splash.Update();
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

        // Keep the splash visible long enough to avoid a blank/late-start feeling,
        // while still opening the main window as soon as it is ready.
        while (watch.ElapsedMilliseconds < 420)
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }

        splash.Close();
        Application.Run(main);
    }
}
