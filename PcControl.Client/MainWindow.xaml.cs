using System.ComponentModel;
using System.Diagnostics;
using System.Drawing; // NuGet: System.Drawing.Common
using System.IO;
using System.Net;
using System.Net.Http;
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
using System.Windows.Controls;

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
        
        private List<string> _playlistArchivos = new List<string>(); // Rutas locales de archivos
        private int _indicePlaylist = 0;
        private DispatcherTimer? _timerSlideshow;
        private string _carpetaCache = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CacheMedia");

        public MainWindow()
        {
            InitializeComponent();
            if (!Directory.Exists(_carpetaCache)) Directory.CreateDirectory(_carpetaCache);
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
        
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CargarPlaylistLocal();
            ReproducirSiguiente();
        }
        
        // LÓGICA DE REPRODUCCIÓN MULTIMEDIA (NUEVO)

        
        private void CargarPlaylistLocal()
        {
            try
            {
                if (!Directory.Exists(_carpetaCache)) Directory.CreateDirectory(_carpetaCache);

                var extensionesValidas = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    ".jpg", ".jpeg", ".png", ".bmp", ".webp",  // Imágenes
                    ".mp4", ".avi", ".wmv", ".mov", ".mkv", ".gif" // Videos y GIFs
                };

                _playlistArchivos = Directory.GetFiles(_carpetaCache)
                    .Where(f => extensionesValidas.Contains(Path.GetExtension(f)))
                    .OrderBy(f => f)
                    .ToList();
            }
            catch { }
        }

        private void ReproducirSiguiente()
        {
            // Validaciones de seguridad
            if (_playlistArchivos.Count == 0) return;
            
            // Avanzar índice circularmente
            if (_indicePlaylist >= _playlistArchivos.Count) _indicePlaylist = 0;
            string archivo = _playlistArchivos[_indicePlaylist];
            _indicePlaylist++;

            // Verificar que el archivo realmente exista antes de intentar tocarlo
            if (!File.Exists(archivo)) 
            {
                ReproducirSiguiente(); // Si lo borraron, salta al siguiente
                return;
            }

            string ext = Path.GetExtension(archivo).ToLower();

            // TRUCO: Los GIFs los mandamos al reproductor de video para que se muevan
            if (ext == ".mp4" || ext == ".avi" || ext == ".wmv" || ext == ".mov" || ext == ".gif")
            {
                ReproducirVideo(archivo);
            }
            else
            {
                ReproducirImagen(archivo);
            }
        }

        private void ReproducirImagen(string ruta)
        {
            // 1. Limpiar Video
            VidFondo.Stop();
            VidFondo.Source = null;
            VidFondo.Visibility = Visibility.Collapsed;

            try
            {
                // 2. Cargar Imagen SIN BLOQUEAR EL ARCHIVO (CacheOption.OnLoad)
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad; // <--- CLAVE PARA EVITAR ERROR DE ACCESO
                bitmap.UriSource = new Uri(ruta);
                bitmap.EndInit();
                bitmap.Freeze(); // Hacerla inmutable para mejor rendimiento

                ImgFondo.Source = bitmap;
                ImgFondo.Visibility = Visibility.Visible;

                // 3. Configurar Timer para pasar a la siguiente
                if (_timerSlideshow != null) _timerSlideshow.Stop();
                
                _timerSlideshow = new DispatcherTimer();
                _timerSlideshow.Interval = TimeSpan.FromSeconds(10); // 10 segundos por foto
                _timerSlideshow.Tick += (s, e) => 
                { 
                    _timerSlideshow.Stop(); 
                    ReproducirSiguiente(); 
                };
                _timerSlideshow.Start();
            }
            catch 
            { 
                ReproducirSiguiente(); // Si falla, siguiente
            }
        }

        private void ReproducirVideo(string ruta)
        {
            // 1. Detener Timer de imágenes
            if (_timerSlideshow != null) _timerSlideshow.Stop();
            
            // 2. Limpiar Imagen
            ImgFondo.Source = null;
            ImgFondo.Visibility = Visibility.Collapsed;

            // 3. Cargar Video
            VidFondo.Source = new Uri(ruta);
            VidFondo.Visibility = Visibility.Visible;
            VidFondo.Play();
        }

        // Evento que salta al siguiente cuando el video/gif termina
        private void VidFondo_MediaEnded(object sender, RoutedEventArgs e)
        {
            // Pequeño delay para asegurar que el motor de video se liberó
            VidFondo.Stop(); 
            ReproducirSiguiente();
        }

        // MÉTODO PARA DESCARGAR DESDE SIGNALR
        private async Task DescargarNuevosFondos(List<string> urls)
        {
            try
            {
                if (!Directory.Exists(_carpetaCache)) Directory.CreateDirectory(_carpetaCache);

                // PASO CRÍTICO: Detener antes de tocar archivos
                // Esto libera los "candados" sobre las imágenes y videos
                await Dispatcher.InvokeAsync(() => 
                {
                    if (_timerSlideshow != null) _timerSlideshow.Stop();
                    VidFondo.Stop();
                    VidFondo.Source = null;
                    ImgFondo.Source = null;
                    // Forzar limpieza de memoria para soltar archivos
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                });

                using (HttpClient client = new HttpClient())
                {
                    // 1. Descargar nuevos
                    foreach (var url in urls)
                    {
                        var nombreArchivo = Path.GetFileName(url);
                        var rutaDestino = Path.Combine(_carpetaCache, nombreArchivo);

                        if (!File.Exists(rutaDestino) || new FileInfo(rutaDestino).Length == 0)
                        {
                            var datos = await client.GetByteArrayAsync(url);
                            await File.WriteAllBytesAsync(rutaDestino, datos);
                        }
                    }

                    // 2. Borrar viejos
                    var nombresValidos = urls.Select(u => Path.GetFileName(u)).ToHashSet();
                    var archivosLocales = Directory.GetFiles(_carpetaCache);

                    foreach (var archivoLocal in archivosLocales)
                    {
                        if (!nombresValidos.Contains(Path.GetFileName(archivoLocal)))
                        {
                            try { File.Delete(archivoLocal); } catch { /* Ignorar si falla por ahora */ }
                        }
                    }
                }
                
                // 3. Reiniciar Playlist
                await Dispatcher.InvokeAsync(() => 
                {
                    CargarPlaylistLocal();
                    if (_playlistArchivos.Count > 0)
                    {
                        _indicePlaylist = 0;
                        ReproducirSiguiente();
                    }
                });
            }
            catch (Exception ex)
            {
                File.AppendAllText("error_media.txt", $"{DateTime.Now}: {ex.Message}\n");
            }
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
            // 1. Lógica de Autodescubrimiento (UDP)
            bool conexionExitosa = await IntentarConectar(_urlServidor);
            
            if (!conexionExitosa)
            {
                txtEstadoConexion.Text = "Buscando servidor en la red...";
                await Task.Delay(500); 

                string nuevaIp = BuscarServidorUdp(); 

                if (!string.IsNullOrEmpty(nuevaIp))
                {
                    _urlServidor = $"http://{nuevaIp}:5249/ciberhub";
                    GuardarConfigEnTxt(_nombrePc, _urlServidor);
                    await IntentarConectar(_urlServidor);
                }
                else
                {
                    txtEstadoConexion.Text = "No se encontró el servidor ";
                    return; // Salir si no hay servidor
                }
            }
            
            // 2. CREAR LA CONEXIÓN PRINCIPAL (Importante: Crear antes de usar)
            _connection = new HubConnectionBuilder()
                .WithUrl(_urlServidor)
                .WithAutomaticReconnect()
                .Build();

            // 3. REGISTRAR TODOS LOS EVENTOS (Listeners)
            
            // Evento: Configuración Admin
            _connection.On<string, string, string>("RecibirConfiguracion", (pass, keyStr, modStr) =>
            {
                _passwordAdmin = pass;
                Dispatcher.Invoke(() => { try { if (Enum.TryParse(keyStr, true, out Key k)) _teclaConsola = k; if (Enum.TryParse(modStr, true, out ModifierKeys m)) _modificadorConsola = m; } catch { } });
            });

            // Evento: Comandos (Bloquear, Desbloquear, Apagar...)
            _connection.On<string, int?, string>("RecibirOrden", (accion, tiempo, parametro) =>
            {
                Dispatcher.Invoke(() => ProcesarOrden(accion, tiempo, parametro));
            });

            // Evento: Mensajes de texto
            _connection.On<string>("RecibirMensaje", (mensaje) =>
            {
                Dispatcher.Invoke(() => 
                {
                    var msgWindow = new MensajeWindow(mensaje);
                    msgWindow.Topmost = true;
                    msgWindow.Show();
                    msgWindow.Activate();
                });
            });
            
            // Evento: Cambio de Nombre
            _connection.On<string>("CambiarNombreIdentity", (nuevoNombre) =>
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
                    
                    var aviso = new MensajeWindow($"Nombre actualizado a: {_nombrePc}");
                    aviso.Topmost = true;
                    aviso.Show();
                });
            });

            // Evento: Actualizar Fondos (NUEVO)
            _connection.On<List<string>>("ActualizarFondos", (urls) =>
            {
                Task.Run(() => DescargarNuevosFondos(urls));
            });

            // Evento: Cierre de conexión
            _connection.Closed += async (error) => await Dispatcher.InvokeAsync(() => ProcesarOrden("Bloquear", 0, "Conexión Perdida"));

            // 4. INICIAR CONEXIÓN
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
                nuevaConexion.On<List<string>>("ActualizarFondos", (urls) => { Task.Run(() => DescargarNuevosFondos(urls)); });
                
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
                    VidFondo.Stop(); 
                    if (_timerSlideshow != null) _timerSlideshow.Stop();
                    
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
                    ReproducirSiguiente();
                    
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