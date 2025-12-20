using System.ComponentModel;
using System.Diagnostics;
using System.Drawing; // NuGet: System.Drawing.Common
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.AspNetCore.SignalR.Client;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using PcControl.Client.data;

namespace PcControl.Client
{
    public partial class MainWindow : Window
    {
        private HubConnection? _connection;
        private string _nombrePc = "DESCONOCIDO"; 
        private string _urlServidor = "http://localhost:5249/ciberhub";
        
        private InfoSesionWindow? _infoWindow;
        private DispatcherTimer? _timerLocal;
        private int _minutosRestantes = 0;
        
        private bool _cierreAutorizado = false;
        private string _passwordAdmin = "123456789a"; 

        private Key _teclaConsola = Key.Q;
        private ModifierKeys _modificadorConsola = ModifierKeys.Control;
        
        private DispatcherTimer? _latidoTimer;
        private DispatcherTimer? _streamTimer;
        private bool _isStreaming = false;
        private bool _inputCongelado = false; 

        // IMPORTANTE: Variable para saber si estamos en sesión activa
        private bool _sesionActiva = false;
        
        private bool _esApagadoDeWindows = false;
        
        private string _ipCandidata = "";

        public MainWindow()
        {
            InitializeComponent();
            CargarConfiguracion(); 
            ConfigurarSignalR();
            InicializarTimer();
            
            // --- SEGURIDAD: WATCHDOG PERMANENTE ---
            // Revisa cada 500ms que no abran el administrador de tareas
            DispatcherTimer watchdog = new DispatcherTimer();
            watchdog.Interval = TimeSpan.FromMilliseconds(500); 
            watchdog.Tick += Watchdog_Tick;
            watchdog.Start();

            // Bloquear al iniciar
            BloqueoTeclado.Bloquear();
            
            try 
            { 
                if (File.Exists("apagar_guardian.tmp")) 
                    File.Delete("apagar_guardian.tmp"); 
            } 
            catch { }

            CargarConfiguracion();
            
            SystemEvents.SessionEnding += (s, e) => 
            {
                _esApagadoDeWindows = true;
                // NO creamos el archivo "apagar_guardian.tmp" 
                // para que cuando la PC vuelva a prender, el guardián proteja de nuevo.
            };

            BloqueoTeclado.Bloquear();
        }
        
        
        
        // --- AQUÍ ESTÁ LA SEGURIDAD ANTI-CIERRE ---
        private void Watchdog_Tick(object? sender, EventArgs e)
        {

            // 2. Comportamiento según estado
            if (this.Visibility == Visibility.Visible && GridAdminConsole.Visibility == Visibility.Collapsed)
            {
                // MODO BLOQUEADO (Pantalla Negra)
                this.Topmost = true; // Forzar estar encima
                this.Activate();     // Robar foco
            }
            else
            {
                // MODO DESBLOQUEADO (Usuario usando la PC)
                this.Topmost = false; // Dejar que use otras ventanas encima
                
                // Truco: Si minimizan la ventana de tiempo, la traemos de vuelta si es crítico
                // (Opcional)
            }
        }
        

        // --- EVITAR CIERRE CON ALT+F4 O BOTÓN X ---
        protected override void OnClosing(CancelEventArgs e)
        {
            // Permitimos cerrar SI:
            // 1. Fue autorizado por Admin/Comando Remoto (_cierreAutorizado)
            // 2. O SI Windows se está apagando (_esApagadoDeWindows)
            if (!_cierreAutorizado && !_esApagadoDeWindows) 
            {
                e.Cancel = true; // Bloqueamos el cierre manual (Alt+F4)
            }
            base.OnClosing(e);
        }
        
        // (CargarConfiguracion, Window_KeyDown, VerificarPassword IGUALES)

        private void CargarConfiguracion()
        {
            string path = "config.txt";
            bool necesitaGuardar = false;

            // 1. Cargar lo que existe
            if (File.Exists(path))
            {
                var lineas = File.ReadAllLines(path);
                foreach (var linea in lineas)
                {
                    if (linea.StartsWith("NOMBRE=")) _nombrePc = linea.Replace("NOMBRE=", "").Trim();
                    if (linea.StartsWith("IP=")) 
                    {
                        string ip = linea.Replace("IP=", "").Trim();
                        // Solo usamos la IP si no es la genérica localhost
                        if (!string.IsNullOrWhiteSpace(ip) && ip != "127.0.0.1")
                        {
                            _urlServidor = $"http://{ip}:5249/ciberhub";
                        }
                        else
                        {
                            _urlServidor = ""; // Forzar búsqueda
                        }
                    }
                }
            }

            // 2. SI NO TENEMOS IP VÁLIDA, BUSCAMOS EL SERVIDOR (UDP)
            if (string.IsNullOrEmpty(_urlServidor) || _urlServidor.Contains("localhost"))
            {
                // Mostramos ventana temporal o log
                // "Buscando servidor..."
                string ipServidorEncontrada = BuscarServidorUdp();
                
                if (!string.IsNullOrEmpty(ipServidorEncontrada))
                {
                    _urlServidor = $"http://{ipServidorEncontrada}:5249/ciberhub";
                    necesitaGuardar = true;
                }
                else
                {
                    // Fallback
                    _urlServidor = "http://127.0.0.1:5249/ciberhub";
                }
            }

            // 3. SI NO TENEMOS NOMBRE, USAMOS PC-TEST
            if (_nombrePc == "DESCONOCIDO" || string.IsNullOrEmpty(_nombrePc))
            {
                _nombrePc = "PC-TEST";
                necesitaGuardar = true;
            }

            // 4. GUARDAR CAMBIOS SI HUBO AUTO-CONFIGURACIÓN
            if (necesitaGuardar)
            {
                GuardarConfigEnTxt(_nombrePc, _urlServidor);
            }

            this.Title = _nombrePc;
            txtEstadoConexion.Text = $"Soy: {_nombrePc}";
        }
        
        private string BuscarServidorUdp()
        {
            try
            {
                using (var udp = new UdpClient())
                {
                    udp.Client.Bind(new IPEndPoint(IPAddress.Any, 8888));
                    udp.Client.ReceiveTimeout = 5000; // Esperar máximo 5 segundos

                    var remoteEp = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = udp.Receive(ref remoteEp);
                    string mensaje = Encoding.UTF8.GetString(data);

                    if (mensaje.StartsWith("CIBER_SERVER|"))
                    {
                        return mensaje.Split('|')[1];
                    }
                }
            }
            catch 
            { 
                return ""; // No se encontró
            }
            return "";
        }
        
        private void GuardarConfigEnTxt(string nombre, string url)
        {
            // Extraer solo la IP de la URL para guardar limpio
            string ip = "127.0.0.1";
            try 
            { 
                Uri u = new Uri(url); 
                ip = u.Host; 
            } catch { }

            string contenido = $"NOMBRE={nombre}\nIP={ip}";
            File.WriteAllText("config.txt", contenido);
        }
        
        private void Window_KeyDown(object sender, KeyEventArgs e) { bool modificadorPresionado = (Keyboard.Modifiers & _modificadorConsola) == _modificadorConsola; if (modificadorPresionado && e.Key == _teclaConsola) { GridAdminConsole.Visibility = Visibility.Visible; txtPasswordAdmin.Focus(); } }
        private void VerificarPassword()
        {
            if (txtPasswordAdmin.Password == _passwordAdmin)
            {
                _cierreAutorizado = true;

                try 
                { 
                    // Creamos un archivo vacío y lo cerramos inmediatamente
                    File.Create("apagar_guardian.tmp").Close(); 
                } 
                catch { }

                BloqueoTeclado.Desbloquear();
                Application.Current.Shutdown();
            }
            else
            {
                MessageBox.Show("Contraseña Incorrecta", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                txtPasswordAdmin.Password = "";
            }
        }
        private void BtnAdminUnlock_Click(object sender, RoutedEventArgs e) => VerificarPassword();
        private void BtnCancelar_Click(object sender, RoutedEventArgs e) { GridAdminConsole.Visibility = Visibility.Collapsed; txtPasswordAdmin.Password = ""; }
        private void txtPasswordAdmin_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) VerificarPassword(); }

        // --- LÓGICA SIGNALR ---
        private async void ConfigurarSignalR()
        {
            bool conexionExitosa = await IntentarConectar(_urlServidor);
            
            // 2. Si falló (porque la IP del servidor cambió o no existe), BUSCAMOS AUTOMÁTICAMENTE
            if (!conexionExitosa)
            {
                txtEstadoConexion.Text = "Buscando servidor en la red...";
                await Task.Delay(500); // Dar tiempo a la UI

                string nuevaIp = BuscarServidorUdp(); // Escuchamos el "grito" del servidor

                if (!string.IsNullOrEmpty(nuevaIp))
                {
                    _urlServidor = $"http://{nuevaIp}:5249/ciberhub";
                    
                    // Guardamos la nueva IP en el txt para la próxima vez
                    GuardarConfigEnTxt(_nombrePc, _urlServidor);
                    
                    // Intentamos conectar de nuevo con la IP fresca
                    await IntentarConectar(_urlServidor);
                }
                else
                {
                    txtEstadoConexion.Text = "No se encontró el servidor ";
                }
            }
            
            _connection = new HubConnectionBuilder()
                .WithUrl(_urlServidor)
                .WithAutomaticReconnect()
                .Build();

            _connection.On<string, string, string>("RecibirConfiguracion", (pass, keyStr, modStr) =>
            {
                _passwordAdmin = pass;
                Dispatcher.Invoke(() => { try { if (Enum.TryParse(keyStr, true, out Key k)) _teclaConsola = k; if (Enum.TryParse(modStr, true, out ModifierKeys m)) _modificadorConsola = m; } catch { } });
            });

            _connection.On<string, int?, string>("RecibirOrden", (accion, tiempo, parametro) =>
            {
                Dispatcher.Invoke(() => ProcesarOrden(accion, tiempo, parametro));
            });

            _connection.On<string>("RecibirMensaje", (mensaje) =>
            {
                Dispatcher.Invoke(() => 
                {
                    var msgWindow = new MensajeWindow(mensaje);
                    msgWindow.Topmost = true; // <--- ESTO FALTABA PARA QUE SE VEA SIEMPRE
                    msgWindow.Show();
                    msgWindow.Activate();
                });
            });
            
            _connection.On<string>("CambiarNombreIdentity", (nuevoNombre) =>
            {
                Dispatcher.Invoke(async () => 
                {
                    // 1. Actualizar memoria
                    _nombrePc = nuevoNombre;
                    this.Title = _nombrePc;
                    txtEstadoConexion.Text = $"Soy: {_nombrePc} (Actualizado)";

                    // 2. Actualizar TXT
                    GuardarConfigEnTxt(_nombrePc, _urlServidor);

                    // 3. REINICIAR CONEXIÓN SIGNALR
                    // Es vital desconectar y reconectar para unirse al grupo del NUEVO nombre
                    await _connection.StopAsync();
                    await Task.Delay(1000);
                    
                    // Al reconectar, llamará a RegistrarPc con el NUEVO nombre
                    await _connection.StartAsync();
                    await _connection.InvokeAsync("RegistrarPc", _nombrePc);
                    
                    // Mostrar aviso
                    var aviso = new MensajeWindow($"Nombre actualizado a: {_nombrePc}");
                    aviso.Topmost = true;
                    aviso.Show();
                });
            });

            _connection.Closed += async (error) => await Dispatcher.InvokeAsync(() => ProcesarOrden("Bloquear", 0, "Conexión Perdida"));

            try
            {
                await _connection.StartAsync();
                txtEstadoConexion.Text = $"Soy: {_nombrePc} | Conectado ✅";
                await _connection.InvokeAsync("RegistrarPc", _nombrePc);
                IniciarLatidos();
            }
            catch (Exception ex) { txtEstadoConexion.Text = $"Error: {ex.Message}"; }
        }

        private async Task<bool> IntentarConectar(string url)
        {
            try
            {
                txtEstadoConexion.Text = $"Conectando a {url}...";

                var nuevaConexion = new HubConnectionBuilder()
                    .WithUrl(url)
                    .WithAutomaticReconnect() // Reintenta si se cae momentáneamente
                    .Build();

                // DEFINIR EVENTOS (Los mismos de siempre)
                nuevaConexion.On<string, string, string>("RecibirConfiguracion", (pass, keyStr, modStr) =>
                {
                    _passwordAdmin = pass;
                    Dispatcher.Invoke(() => { try { if (Enum.TryParse(keyStr, true, out Key k)) _teclaConsola = k; if (Enum.TryParse(modStr, true, out ModifierKeys m)) _modificadorConsola = m; } catch { } });
                });

                nuevaConexion.On<string, int?, string>("RecibirOrden", (accion, tiempo, parametro) =>
                {
                    Dispatcher.Invoke(() => ProcesarOrden(accion, tiempo, parametro));
                });

                nuevaConexion.On<string>("RecibirMensaje", (mensaje) =>
                {
                    Dispatcher.Invoke(() => 
                    {
                        var msgWindow = new MensajeWindow(mensaje);
                        msgWindow.Topmost = true;
                        msgWindow.Show();
                    });
                });
            
                nuevaConexion.On<string>("CambiarNombreIdentity", (nuevoNombre) =>
                {
                    Dispatcher.Invoke(async () => 
                    {
                        _nombrePc = nuevoNombre;
                        this.Title = _nombrePc;
                        txtEstadoConexion.Text = $"Soy: {_nombrePc} (Actualizado)";
                        GuardarConfigEnTxt(_nombrePc, _urlServidor);
                        
                        await _connection.StopAsync();
                        await Task.Delay(1000);
                        await _connection.StartAsync();
                        await _connection.InvokeAsync("RegistrarPc", _nombrePc);
                    });
                });

                nuevaConexion.On<string>("StreamDetenido", (pc) => { /* Cliente no hace nada con esto, pero evita error */ });

                // Intentamos iniciar con un timeout corto para no congelar la app si la IP está mal
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3)); 
                await nuevaConexion.StartAsync(cts.Token);

                // ¡ÉXITO!
                _connection = nuevaConexion;
                txtEstadoConexion.Text = $"Soy: {_nombrePc} | Conectado ✅";
                
                // Nos registramos
                await _connection.InvokeAsync("RegistrarPc", _nombrePc);
                
                // Setup de cierre
                _connection.Closed += async (error) => await Dispatcher.InvokeAsync(() => ProcesarOrden("Bloquear", 0, "Conexión Perdida"));
                
                IniciarLatidos();
                return true;
            }
            catch
            {
                return false; // Falló la conexión
            }
        }
        
        private void IniciarLatidos()
        {
            _latidoTimer = new DispatcherTimer();
            _latidoTimer.Interval = TimeSpan.FromSeconds(2);
            _latidoTimer.Tick += async (s, e) => 
            {
                if (_connection.State == HubConnectionState.Connected)
                {
                    await _connection.InvokeAsync("ReportarEstado", _nombrePc, ObtenerMac(), ObtenerIp(), _inputCongelado);
                }
            };
            _latidoTimer.Start();
        }

        // --- SOLUCIÓN AL CRASH DE VIDEO (CON LOG) ---
        private async void EnviarFrameStreaming()
        {
            if (!_isStreaming || _connection.State != HubConnectionState.Connected) return;
    
            try 
            {
                int ancho = (int)SystemParameters.PrimaryScreenWidth;
                int alto = (int)SystemParameters.PrimaryScreenHeight;

                string? base64 = await Task.Run(() => 
                {
                    // Bloque TRY interno para capturar errores de GDI+ específicamente
                    try 
                    {
                        return TomarCapturaPantalla(ancho, alto);
                    }
                    catch (Exception ex)
                    {
                        // Si falla la captura (ej: pantalla bloqueada por UAC), devolvemos null
                        // y escribimos en el log para saberlo
                        File.AppendAllText("crash_log.txt", $"[{DateTime.Now}] Error Captura: {ex.Message}\n");
                        return null;
                    }
                });
        
                if (!string.IsNullOrEmpty(base64))
                    await _connection.InvokeAsync("EnviarCaptura", _nombrePc, base64);
            }
            catch (Exception ex) 
            { 
                // Error de SignalR o Task
                File.AppendAllText("crash_log.txt", $"[{DateTime.Now}] Error Envio: {ex.Message}\n");
            }
        }
        
        private void ProcesarOrden(string accion, int? tiempo, string parametro)
        {
            switch (accion)
            {
                case "Desbloquear":
                    this.Hide(); // Ocultamos pantalla negra
                    _sesionActiva = true; // MARCAMOS SESIÓN ACTIVA
                    BloqueoTeclado.Desbloquear(); 
                    _inputCongelado = false; 
                    BloqueoInputTotal(false); 
                    
                    _minutosRestantes = tiempo ?? 0;
                    if (_infoWindow == null) _infoWindow = new InfoSesionWindow();
                    _infoWindow.Show();
                    _infoWindow.ActualizarDatos(_minutosRestantes > 0 ? $"{_minutosRestantes} min" : "Libre", parametro);
                    if (_minutosRestantes > 0) _timerLocal.Start();
                    break;

                case "Bloquear":
                    _timerLocal.Stop();
                    _sesionActiva = false; // FIN DE SESIÓN
                    if (_infoWindow != null) { _infoWindow.Hide(); }
                    
                    BloqueoTeclado.Bloquear();
                    _inputCongelado = false; 
                    BloqueoInputTotal(false);

                    txtMensaje.Text = parametro;
                    GridAdminConsole.Visibility = Visibility.Collapsed;
                    
                    this.Show(); // Mostramos pantalla negra
                    this.WindowState = WindowState.Maximized;
                    this.Topmost = true; 
                    this.Activate();
                    break;

                case "Apagar":
                    // AVISAR AL GUARDIÁN
                    try { File.Create("apagar_guardian.tmp").Close(); } catch { }
    
                    _cierreAutorizado = true;
                    BloqueoTeclado.Desbloquear();
                    Process.Start("shutdown", "/s /t 0");
                    Application.Current.Shutdown();
                    break;
                
                case "Reiniciar":
                    try { File.Create("apagar_guardian.tmp").Close(); } catch { }
                    
                    BloqueoTeclado.Desbloquear();
                    _cierreAutorizado = true;
                    Process.Start("shutdown", "/r /t 0");
                    Application.Current.Shutdown();
                    break;
                
                case "CongelarInput":
                    _inputCongelado = true; 
                    BloqueoInputTotal(true);
                    _timerLocal?.Stop(); 
                    if (_infoWindow != null) _infoWindow.ActualizarDatos($"{_minutosRestantes} min", "SISTEMA PAUSADO");
                    break;

                case "DescongelarInput":
                    _inputCongelado = false;
                    BloqueoInputTotal(false);
                    _timerLocal?.Start(); 
                    if (_infoWindow != null) _infoWindow.ActualizarDatos($"{_minutosRestantes} min", "Tiempo corriendo...");
                    break;

                case "Captura": 
                    // ... (Igual)
                    break;

                case "EjecutarCliente":
                    // Restaura la ventana si estaba minimizada
                    if (this.WindowState == WindowState.Minimized) 
                        this.WindowState = WindowState.Normal;
                    
                    this.Show();
                    this.Activate(); 
                    this.Topmost = true;  // La subimos
                    this.Topmost = false; // La soltamos para que no moleste
                    this.Focus();
                    break;

                case "StreamStart":
                    _isStreaming = true;
                    if (_streamTimer == null)
                    {
                        _streamTimer = new DispatcherTimer();
                        _streamTimer.Interval = TimeSpan.FromMilliseconds(500); // 2 FPS
                        _streamTimer.Tick += (s, e) => EnviarFrameStreaming();
                    }
                    _streamTimer.Start();
                    break;

                case "StreamStop":
                    // 1. Apagamos la bandera
                    _isStreaming = false;
                    
                    // 2. Detenemos el reloj de fotos
                    if (_streamTimer != null)
                    {
                        _streamTimer.Stop();
                        _streamTimer = null; // Lo matamos para liberar memoria
                    }
                    
                    // 3. Forzamos recolección de basura (Opcional, limpia la RAM de las fotos)
                    GC.Collect();
                    break;
            }
        }

        // LIMPIEZA
        protected override void OnClosed(EventArgs e)
        {
            BloqueoTeclado.Desbloquear(); 
            BloqueoInputTotal(false);
            base.OnClosed(e);
        }

        private void InicializarTimer()
        {
            _timerLocal = new DispatcherTimer();
            _timerLocal.Interval = TimeSpan.FromMinutes(1);
            _timerLocal.Tick += (s, e) =>
            {
                if (_minutosRestantes > 0)
                {
                    _minutosRestantes--;
                    if (_minutosRestantes == 2) { var av = new MensajeWindow("2 min restantes"); av.Topmost = true; av.Show(); }
                    if (_infoWindow != null) _infoWindow.ActualizarDatos($"{_minutosRestantes} min", "Tiempo corriendo...");
                    if (_minutosRestantes <= 0) { _timerLocal.Stop(); ProcesarOrden("Bloquear", 0, "Tiempo Agotado"); }
                }
            };
        }

        // --- HELPERS ---
        private string ObtenerMac()
        {
            var mac = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up && nic.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
                .Select(nic => nic.GetPhysicalAddress().ToString())
                .FirstOrDefault();
            return !string.IsNullOrEmpty(mac) ? string.Join(":", Enumerable.Range(0, mac.Length / 2).Select(i => mac.Substring(i * 2, 2))) : "UNKNOWN";
        }
        
        private string ObtenerIp()
        {
             var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
             foreach (var ip in host.AddressList) { if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) return ip.ToString(); }
             return "127.0.0.1";
        }

        // MÉTODO DE CAPTURA ROBUSTO
        private string TomarCapturaPantalla(int width, int height)
        {
            // Usamos Bitmap(width, height) directamente
            using (var bitmap = new Bitmap(width, height))
            {
                using (var g = Graphics.FromImage(bitmap))
                {
                    // Si la pantalla está bloqueada (Win+L) o UAC, esto lanzará error
                    g.CopyFromScreen(0, 0, 0, 0, bitmap.Size);
                }
                using (var ms = new MemoryStream())
                {
                    bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                    return Convert.ToBase64String(ms.ToArray());
                }
            }
        }

        [DllImport("user32.dll")] static extern bool BlockInput(bool fBlockIt);
        private void BloqueoInputTotal(bool bloquear) { try { BlockInput(bloquear); } catch { } }
    }
    
    
}