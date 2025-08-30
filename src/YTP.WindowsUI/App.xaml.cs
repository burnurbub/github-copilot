using System.Windows;
using System;
using System.IO;
using System.Threading.Tasks;

namespace YTP.WindowsUI
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            // Wire global exception handlers so startup/runtime errors are visible
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            try
            {
                var p = Path.Combine(Path.GetTempPath(), "ytp_unhandled_exception.txt");
                File.WriteAllText(p, e.Exception.ToString());
                System.Windows.MessageBox.Show($"Unhandled exception: {e.Exception.Message}\nDetails written to: {p}", "Unhandled Exception", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch { }
            // allow default crash behavior after logging
            e.Handled = false;
        }

        private void CurrentDomain_UnhandledException(object? sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                var ex = e.ExceptionObject as Exception;
                var p = Path.Combine(Path.GetTempPath(), "ytp_unhandled_exception_domain.txt");
                File.WriteAllText(p, ex?.ToString() ?? e.ExceptionObject?.ToString() ?? "Unknown error");
            }
            catch { }
        }

        private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            try
            {
                var p = Path.Combine(Path.GetTempPath(), "ytp_unobserved_task_exception.txt");
                File.WriteAllText(p, e.Exception.ToString());
            }
            catch { }
        }

    // Use StartupUri in App.xaml to create and show the main window.
    }
}
