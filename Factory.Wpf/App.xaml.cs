using System.Windows;

namespace Factory.Wpf;
public partial class App
{
    private void Application_Startup(object sender, StartupEventArgs e)
    {
        var viewModel = new MainViewModel();
        var mainWindow = new MainWindow(viewModel);
        mainWindow.Show();
    }
}