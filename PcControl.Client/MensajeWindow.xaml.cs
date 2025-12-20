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
            txtContenido.Text = mensaje;
            
            // Posicionar en el centro de la pantalla
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            // Timer para cerrar en 5 segundos
            _autoCloseTimer = new DispatcherTimer();
            _autoCloseTimer.Interval = TimeSpan.FromSeconds(1);
            _autoCloseTimer.Tick += Timer_Tick;
            _autoCloseTimer.Start();
        }

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
            this.DragMove(); // Permite mover la ventana
        }
    }
}