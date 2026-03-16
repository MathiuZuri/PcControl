using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace PcControl.Client
{
    public static class SeguridadWin32
    {
        // Constantes de Windows
        private const int GWL_STYLE = -16;
        private const int WS_SYSMENU = 0x80000; // Botón cerrar, Alt+F4
        
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080; // Ocultar de Alt+Tab y Barra tareas

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        /// <summary>
        /// Aplica el "Modo Nuclear": Sin barra de tareas, sin Alt+Tab, sin Alt+F4.
        /// </summary>
        public static void BlindarVentana(Window window)
        {
            // Nos aseguramos de que la ventana tenga un Handle (esté inicializada)
            if (PresentationSource.FromVisual(window) is HwndSource source)
            {
                AplicarEstilos(source.Handle);
            }
            else
            {
                // Si aún no tiene Handle, esperamos a que lo tenga
                window.SourceInitialized += (s, e) =>
                {
                    var handle = new WindowInteropHelper(window).Handle;
                    AplicarEstilos(handle);
                };
            }
        }

        private static void AplicarEstilos(IntPtr handle)
        {
            // 1. Quitar de la Barra de Tareas y Alt+Tab (ToolWindow)
            int exStyle = GetWindowLong(handle, GWL_EXSTYLE);
            SetWindowLong(handle, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW);

            // 2. Quitar menú de sistema (Impide Alt+F4 y Clic derecho en barra título)
            int style = GetWindowLong(handle, GWL_STYLE);
            SetWindowLong(handle, GWL_STYLE, style & ~WS_SYSMENU);
        }
    }
}