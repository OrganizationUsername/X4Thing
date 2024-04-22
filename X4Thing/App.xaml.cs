using System.Windows;

namespace X4Thing;
public partial class App : Application
{
    private void Application_Startup(object sender, StartupEventArgs e)
    {
        var mainWindowViewModel = new MainWindowViewModel();
        var mainWindow = new MainWindow(mainWindowViewModel);
        mainWindow.Show();
    }
}