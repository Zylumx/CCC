using System;
using System.Linq;
using System.Windows;

namespace CCC;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        bool adminMode = e.Args.Any(arg =>
            string.Equals(
                arg,
                "--admin",
                StringComparison.OrdinalIgnoreCase));

        var mainWindow = new MainWindow();

        MainWindow = mainWindow;

        mainWindow.Show();

        if (adminMode)
        {
            mainWindow.Dispatcher.BeginInvoke(new Action(() =>
            {
                var adminWindow = new AdminWindow
                {
                    Owner = mainWindow
                };

                adminWindow.ShowDialog();
            }));
        }
    }
}