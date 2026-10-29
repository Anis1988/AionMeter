using System.Windows;

namespace AionMeter;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var controller = new MeterController(Settings.Load());
        var window = new MainWindow(controller);
        MainWindow = window;
        window.Show();
        controller.Start();
    }
}
