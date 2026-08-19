using System.Windows;
using GoldBar.Windows.Core;

namespace GoldBar.Windows;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Any(a => string.Equals(a, "--self-test", StringComparison.OrdinalIgnoreCase)))
        {
            var exitCode = SelfTest.Run(Console.Out);
            Shutdown(exitCode);
            return;
        }

        var uiSelfTest = e.Args.Any(a => string.Equals(a, "--ui-self-test", StringComparison.OrdinalIgnoreCase));
        var window = new MainWindow(uiSelfTest);
        MainWindow = window;
        window.Show();
    }
}
