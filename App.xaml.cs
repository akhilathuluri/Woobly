using System.Configuration;
using System.Data;
using System.Windows;
using WpfApp = System.Windows.Application;
using WpfMessageBox = System.Windows.MessageBox;

namespace Woobly;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : WpfApp
{
    public App()
    {
        // Prevent application from closing when windows close
        this.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        
        // Handle unhandled exceptions
        this.DispatcherUnhandledException += (s, e) =>
        {
            WpfMessageBox.Show($"Error: {e.Exception.Message}\n\n{e.Exception.StackTrace}", 
                          "Woobly Error", 
                          MessageBoxButton.OK, 
                          MessageBoxImage.Error);
            e.Handled = true;
        };
        
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            WpfMessageBox.Show($"Fatal Error: {e.ExceptionObject}", 
                          "Woobly Fatal Error", 
                          MessageBoxButton.OK, 
                          MessageBoxImage.Error);
        };
    }
}

