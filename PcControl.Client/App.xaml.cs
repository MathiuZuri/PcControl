using System.IO;
using System.Windows;
using System.Windows.Threading;
using System.Threading; // Necesario para Mutex
using System.Diagnostics; // Necesario para Process
using System.Reflection; // Necesario para Assembly

namespace PcControl.Client
{
    public partial class App : Application
    {
        // 1. MUTEX: Previene que se abran dos clientes al mismo tiempo
        private static Mutex? _mutex = null;

        public App()
        {
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            const string appName = "CyberControlCliente_InstanciaUnica";
            bool createdNew;

            _mutex = new Mutex(true, appName, out createdNew);

            if (!createdNew)
            {
                // Si ya hay una copia corriendo, nos suicidamos en silencio
                Application.Current.Shutdown();
                return;
            }

            base.OnStartup(e);

            // Crear la Ventana Fantasma (Invisible)
            Window ventanaFantasma = new Window
            {
                Width = 0,
                Height = 0,
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false,
                ShowActivated = false,
                Visibility = Visibility.Hidden
            };
            ventanaFantasma.Show(); 

            // Crear TU ventana principal
            MainWindow main = new MainWindow();
            main.Owner = ventanaFantasma;
            main.ShowInTaskbar = false;
            main.Show();
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogCrasheo("UI Error", e.Exception);
            RevivirAplicacion(); // Si hay error grave, revivimos
            e.Handled = true; 
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            LogCrasheo("Fatal Domain Error", e.ExceptionObject as Exception);
            RevivirAplicacion();
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
            catch { }
        }

        // 2. MODO INMORTAL: Lanza un clon y mata a la instancia defectuosa
        private void RevivirAplicacion()
        {
            try
            {
                // IMPORTANTE: Si implementas el archivo SeguridadSistema.cs de la clase anterior, 
                // descomenta esta línea para evitar pantallazo azul (BSOD) al reiniciar:
                // SeguridadSistema.EstablecerProcesoCritico(false);
                
                string miExe = Assembly.GetExecutingAssembly().Location.Replace(".dll", ".exe");
                Process.Start(miExe);
                Process.GetCurrentProcess().Kill();
            }
            catch { }
        }
    }
}