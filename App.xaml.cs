using System.Configuration;
using System.Data;
using System.Windows;
using Woobly.Services;
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
            AppLog.Error("Unhandled UI exception", e.Exception);
            WpfMessageBox.Show("Something went wrong. Woobly logged details for diagnostics.",
                          "Woobly Error",
                          MessageBoxButton.OK,
                          MessageBoxImage.Error);
            e.Handled = true;
        };
        
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                AppLog.Error("Fatal unhandled exception", ex);
            }
            else
            {
                AppLog.Error($"Fatal unhandled exception object: {e.ExceptionObject}");
            }

            WpfMessageBox.Show("A fatal error occurred. Woobly logged details for diagnostics.",
                          "Woobly Fatal Error",
                          MessageBoxButton.OK,
                          MessageBoxImage.Error);
        };
    }
}

