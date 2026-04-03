using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PcControl.Server.Data;
using PcControl.Server.Services;
using PcControl.Shared.Models;

namespace PcControl.server.Hubs
{
    public class CiberHub : Hub
    {
        private readonly AppDbContext _context;
        private readonly ConfiguracionService _configService;
        private readonly PcStatusService _statusService;
        
        public CiberHub(PcStatusService statusService, AppDbContext context, ConfiguracionService configService)
        {
            _context = context;
            _configService = configService;
            _statusService = statusService;
        }

        // 1. REGISTRAR PC Y ENVIARLE CONFIGURACIÓN (Llamado al iniciar el cliente)
        public async Task RegistrarPc(string nombrePc)
        {
            // Buscamos si la PC existe, si no, la creamos al momento
            var pc = await _context.Computadoras.FirstOrDefaultAsync(c => c.Nombre == nombrePc);
            
            if (pc == null)
            {
                pc = new Computadora 
                { 
                    Nombre = nombrePc, 
                    Estado = "Disponible" 
                };
                _context.Computadoras.Add(pc);
                await _context.SaveChangesAsync();
                
                await Clients.All.SendAsync("PC_Nueva_Vinculada", nombrePc);
                await Clients.All.SendAsync("RefrescarDashboard");
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, nombrePc);

            // Enviar Configuración de seguridad
            string pass = await _configService.ObtenerValorAsync("AdminPassword", "123456789a");
            string key = await _configService.ObtenerValorAsync("ConsoleKey", "Q");
            string modifier = await _configService.ObtenerValorAsync("ConsoleModifier", "Control");
            await Clients.Caller.SendAsync("RecibirConfiguracion", pass, key, modifier);

            // LÓGICA DE RESTAURACIÓN DE SESIÓN
            if (pc.Estado == "Ocupada")
            {
                int minutosRestantes = pc.MinutosRestantes;

                // --- CORRECCIÓN VITAL PARA MODO LIBRE ---
                // Si tiene minutos restantes O si es tiempo libre (TiempoLimite = 0)
                if (minutosRestantes > 0 || pc.TiempoLimiteMinutos == 0)
                {
                    string importeTotal = (pc.ImportePorTiempo + pc.ImporteExtra).ToString("C");
                    
                    // Si es tiempo libre, enviamos 0, de lo contrario enviamos lo que le queda
                    int tiempoAEnviar = pc.TiempoLimiteMinutos == 0 ? 0 : minutosRestantes;
                    
                    await Clients.Caller.SendAsync("RecibirOrden", "Desbloquear", tiempoAEnviar, importeTotal);
                }
                else
                {
                    await Clients.Caller.SendAsync("RecibirOrden", "Bloquear", 0, "Su tiempo finalizó.");
                }
            }
            else
            {
                await Clients.Caller.SendAsync("RecibirOrden", "Bloquear", 0, "Bienvenido");
            }

            Console.WriteLine($"PC Vinculada: {nombrePc} - Estado sincronizado.");
        }

        // 2. EL CLIENTE SE REPORTA (LATIDO)
        public async Task ReportarEstado(string nombrePc, string mac, string ip, bool pausada)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, nombrePc);

            _statusService.ActualizarLatido(nombrePc, pausada);
            
            var pc = await _context.Computadoras.FirstOrDefaultAsync(c => c.Nombre == nombrePc);
            if (pc != null)
            {
                if (pc.MacAddress != mac || pc.IpAddress != ip)
                {
                    pc.MacAddress = mac;
                    pc.IpAddress = ip;
                    await _context.SaveChangesAsync();
                }
            }
            
            // --- CORRECCIÓN REDUNDANCIA ---
            // Solo lo enviamos una vez después de actualizar todo
            await Clients.All.SendAsync("ActualizarLatidoFull", nombrePc, pausada);
        }

        // RECIBIR CAPTURA Y REENVIARLA
        public async Task EnviarCaptura(string nombrePc, string base64Image)
        {
            await Clients.All.SendAsync("RecibirCaptura", nombrePc, base64Image);
        }
        
        public async Task EnviarListaFondos(List<string> urls)
        {
            await Clients.All.SendAsync("ActualizarFondos", urls);
        }
        
        public async Task NotificarFinStream(string nombrePc)
        {
            await Clients.All.SendAsync("StreamDetenido", nombrePc);
        }
        
        public async Task NotificarTiempoAgotado(string nombrePc)
        {
            await Clients.All.SendAsync("PC_TiempoAgotado_Alerta", nombrePc);
            await Clients.All.SendAsync("RefrescarDashboard");
        }
    }
}