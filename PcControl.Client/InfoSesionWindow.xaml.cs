using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation; 

namespace PcControl.Client
{
    public partial class InfoSesionWindow : Window
    {
        private bool _esCompacto = false;

        // --- CORRECCIÓN: Dimensiones sincronizadas con el nuevo XAML ---
        private const double ANCHO_NORMAL = 280;
        private const double ALTO_NORMAL = 200;

        // Dimensiones del modo widget flotante (Solo muestra el tiempo restante)
        private const double ANCHO_COMPACTO = 140;
        private const double ALTO_COMPACTO = 60;

        public InfoSesionWindow()
        {
            InitializeComponent();
            
            // 1. BLINDAJE NUCLEAR
            SeguridadWin32.BlindarVentana(this);

            this.Left = SystemParameters.PrimaryScreenWidth - this.Width - 20;
            this.Top = 20;
        }

        // Método original mantenido por seguridad y compatibilidad
        public void ActualizarDatos(string tiempo, string mensaje)
        {
            txtTiempo.Text = tiempo;
            txtMensaje.Text = mensaje;
        }

        // Usar este desde MainWindow para llenar todo el dashboard
        public void ActualizarDashboardCompleto(string tiempoRestante, string mensaje, string horaInicio, string horaFin, string tiempoComprado, string precio)
        {
            txtTiempo.Text = tiempoRestante;
            txtMensaje.Text = mensaje;
            txtHoraInicio.Text = horaInicio;
            txtHoraFin.Text = horaFin;
            txtTiempoComprado.Text = tiempoComprado;
            txtPrecio.Text = precio;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        private void BtnMinimizar_Click(object sender, RoutedEventArgs e)
        {
            AlternarModoCompacto();
        }

        private void AlternarModoCompacto()
        {
            Duration duracion = new Duration(TimeSpan.FromSeconds(0.2)); 
            
            DoubleAnimation animAncho = new DoubleAnimation { Duration = duracion, EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut } };
            DoubleAnimation animAlto = new DoubleAnimation { Duration = duracion, EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut } };

            if (!_esCompacto)
            {
                // --- ACTIVAR MODO COMPACTO ---
                animAncho.To = ANCHO_COMPACTO; 
                animAlto.To = ALTO_COMPACTO;   

                // Ocultar detalles
                PanelDetalles.Visibility = Visibility.Collapsed;
                Separador.Visibility = Visibility.Collapsed;
                txtMensaje.Visibility = Visibility.Collapsed;
                
                BtnToggle.Content = "❐"; 
                // Fondo compacto más translúcido
                BordePrincipal.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#99000000");

                _esCompacto = true;
            }
            else
            {
                // --- VOLVER A MODO NORMAL ---
                animAncho.To = ANCHO_NORMAL; 
                animAlto.To = ALTO_NORMAL;  

                // Mostrar detalles
                PanelDetalles.Visibility = Visibility.Visible;
                Separador.Visibility = Visibility.Visible;
                txtMensaje.Visibility = Visibility.Visible;
                
                BtnToggle.Content = "—"; 
                
                // --- CORRECCIÓN: Usar el mismo fondo del nuevo XAML ---
                BordePrincipal.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#F215151C");

                _esCompacto = false;
            }

            this.BeginAnimation(Window.WidthProperty, animAncho);
            this.BeginAnimation(Window.HeightProperty, animAlto);
        }
    }
}