using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation; // Necesario para animaciones

namespace PcControl.Client
{
    public partial class InfoSesionWindow : Window
    {
        private bool _esCompacto = false;

        // Dimensiones originales
        private const double ANCHO_NORMAL = 250;
        private const double ALTO_NORMAL = 80;

        // Dimensiones compactas (tipo widget)
        private const double ANCHO_COMPACTO = 120;
        private const double ALTO_COMPACTO = 45;

        public InfoSesionWindow()
        {
            InitializeComponent();
            
            // 1. BLINDAJE NUCLEAR (Mantén esto siempre)
            SeguridadWin32.BlindarVentana(this);

            // Posición inicial: Esquina superior derecha
            this.Left = SystemParameters.PrimaryScreenWidth - this.Width - 20;
            this.Top = 20;
        }

        public void ActualizarDatos(string tiempo, string mensaje)
        {
            txtTiempo.Text = tiempo;
            txtMensaje.Text = mensaje;
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
            // Configuramos la duración de la animación (0.3 segundos)
            Duration duracion = new Duration(TimeSpan.FromSeconds(0.3));
            
            // Animaciones de ancho y alto
            DoubleAnimation animAncho = new DoubleAnimation();
            DoubleAnimation animAlto = new DoubleAnimation();
            animAncho.Duration = duracion;
            animAlto.Duration = duracion;
            
            // Curva de aceleración suave (EaseInOut)
            animAncho.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut };
            animAlto.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut };

            if (!_esCompacto)
            {
                // --- ACTIVAR MODO COMPACTO ---
                animAncho.To = ANCHO_COMPACTO;
                animAlto.To = ALTO_COMPACTO;

                // Ocultar detalles visuales innecesarios
                txtMensaje.Visibility = Visibility.Collapsed;
                BtnToggle.Content = "❐"; // Icono de "Restaurar" (o un cuadrado)
                BordePrincipal.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#99000000"); // Un poco más transparente en modo mini

                _esCompacto = true;
            }
            else
            {
                // --- VOLVER A MODO NORMAL ---
                animAncho.To = ANCHO_NORMAL;
                animAlto.To = ALTO_NORMAL;

                // Mostrar detalles
                txtMensaje.Visibility = Visibility.Visible;
                BtnToggle.Content = "—"; // Icono de "Minimizar"
                BordePrincipal.Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#CC000000"); // Color original

                _esCompacto = false;
            }

            // Iniciar animaciones
            this.BeginAnimation(Window.WidthProperty, animAncho);
            this.BeginAnimation(Window.HeightProperty, animAlto);
        }
    }
}