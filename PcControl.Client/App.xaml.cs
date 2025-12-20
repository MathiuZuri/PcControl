using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace PcControl.Client
{
    public partial class App : Application
    {
        public App()
        {
            // 1. Capturar errores del hilo principal (UI)
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            
            // 2. Capturar errores de otros hilos (Background Tasks)
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            
            // 3. Capturar errores de tareas asíncronas perdidas
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogCrasheo("UI Error", e.Exception);
            e.Handled = true; // Intentar que la app no se cierre si es posible
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            LogCrasheo("Fatal Domain Error", e.ExceptionObject as Exception);
        }

        private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            LogCrasheo("Task Error", e.Exception);
            e.SetObserved();
        }

        private void LogCrasheo(string origen, Exception? ex)
        {
            try
            {
                string ruta = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash_log.txt");
                string mensaje = $"[{DateTime.Now}] ({origen}): {ex?.Message}\nSTACK: {ex?.StackTrace}\n--------------------------\n";
                File.AppendAllText(ruta, mensaje);
            }
            catch { /* Si falla el log, no podemos hacer nada más */ }
        }
    }
}