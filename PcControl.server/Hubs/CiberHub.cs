using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PcControl.Server.Data;
using PcControl.Server.Services;

namespace PcControl.server.Hubs
{
    public class CiberHub : Hub
    {
        private readonly AppDbContext _context;
        private readonly ConfiguracionService _configService;
        private readonly PcStatusService _statusService;

        // UN SOLO CONSTRUCTOR PARA INYECTAR AMBOS SERVICIOS
        public CiberHub(PcStatusService statusService, AppDbContext context, ConfiguracionService configService)
        {
            _context = context;
            _configService = configService;
            _statusService = statusService;
        }

        // 1. REGISTRAR PC Y ENVIARLE CONFIGURACIÓN (Llamado al iniciar el cliente)
        public async Task RegistrarPc(string nombrePc)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, nombrePc);

            // 1. Enviar Configuración (Igual que antes)
            string pass = await _configService.ObtenerValorAsync("AdminPassword", "123456789a");
            string key = await _configService.ObtenerValorAsync("ConsoleKey", "Q");
            string modifier = await _configService.ObtenerValorAsync("ConsoleModifier", "Control");
            await Clients.Caller.SendAsync("RecibirConfiguracion", pass, key, modifier);

            // 2. LÓGICA DE RESTAURACIÓN DE SESIÓN (NUEVO)
            // Buscamos si esta PC debería estar ocupada
            var pc = await _context.Computadoras.FirstOrDefaultAsync(c => c.Nombre == nombrePc);
            
            if (pc != null)
            {
                if (pc.Estado == "Ocupada")
                {
                    // Calculamos cuánto tiempo le queda REALMENTE
                    // El tiempo siguió corriendo en el servidor aunque la PC estuviera apagada
                    int minutosRestantes = pc.MinutosRestantes;

                    if (minutosRestantes > 0)
                    {
                        // Aún tiene tiempo: La desbloqueamos y le decimos cuánto le queda
                        await Clients.Caller.SendAsync("RecibirOrden", "Desbloquear", minutosRestantes, "Sesión Restaurada");
                        Console.WriteLine($"PC {nombrePc}: Sesión restaurada con {minutosRestantes} min.");
                    }
                    else
                    {
                        // Se le acabó el tiempo mientras estaba apagada (o reiniciando)
                        // La marcamos como Disponible o simplemente la bloqueamos
                        await Clients.Caller.SendAsync("RecibirOrden", "Bloquear", 0, "Su tiempo finalizó.");
                        
                        // Opcional: Podrías cambiar el estado a "Disponible" en la BD aquí si quisieras
                        // pero es mejor dejarla "Ocupada" con 0 min para que el cajero cobre.
                    }
                }
                else
                {
                    // Si está disponible, nos aseguramos que se bloquee (por seguridad)
                    await Clients.Caller.SendAsync("RecibirOrden", "Bloquear", 0, "Bienvenido");
                }
            }

            Console.WriteLine($"PC Conectada: {nombrePc} - Config y Estado verificados.");
        }

        // 2. EL CLIENTE SE REPORTA (LATIDO)
        public async Task ReportarEstado(string nombrePc, string mac, string ip, bool pausada)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, nombrePc);

            _statusService.ActualizarLatido(nombrePc, pausada);
            
            await Clients.All.SendAsync("ActualizarLatidoFull", nombrePc, pausada);
            var pc = await _context.Computadoras.FirstOrDefaultAsync(c => c.Nombre == nombrePc);
            if (pc != null)
            {
                if (pc.MacAddress != mac || pc.IpAddress != ip)
                {
                    pc.MacAddress = mac;
                    pc.IpAddress = ip;
                    await _context.SaveChangesAsync();
                }

                // Avisamos al Home incluyendo el estado de pausa
                await Clients.All.SendAsync("ActualizarLatidoFull", nombrePc, pausada);
            }
        }
        // RECIBIR CAPTURA Y REENVIARLA
        public async Task EnviarCaptura(string nombrePc, string base64Image)
        {
            // Reenviamos a TODOS (Dashboard) para que se vea en el modal
            await Clients.All.SendAsync("RecibirCaptura", nombrePc, base64Image);
        }
        
        public async Task EnviarListaFondos(List<string> urls)
        {
            // Enviamos a TODOS los clientes la lista de URLs para descargar
            await Clients.All.SendAsync("ActualizarFondos", urls);
        }
        
        public async Task NotificarFinStream(string nombrePc)
        {
            await Clients.All.SendAsync("StreamDetenido", nombrePc);
        }
    }
}