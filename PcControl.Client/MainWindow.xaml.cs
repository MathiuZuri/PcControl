using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing; 
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.AspNetCore.SignalR.Client;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using PcControl.Client.data;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using System.Drawing.Imaging; 

namespace PcControl.Client
{
    public partial class MainWindow : Window
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

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
        private bool _sesionActiva = false;
        private bool _esApagadoDeWindows = false;
        private string _ipCandidata = "";

        private List<string> _playlistArchivos = new List<string>(); 
        private int _indicePlaylist = 0;
        private DispatcherTimer? _timerSlideshow;
        private string _carpetaCache = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CacheMedia");

        private const int GwlExstyle = -20;
        private const int WsExToolwindow = 0x00000080; 
        private const int GwlStyle = -16;
        private const int WsSysmenu = 0x00080000; 

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);
        
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_FORCEMINIMIZE = 11;
        
        private CancellationTokenSource? _streamCts;
        private byte[]? _ultimoFrameBytes;

        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += MainWindow_Loaded_SystemLock;
            this.ShowInTaskbar = false;

            SeguridadSistema.AsegurarAutoArranque();
            
            if (!Directory.Exists(_carpetaCache)) Directory.CreateDirectory(_carpetaCache);
            
            InicializarTimer();

            DispatcherTimer watchdog = new DispatcherTimer();
            watchdog.Interval = TimeSpan.FromMilliseconds(500);
            watchdog.Tick += Watchdog_Tick;
            watchdog.Start();

            BloqueoTeclado.Bloquear();

            try { if (File.Exists("apagar_guardian.tmp")) File.Delete("apagar_guardian.tmp"); } catch { }

            SystemEvents.SessionEnding += (s, e) => { _esApagadoDeWindows = true; };
            this.SourceInitialized += MainWindow_SourceInitialized;
        }

        private void MainWindow_SourceInitialized(object? sender, EventArgs e)
        {
            var helper = new WindowInteropHelper(this);
            int exStyle = GetWindowLong(helper.Handle, GwlExstyle);
            SetWindowLong(helper.Handle, GwlExstyle, exStyle | WsExToolwindow);
            int style = GetWindowLong(helper.Handle, GwlStyle);
            SetWindowLong(helper.Handle, GwlStyle, style & ~WsSysmenu);
        }

        private void MainWindow_Loaded_SystemLock(object sender, RoutedEventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int style = GetWindowLong(hwnd, GwlStyle);
            SetWindowLong(hwnd, GwlStyle, style & ~WsSysmenu);
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            CargarPlaylistLocal();
            ReproducirSiguiente();

            await EsperarCargaEscritorio();
            
            // 1. Esperamos a que lea el archivo de texto por completo
            await CargarConfiguracion();
            ConfigurarSignalR();

            BloqueoTeclado.Bloquear();

            ShowInTaskbar = false;
            WindowState = WindowState.Normal; 
            WindowState = WindowState.Maximized;
            this.Topmost = true;
            this.Activate();
            this.Focus();
        }

        private async Task EsperarCargaEscritorio()
        {
            int intentos = 0;
            while (intentos < 60)
            {
                IntPtr taskbarHandle = FindWindow("Shell_TrayWnd", null);
                if (taskbarHandle != IntPtr.Zero)
                {
                    await Task.Delay(4000);
                    return;
                }
                await Task.Delay(1000);
                intentos++;
            }
        }

        private void CargarPlaylistLocal()
        {
            try
            {
                if (!Directory.Exists(_carpetaCache)) Directory.CreateDirectory(_carpetaCache);

                var extensionesValidas = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    ".jpg", ".jpeg", ".png", ".bmp", ".webp",
                    ".mp4", ".avi", ".wmv", ".mov", ".mkv", ".gif" 
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
            if (_playlistArchivos.Count == 0) return;

            if (_indicePlaylist >= _playlistArchivos.Count) _indicePlaylist = 0;
            string archivo = _playlistArchivos[_indicePlaylist];
            _indicePlaylist++;

            if (!File.Exists(archivo))
            {
                ReproducirSiguiente(); 
                return;
            }

            string ext = Path.GetExtension(archivo).ToLower();

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
            VidFondo.Stop();
            VidFondo.Source = null;
            VidFondo.Visibility = Visibility.Collapsed;

            try
            {
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad; 
                bitmap.UriSource = new Uri(ruta);
                bitmap.EndInit();
                bitmap.Freeze(); 

                ImgFondo.Source = bitmap;
                ImgFondo.Visibility = Visibility.Visible;

                if (_timerSlideshow != null) _timerSlideshow.Stop();

                _timerSlideshow = new DispatcherTimer();
                _timerSlideshow.Interval = TimeSpan.FromSeconds(10); 
                _timerSlideshow.Tick += (s, e) =>
                {
                    _timerSlideshow.Stop();
                    ReproducirSiguiente();
                };
                _timerSlideshow.Start();
            }
            catch
            {
                ReproducirSiguiente(); 
            }
        }

        private void ReproducirVideo(string ruta)
        {
            if (_timerSlideshow != null) _timerSlideshow.Stop();

            ImgFondo.Source = null;
            ImgFondo.Visibility = Visibility.Collapsed;

            VidFondo.Source = new Uri(ruta);
            VidFondo.Visibility = Visibility.Visible;
            VidFondo.Play();
        }

        private void VidFondo_MediaEnded(object sender, RoutedEventArgs e)
        {
            VidFondo.Stop();
            ReproducirSiguiente();
        }

        private async Task DescargarNuevosFondos(List<string> urls)
        {
            try
            {
                if (!Directory.Exists(_carpetaCache)) Directory.CreateDirectory(_carpetaCache);

                await Dispatcher.InvokeAsync(() =>
                {
                    if (_timerSlideshow != null) _timerSlideshow.Stop();
                    VidFondo.Stop();
                    VidFondo.Source = null;
                    ImgFondo.Source = null;
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                });

                using (HttpClient client = new HttpClient())
                {
                    foreach (var url in urls)
                    {
                        var nombreArchivo = Path.GetFileName(url);
                        var rutaDestino = Path.Combine(_carpetaCache, nombreArchivo);

                        if (!File.Exists(rutaDestino) || new FileInfo(rutaDestino).Length == 0)
                        {
                            string urlCompleta = url;
                            if (!url.StartsWith("http")) 
                            {
                                // Tomamos el servidor base (ej. http://192.168.1.5:5249)
                                string serverBase = _urlServidor.Replace("/ciberhub", "");
                                urlCompleta = $"{serverBase}{url}";
                            }

                            var datos = await client.GetByteArrayAsync(urlCompleta);
                            await File.WriteAllBytesAsync(rutaDestino, datos);
                        }
                    }

                    var nombresValidos = urls.Select(u => Path.GetFileName(u)).ToHashSet();
                    var archivosLocales = Directory.GetFiles(_carpetaCache);

                    foreach (var archivoLocal in archivosLocales)
                    {
                        if (!nombresValidos.Contains(Path.GetFileName(archivoLocal)))
                        {
                            try { File.Delete(archivoLocal); } catch { }
                        }
                    }
                }

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

        private void Watchdog_Tick(object? sender, EventArgs e)
        {
            if (this.Visibility == Visibility.Visible && GridAdminConsole.Visibility == Visibility.Collapsed)
            {
                // MODO BLOQUEADO (Pantalla Negra)
                IntPtr ventanaActual = GetForegroundWindow();
                var miHandle = new WindowInteropHelper(this).Handle;

                // Si alguien logra poner una ventana (un juego) por encima de nuestra pantalla negra, la aplastamos
                if (ventanaActual != miHandle)
                {
                    ShowWindow(ventanaActual, SW_FORCEMINIMIZE);
                }

                if (!this.Topmost) this.Topmost = true;
                if (!this.IsActive) this.Activate();
            }
            else
            {
                // MODO DESBLOQUEADO
                if (this.Topmost) this.Topmost = false; 
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_cierreAutorizado && !_esApagadoDeWindows)
            {
                e.Cancel = true;
                this.Topmost = false;
                this.Topmost = true;
                this.Activate();
            }
            base.OnClosing(e);
        }

        private async Task CargarConfiguracion()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.txt");
            bool necesitaGuardar = false;

            if (File.Exists(path))
            {
                var lineas = File.ReadAllLines(path);
                foreach (var linea in lineas)
                {
                    if (linea.StartsWith("NOMBRE=")) _nombrePc = linea.Replace("NOMBRE=", "").Trim();
                    if (linea.StartsWith("IP="))
                    {
                        string ip = linea.Replace("IP=", "").Trim();
                        if (!string.IsNullOrWhiteSpace(ip) && ip != "127.0.0.1" && !ip.ToLower().Contains("localhost"))
                        {
                            _urlServidor = $"http://{ip}:5249/ciberhub";
                        }
                        else { _urlServidor = ""; }
                    }
                    
                    // --- CORRECCIÓN 2: LEER SEGURIDAD DESDE EL TXT ---
                    if (linea.StartsWith("PASS=")) _passwordAdmin = linea.Replace("PASS=", "").Trim();
                    if (linea.StartsWith("KEY=")) 
                    {
                        if (Enum.TryParse(linea.Replace("KEY=", "").Trim(), true, out Key k)) _teclaConsola = k;
                    }
                    if (linea.StartsWith("MOD="))
                    {
                        if (Enum.TryParse(linea.Replace("MOD=", "").Trim(), true, out ModifierKeys m)) _modificadorConsola = m;
                    }
                }
            }

            if (string.IsNullOrEmpty(_urlServidor))
            {
                txtEstadoConexion.Text = "Buscando servidor (Escaneo Inteligente)...";
                string ipServidorEncontrada = await BuscarServidorRobusto();

                if (!string.IsNullOrEmpty(ipServidorEncontrada))
                {
                    _urlServidor = $"http://{ipServidorEncontrada}:5249/ciberhub";
                    necesitaGuardar = true;
                }
                else { _urlServidor = "http://127.0.0.1:5249/ciberhub"; }
            }

            if (_nombrePc == "DESCONOCIDO" || string.IsNullOrEmpty(_nombrePc))
            {
                _nombrePc = "PC-TEST";
                necesitaGuardar = true;
            }

            if (necesitaGuardar) GuardarConfigEnTxt(_nombrePc, _urlServidor);

            this.Title = _nombrePc;
            txtEstadoConexion.Text = $"Soy: {_nombrePc}";
        }

        private async Task<string> BuscarServidorRobusto()
        {
            string ipPorUdp = await BuscarPorUDP();
            if (!string.IsNullOrEmpty(ipPorUdp)) return ipPorUdp;

            Dispatcher.Invoke(() => txtEstadoConexion.Text = "UDP falló. Escaneando red local (TCP)...");
            string ipPorTcp = await EscanearRedLocalTCP(5249);

            return ipPorTcp;
        }

        private async Task<string> BuscarPorUDP()
        {
            return await Task.Run(() =>
            {
                try
                {
                    using (var udp = new UdpClient())
                    {
                        udp.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
                        udp.EnableBroadcast = true;
                        byte[] sendBytes = Encoding.ASCII.GetBytes("CIBER_CLIENT_HOLA");
                        udp.Send(sendBytes, sendBytes.Length, new IPEndPoint(IPAddress.Broadcast, 8888));
                        udp.Client.ReceiveTimeout = 1500; 
                        var remoteEp = new IPEndPoint(IPAddress.Any, 0);
                        try
                        {
                            byte[] data = udp.Receive(ref remoteEp);
                            string mensaje = Encoding.UTF8.GetString(data);
                            if (mensaje.StartsWith("CIBER_SERVER|")) return mensaje.Split('|')[1];
                        }
                        catch { }
                    }
                }
                catch { }
                return "";
            });
        }

        private async Task<string> EscanearRedLocalTCP(int puerto)
        {
            string miIp = ObtenerIpLocal();
            if (string.IsNullOrEmpty(miIp)) return "";

            string baseIp = miIp.Substring(0, miIp.LastIndexOf('.') + 1);
            var ipsEncontradas = new ConcurrentBag<string>();
            var tareas = new List<Task>();

            for (int i = 1; i < 255; i++)
            {
                string ipObjetivo = baseIp + i;
                if (ipObjetivo == miIp) continue; 

                tareas.Add(Task.Run(async () =>
                {
                    try
                    {
                        using (var tcp = new TcpClient())
                        {
                            var taskConectar = tcp.ConnectAsync(ipObjetivo, puerto);
                            var taskTimeout = Task.Delay(200);

                            if (await Task.WhenAny(taskConectar, taskTimeout) == taskConectar && tcp.Connected)
                            {
                                ipsEncontradas.Add(ipObjetivo);
                            }
                        }
                    }
                    catch { }
                }));
            }

            await Task.WhenAll(tareas);
            return ipsEncontradas.FirstOrDefault() ?? "";
        }

        private string ObtenerIpLocal()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                    {
                        if (!ip.ToString().EndsWith(".1")) return ip.ToString();
                    }
                }
                return host.AddressList.FirstOrDefault(i => i.AddressFamily == AddressFamily.InterNetwork)?.ToString();
            }
            catch { return ""; }
        }

        private void GuardarConfigEnTxt(string nombre, string url)
        {
            string ip = "127.0.0.1";
            try
            {
                Uri u = new Uri(url);
                ip = u.Host;
            }
            catch { }

            // --- CORRECCIÓN 2.5: GUARDAR SEGURIDAD EN EL TXT ---
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.txt");
            string contenido = $"NOMBRE={nombre}\nIP={ip}\nPASS={_passwordAdmin}\nKEY={_teclaConsola}\nMOD={_modificadorConsola}";
            File.WriteAllText(path, contenido);
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            bool modificadorPresionado = (Keyboard.Modifiers & _modificadorConsola) == _modificadorConsola;
            if (modificadorPresionado && e.Key == _teclaConsola)
            {
                GridAdminConsole.Visibility = Visibility.Visible;
                txtPasswordAdmin.Focus();
            }

            if (e.Key == Key.System && e.SystemKey == Key.F4)
            {
                e.Handled = true; 
            }
        }

        private void VerificarPassword()
        {
            if (txtPasswordAdmin.Password == _passwordAdmin)
            {
                _cierreAutorizado = true;

                try { File.Create("apagar_guardian.tmp").Close(); } catch { }

                SeguridadSistema.EstablecerProcesoCritico(false);
                SeguridadSistema.BloquearAdministradorTareas(false);

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

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            GridAdminConsole.Visibility = Visibility.Collapsed;
            txtPasswordAdmin.Password = "";
        }

        private void txtPasswordAdmin_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) VerificarPassword();
        }

        private async void ConfigurarSignalR()
        {
            // --- LIMPIEZA 3: Inicializa la conexión de forma limpia. Eliminada la creación de clones abajo ---
            bool conexionExitosa = await IntentarConectar(_urlServidor);

            if (!conexionExitosa)
            {
                txtEstadoConexion.Text = "Servidor no responde. Buscando en red...";
                await Task.Delay(500);

                string nuevaIp = await BuscarServidorRobusto();

                if (!string.IsNullOrEmpty(nuevaIp))
                {
                    _urlServidor = $"http://{nuevaIp}:5249/ciberhub";
                    GuardarConfigEnTxt(_nombrePc, _urlServidor);

                    conexionExitosa = await IntentarConectar(_urlServidor);
                }

                if (!conexionExitosa)
                {
                    txtEstadoConexion.Text = "NO SE ENCONTRÓ EL SERVIDOR ❌";
                }
            }
        }

        private async Task<bool> IntentarConectar(string url)
        {
            try
            {
                txtEstadoConexion.Text = $"Conectando a {url}...";

                var nuevaConexion = new HubConnectionBuilder()
                    .WithUrl(url)
                    .WithAutomaticReconnect() 
                    .Build();

                // 1. RECIBIR CONFIGURACIÓN DE SEGURIDAD
                nuevaConexion.On<string, string, string>("RecibirConfiguracion", (pass, keyStr, modStr) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        _passwordAdmin = pass;
                        try
                        {
                            if (Enum.TryParse(keyStr, true, out Key k)) _teclaConsola = k;
                            if (Enum.TryParse(modStr, true, out ModifierKeys m)) _modificadorConsola = m;
                        }
                        catch { }
                        
                        GuardarConfigEnTxt(_nombrePc, _urlServidor);
                    });
                });

                // 2. RECIBIR ORDEN DE BLOQUEO / DESBLOQUEO
                nuevaConexion.On<string, int?, string>("RecibirOrden", (accion, tiempo, parametro) =>
                {
                    Dispatcher.Invoke(() => ProcesarOrden(accion, tiempo, parametro));
                });

                // 3. RECIBIR MENSAJE DEL ADMIN
                nuevaConexion.On<string>("RecibirMensaje", (mensaje) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        var msgWindow = new MensajeWindow(mensaje);
                        msgWindow.Topmost = true;
                        msgWindow.Show();
                    });
                });

                // 4. CAMBIO DE NOMBRE DE PC
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

                nuevaConexion.On<string>("StreamDetenido", (pc) => { });

                // 5. ACTUALIZAR FONDOS Y RECARGAR UI INMEDIATAMENTE
                nuevaConexion.On<List<string>>("ActualizarFondos", async (urls) => 
                {
                    try
                    {
                        using var client = new System.Net.Http.HttpClient();
                        
                        // A. Descargar nuevos
                        foreach (var urlImg in urls)
                        {
                            var nombreArchivo = System.IO.Path.GetFileName(urlImg);
                            var rutaDestino = System.IO.Path.Combine(_carpetaCache, nombreArchivo);

                            if (!System.IO.File.Exists(rutaDestino) || new System.IO.FileInfo(rutaDestino).Length == 0)
                            {
                                string urlCompleta = urlImg;
                                if (!urlImg.StartsWith("http")) 
                                {
                                    string serverBase = _urlServidor.Replace("/ciberhub", "");
                                    urlCompleta = $"{serverBase}{urlImg}";
                                }

                                var datos = await client.GetByteArrayAsync(urlCompleta);
                                await System.IO.File.WriteAllBytesAsync(rutaDestino, datos);
                            }
                        }

                        // B. Borrar huérfanos (los que el admin eliminó)
                        var archivosLocales = System.IO.Directory.GetFiles(_carpetaCache);
                        var nombresUrls = urls.Select(System.IO.Path.GetFileName).ToList();
                        foreach (var local in archivosLocales)
                        {
                            if (!nombresUrls.Contains(System.IO.Path.GetFileName(local)))
                            {
                                System.IO.File.Delete(local);
                            }
                        }

                        // C. Recargar y mostrar en pantalla al instante
                        await Dispatcher.InvokeAsync(() =>
                        {
                            CargarPlaylistLocal(); 
                            if (!_sesionActiva) 
                            {
                                ReproducirSiguiente(); 
                            }
                        });
                    }
                    catch { }
                });

                // --- INICIAR CONEXIÓN ---
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                await nuevaConexion.StartAsync(cts.Token);

                _connection = nuevaConexion;
                txtEstadoConexion.Text = $"Soy: {_nombrePc} | Conectado ✅";

                await _connection.InvokeAsync("RegistrarPc", _nombrePc);

                // --- MANEJO DE DESCONEXIÓN (MODO OFFLINE Y RECONEXIÓN INFINITA) ---
                _connection.Closed += async (error) =>
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        if (_sesionActiva && _minutosRestantes > 0)
                        {
                            txtEstadoConexion.Text = "Conexión Perdida - Modo Offline";
                            GuardarRespaldoLocal(); 
                        }
                        else if (!_sesionActiva)
                        {
                            ProcesarOrden("Bloquear", 0, "Conexión Perdida");
                        }
                        
                        // INICIA EL INTENTO ETERNO DE RECONEXIÓN
                        _ = CicloReconexionInfinita(); 
                    });
                };

                _connection.Reconnected += async (connectionId) =>
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        txtEstadoConexion.Text = $"Soy: {_nombrePc} | Reconectado";
                    });
                };
                
                IniciarLatidos();
                return true;
            }
            catch
            {
                return false; 
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
                    await _connection.InvokeAsync("ReportarEstado", _nombrePc, ObtenerMac(), ObtenerIp(),
                        _inputCongelado);
                }
            };
            _latidoTimer.Start();
        }

        private async void EnviarFrameStreaming()
        {
            if (!_isStreaming || _connection.State != HubConnectionState.Connected) return;

            try
            {
                int ancho = (int)SystemParameters.PrimaryScreenWidth;
                int alto = (int)SystemParameters.PrimaryScreenHeight;

                string? base64 = await Task.Run(() =>
                {
                    try
                    {
                        return TomarCapturaPantalla(ancho, alto);
                    }
                    catch (Exception ex)
                    {
                        File.AppendAllText("crash_log.txt", $"[{DateTime.Now}] Error Captura: {ex.Message}\n");
                        return null;
                    }
                });

                if (!string.IsNullOrEmpty(base64))
                    await _connection.InvokeAsync("EnviarCaptura", _nombrePc, base64);
            }
            catch (Exception ex)
            {
                File.AppendAllText("crash_log.txt", $"[{DateTime.Now}] Error Envio: {ex.Message}\n");
            }
        }

        private void ProcesarOrden(string accion, int? tiempo, string parametro)
        {
            switch (accion)
            {
                case "Desbloquear":
                    // Solo bloqueamos por error si el tiempo es NEGATIVO. 
                    if (tiempo < 0)
                    {
                        ProcesarOrden("Bloquear", 0, "Tiempo Agotado");
                        return; 
                    }

                    VidFondo.Stop();
                    if (_timerSlideshow != null) _timerSlideshow.Stop();

                    this.Hide(); 
                    _sesionActiva = true; 
                    BloqueoTeclado.Desbloquear();
                    _inputCongelado = false;
                    BloqueoInputTotal(false);

                    _minutosRestantes = tiempo ?? 0;
                    if (_infoWindow == null) _infoWindow = new InfoSesionWindow();
                    _infoWindow.Show();
                    
                    if (_minutosRestantes > 0)
                    {
                        DateTime inicio = DateTime.Now;
                        DateTime fin = inicio.AddMinutes(_minutosRestantes);
                        
                        string importeStr = string.IsNullOrEmpty(parametro) ? "--" : parametro;

                        _infoWindow.ActualizarDashboardCompleto(
                            $"{_minutosRestantes} min", 
                            "Sesión Iniciada", 
                            inicio.ToString("hh:mm tt"), 
                            fin.ToString("hh:mm tt"), 
                            $"{_minutosRestantes} minutos", 
                            importeStr
                        );
                        _timerLocal.Start(); // Iniciamos descuento
                    }
                    else
                    {
                        // MODO USO LIBRE (Tiempo 0)
                        string importeStr = string.IsNullOrEmpty(parametro) ? "--" : parametro;
                        _infoWindow.ActualizarDashboardCompleto(
                            "Libre", 
                            "Uso Libre", 
                            DateTime.Now.ToString("hh:mm tt"), 
                            "--", // No hay fin estimado
                            "--", // No hay tiempo total comprado
                            importeStr // Mostramos el precio (ej. S/ 0.00 o si le sumas extras)
                        );
                        
                        _timerLocal.Stop(); // Aseguramos que el cronómetro no descuente
                    }
                    break;

                case "Bloquear":
                    ReproducirSiguiente();

                    _timerLocal?.Stop();
                    _sesionActiva = false; 
                    if (_infoWindow != null)
                    {
                        _infoWindow.Hide();
                    }

                    BloqueoTeclado.Bloquear();
                    _inputCongelado = false;
                    BloqueoInputTotal(false);

                    txtMensaje.Text = parametro;
                    GridAdminConsole.Visibility = Visibility.Collapsed;

                    // --- NUEVO: ATAQUE AL JUEGO EN PANTALLA COMPLETA ---
                    IntPtr ventanaActual = GetForegroundWindow();
                    var miHandle = new WindowInteropHelper(this).Handle;

                    // Si el usuario está dentro de un juego, lo minimizamos a la fuerza
                    if (ventanaActual != IntPtr.Zero && ventanaActual != miHandle)
                    {
                        ShowWindow(ventanaActual, SW_FORCEMINIMIZE);
                    }

                    this.Show(); 
                    
                    // Truco WPF: Cambiar el estado normal a maximizado fuerza a la tarjeta gráfica a dibujar la ventana de nuevo
                    this.WindowState = WindowState.Normal;
                    this.WindowState = WindowState.Maximized;
                    this.Topmost = true;
                    this.Activate();
                    break;

                case "Apagar":
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
                    if (_infoWindow != null)
                        _infoWindow.ActualizarDatos($"{_minutosRestantes} min", "Tiempo corriendo...");
                    break;

                case "Captura":
                    break;

                case "EjecutarCliente":
                    if (this.WindowState == WindowState.Minimized)
                        this.WindowState = WindowState.Normal;
                    this.Show();
                    this.Activate();
                    this.Topmost = true; 
                    this.Topmost = false; 
                    this.Focus();
                    break;

                case "StreamStart":
                    int fps = 2; 
                    long calidad = 40; 
                    float escala = 0.5f; 

                    if (!string.IsNullOrEmpty(parametro) && parametro.Contains("|"))
                    {
                        var partes = parametro.Split('|');
                        if (partes.Length >= 1) int.TryParse(partes[0], out fps);
                        if (partes.Length >= 2) long.TryParse(partes[1], out calidad);
                        if (partes.Length >= 3)
                            float.TryParse(partes[2], System.Globalization.CultureInfo.InvariantCulture, out escala);
                    }

                    _streamCts?.Cancel();
                    _streamCts = new CancellationTokenSource();
                    _isStreaming = true;

                    Task.Run(() => BucleStreamingAsync(fps, calidad, escala, _streamCts.Token), _streamCts.Token);
                    break;

                case "StreamStop":
                    _isStreaming = false;
                    _streamCts?.Cancel();
                    _streamCts = null;
                    _ultimoFrameBytes = null; 
                    GC.Collect(); 
                    break;
            }
        }

        private async Task BucleStreamingAsync(int fps, long calidad, float escala, CancellationToken token)
        {
            int delayMs = 1000 / (fps > 0 ? fps : 1);

            while (!token.IsCancellationRequested && _isStreaming)
            {
                try
                {
                    byte[] frameActual = CapturarYComprimirPantalla(calidad, escala);

                    if (_ultimoFrameBytes == null || !frameActual.SequenceEqual(_ultimoFrameBytes))
                    {
                        _ultimoFrameBytes = frameActual;
                        string base64 = Convert.ToBase64String(frameActual);
                        
                        await _connection.InvokeAsync("EnviarCaptura", _nombrePc, base64);
                    }
                }
                catch { }
                await Task.Delay(delayMs, token);
            }
        }

        private byte[] CapturarYComprimirPantalla(long calidad, float escala)
        {
            int width = (int)SystemParameters.PrimaryScreenWidth;
            int height = (int)SystemParameters.PrimaryScreenHeight;
            int newWidth = (int)(width * escala);
            int newHeight = (int)(height * escala);

            using (Bitmap bmpOriginal = new Bitmap(width, height))
            {
                using (Graphics g = Graphics.FromImage(bmpOriginal))
                {
                    g.CopyFromScreen(0, 0, 0, 0, bmpOriginal.Size);
                }

                using (Bitmap bmpEscalado = new Bitmap(bmpOriginal, new (newWidth, newHeight)))
                using (MemoryStream ms = new MemoryStream())
                {
                    EncoderParameters encoderParams = new EncoderParameters(1);
                    encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, calidad);
                    
                    ImageCodecInfo? jpegCodec = ImageCodecInfo.GetImageEncoders().FirstOrDefault(e => e.MimeType == "image/jpeg");

                    if (jpegCodec != null)
                    {
                        bmpEscalado.Save(ms, jpegCodec, encoderParams);
                    }
                    else
                    {
                        bmpEscalado.Save(ms, ImageFormat.Jpeg);
                    }

                    return ms.ToArray();
                }
            }
        }
        
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
            _timerLocal.Tick += async (s, e) =>
            {
                // Solo actuar si hay una sesión activa y minutos por descontar
                if (!_sesionActiva || _minutosRestantes <= 0) return;

                _minutosRestantes--;
                GuardarRespaldoLocal();

                // 1. AVISO DE 2 MINUTOS
                if (_minutosRestantes == 2)
                {
                    // Sin InvokeAsync para que sea instantáneo en el hilo visual
                    var av = new MensajeWindow("Le quedan 2 minutos de sesión.");
                    av.Topmost = true;
                    av.Show();
                    av.Activate();
                }

                // 2. ACTUALIZAR DASHBOARD
                if (_infoWindow != null)
                    _infoWindow.ActualizarDatos($"{_minutosRestantes} min", "Tiempo corriendo...");

                // 3. BLOQUEO AUTOMÁTICO AL LLEGAR A 0
                if (_minutosRestantes <= 0)
                {
                    _timerLocal.Stop();
            
                    // Bloqueo local inmediato
                    ProcesarOrden("Bloquear", 0, "Tiempo Agotado");

                    // Informar al servidor para que el administrador vea la PC libre
                    if (_connection != null && _connection.State == HubConnectionState.Connected)
                    {
                        await _connection.InvokeAsync("NotificarTiempoAgotado", _nombrePc);
                    }

                    if (File.Exists("sesion_backup.tmp")) File.Delete("sesion_backup.tmp");
                }
            };
        }

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
        
        private string TomarCapturaPantalla(int width, int height)
        {
            using (var bitmap = new Bitmap(width, height))
            {
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.CopyFromScreen(0, 0, 0, 0, bitmap.Size);
                }
                using (var ms = new MemoryStream())
                {
                    bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                    return Convert.ToBase64String(ms.ToArray());
                }
            }
        }
        
        private void GuardarRespaldoLocal()
        {
            try
            {
                string contenido = $"{_sesionActiva}|{_minutosRestantes}";
                File.WriteAllText("sesion_backup.tmp", contenido);
                File.SetAttributes("sesion_backup.tmp", FileAttributes.Hidden);
            }
            catch { }
        }
        
        private async Task CicloReconexionInfinita()
        {
            while (_connection == null || _connection.State != HubConnectionState.Connected)
            {
                await Task.Delay(5000); // Espera 5 segundos
                Dispatcher.Invoke(() => txtEstadoConexion.Text = "Reconectando...");
                bool exito = await IntentarConectar(_urlServidor);
                if (exito) break;
            }
        }

        [DllImport("user32.dll")] static extern bool BlockInput(bool fBlockIt);
        private void BloqueoInputTotal(bool bloquear) { try { BlockInput(bloquear); } catch { } }
    }
}