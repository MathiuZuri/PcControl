using System.Windows;
using System.Windows.Input;

namespace PcControl.Client
{
    public partial class InfoSesionWindow : Window
    {
        public InfoSesionWindow()
        {
            InitializeComponent();
            // Posición inicial: Arriba a la derecha
            this.Left = SystemParameters.PrimaryScreenWidth - this.Width - 20;
            this.Top = 20;
        }

        public void ActualizarDatos(string tiempo, string mensaje)
        {
            txtTiempo.Text = tiempo;
            txtMensaje.Text = mensaje;
        }

        // Permite arrastrar la ventana
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        // Minimiza la ventana a la barra de tareas
        private void BtnMinimizar_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }
    }
}