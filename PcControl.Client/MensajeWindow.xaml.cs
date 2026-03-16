using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace PcControl.Client
{
    public partial class MensajeWindow : Window
    {
        private DispatcherTimer _autoCloseTimer;
        private int _segundosVida = 5;

        public MensajeWindow(string mensaje)
        {
            InitializeComponent();
            
            // 1. APLICAR SOLUCIÓN NUCLEAR (Indestructible por 5 segundos)
            SeguridadWin32.BlindarVentana(this);

            txtContenido.Text = mensaje;
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            // Timer para cerrar en 5 segundos
            _autoCloseTimer = new DispatcherTimer();
            _autoCloseTimer.Interval = TimeSpan.FromSeconds(1);
            _autoCloseTimer.Tick += Timer_Tick;
            _autoCloseTimer.Start();
        }

        // ... (El resto de tu código sigue igual: Timer_Tick, BtnOk_Click, DragMove) ...
        private void Timer_Tick(object? sender, EventArgs e)
        {
            _segundosVida--;
            txtCuentaAtras.Text = $"{_segundosVida}s";

            if (_segundosVida <= 0)
            {
                _autoCloseTimer.Stop();
                this.Close();
            }
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove(); 
        }
    }
}