using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PcControl.Server.Data;
using PcControl.server.Hubs; 
using PcControl.Shared.Models;
using System.Net;
using System.Net.Sockets;

namespace PcControl.Server.Services
{
    public class ComputadoraService
    {
        private readonly IDbContextFactory<AppDbContext> _dbFactory;
        private readonly IHubContext<CiberHub> _hubContext;

        public ComputadoraService(IDbContextFactory<AppDbContext> dbFactory, IHubContext<CiberHub> hubContext)
        {
            _dbFactory = dbFactory;
            _hubContext = hubContext;
        }

        public async Task<List<Computadora>> ObtenerTodasAsync()
        {
            using var context = _dbFactory.CreateDbContext();
            return await context.Computadoras.AsNoTracking().ToListAsync();
        }

        public async Task EncenderPcAsync(int id)
        {
            using var context = _dbFactory.CreateDbContext();
            var pc = await context.Computadoras.FindAsync(id);
            if (pc != null && !string.IsNullOrEmpty(pc.MacAddress)) await SendWakeOnLan(pc.MacAddress);
            {
                await SendWakeOnLan(pc.MacAddress);
            }
        }

        public async Task ActualizarPcAsync(Computadora pc)
        {
            using var context = _dbFactory.CreateDbContext();
            var pcEnDb = await context.Computadoras.FindAsync(pc.Id);
            
            if (pcEnDb != null)
            {
                bool nombreCambio = pcEnDb.Nombre != pc.Nombre;
                string nombreViejo = pcEnDb.Nombre;
                
                pcEnDb.Nombre = pc.Nombre;
                pcEnDb.IpAddress = pc.IpAddress;   
                pcEnDb.MacAddress = pc.MacAddress; 
                pcEnDb.Estado = pc.Estado;
                pcEnDb.HoraInicio = pc.HoraInicio;
                pcEnDb.TiempoLimiteMinutos = pc.TiempoLimiteMinutos;
                pcEnDb.ImporteExtra = pc.ImporteExtra;
                pcEnDb.NotaAdmin = pc.NotaAdmin;

                if (pc.Consumos != null && pc.Consumos.Any())
                {
                    var inventario = await context.Productos.ToListAsync();

                    foreach (var consumo in pc.Consumos)
                    {
                        var productoReal = inventario.FirstOrDefault(p => 
                            p.Nombre.Trim().Equals(consumo.Nombre.Trim(), StringComparison.OrdinalIgnoreCase));
                        
                        if (productoReal != null)
                        {
                            Console.WriteLine($"[VENTA] Descontando: {productoReal.Nombre} - Cant: {consumo.Cantidad}");

                            productoReal.Stock -= consumo.Cantidad;
                            context.Entry(productoReal).State = EntityState.Modified;

                            var venta = new VentaRegistrada
                            {
                                NombreProducto = consumo.Nombre,
                                Cantidad = consumo.Cantidad,
                                Total = consumo.Total,
                                Fecha = DateTime.Now
                            };
                            context.VentasRegistradas.Add(venta);
                        }
                    }
                }

                await context.SaveChangesAsync();
                
                if (nombreCambio) await _hubContext.Clients.Group(nombreViejo).SendAsync("CambiarNombreIdentity", pc.Nombre);

                if (pc.Estado == "Ocupada")
                {
                    int minutosAEnviar = pc.TiempoLimiteMinutos ?? 0;
    
                    if (pc.HoraInicio.HasValue && pc.TiempoLimiteMinutos > 0)
                    {
                        var minutosUsados = (int)(DateTime.Now - pc.HoraInicio.Value).TotalMinutes;
                        minutosAEnviar = pc.TiempoLimiteMinutos.Value - minutosUsados;
                    }
                    
                    if (minutosAEnviar <= 0 && pc.TiempoLimiteMinutos > 0) 
                    {
                        await _hubContext.Clients.Group(pc.Nombre).SendAsync("RecibirOrden", "Bloquear", 0, "Tiempo Agotado");
                    }
                    else 
                    {
                        await _hubContext.Clients.Group(pc.Nombre).SendAsync("RecibirOrden", "Desbloquear", minutosAEnviar, (pc.ImportePorTiempo + pc.ImporteExtra).ToString("C"));
                    }
                }
                else
                {
                    await _hubContext.Clients.Group(pc.Nombre).SendAsync("RecibirOrden", "Bloquear", 0, "Tiempo Terminado");
                }
            }
        }

        public async Task<Computadora> CrearPcAsync(Computadora pc)
        {
            using var context = _dbFactory.CreateDbContext();
            pc.Estado = "Disponible";
            context.Computadoras.Add(pc);
            await context.SaveChangesAsync();
            await _hubContext.Clients.All.SendAsync("RefrescarDashboard");
            return pc;
        }

        public async Task EliminarPcAsync(int id)
        {
            using var context = _dbFactory.CreateDbContext();
            var pc = await context.Computadoras.FindAsync(id);
            if (pc != null)
            {
                context.Computadoras.Remove(pc);
                await context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("RefrescarDashboard");
            }
        }

        public async Task AlternarPausaAsync(int pcId, bool pausar)
        {
            using var context = _dbFactory.CreateDbContext();
            var pc = await context.Computadoras.FindAsync(pcId);
            if (pc == null) return;

            if (pausar)
            {
                if (pc.InicioPausa == null)
                {
                    pc.InicioPausa = DateTime.Now;
                    await context.SaveChangesAsync();
                    await _hubContext.Clients.Group(pc.Nombre).SendAsync("RecibirOrden", "CongelarInput", 0, "SISTEMA PAUSADO POR ADMIN");
                }
            }
            else
            {
                if (pc.InicioPausa != null && pc.HoraInicio != null)
                {
                    TimeSpan tiempoPerdido = DateTime.Now - pc.InicioPausa.Value;
                    pc.HoraInicio = pc.HoraInicio.Value.Add(tiempoPerdido);
                    pc.InicioPausa = null;
                    await context.SaveChangesAsync();

                    int nuevosMinutosRestantes = 0;
                    if (pc.TiempoLimiteMinutos > 0)
                    {
                        var usados = (int)(DateTime.Now - pc.HoraInicio.Value).TotalMinutes;
                        nuevosMinutosRestantes = pc.TiempoLimiteMinutos.Value - usados;
                    }
                    await _hubContext.Clients.Group(pc.Nombre).SendAsync("RecibirOrden", "Desbloquear", nuevosMinutosRestantes, (pc.ImportePorTiempo + pc.ImporteExtra).ToString("C"));
                }
                else 
                {
                    await _hubContext.Clients.Group(pc.Nombre).SendAsync("RecibirOrden", "Desbloquear", pc.MinutosRestantes, (pc.ImportePorTiempo + pc.ImporteExtra).ToString("C"));
                }
            }
            await _hubContext.Clients.All.SendAsync("RefrescarDashboard");
        }
        
        public async Task CobrarYFinalizarAsync(Computadora pc, decimal totalCobrado, string metodoPago)
        {
            using var context = _dbFactory.CreateDbContext();
            var pcDb = await context.Computadoras.FindAsync(pc.Id);

            if (pcDb != null)
            {
                var fechaInicioReal = pcDb.HoraInicio ?? DateTime.Now;
                var fechaFinReal = DateTime.Now;

                // Guardar en Caja (Historial)
                var historial = new HistorialSesion
                {
                    PcNombre = pcDb.Nombre,
                    FechaInicio = fechaInicioReal,
                    FechaFin = fechaFinReal,
                    ImporteTiempo = pc.ImportePorTiempo,
                    ImporteExtra = pc.ImporteExtra,
                    TotalCobrado = totalCobrado,
                    MetodoPago = metodoPago
                };
                context.HistorialSesiones.Add(historial);

                // Limpiar PC
                pcDb.Estado = "Disponible";
                pcDb.HoraInicio = null;
                pcDb.HoraFin = null;
                pcDb.TiempoLimiteMinutos = 0;
                pcDb.ImporteExtra = 0;
                pcDb.ImportePorTiempo = 0;
                pcDb.NotaAdmin = "";
                pcDb.InicioPausa = null;

                if (pcDb.Consumos != null) pcDb.Consumos.Clear();

                await context.SaveChangesAsync();

                // 3. Notificaciones
                await _hubContext.Clients.Group(pc.Nombre).SendAsync("RecibirOrden", "Bloquear", 0, "Gracias por su visita");
                await _hubContext.Clients.All.SendAsync("RefrescarDashboard");
            }
        }
        
        public async Task AnularSesionAsync(int pcId)
        {
            using var context = _dbFactory.CreateDbContext();
            var pc = await context.Computadoras.FindAsync(pcId);

            if (pc != null)
            {
                pc.Estado = "Disponible";
                pc.HoraInicio = null;
                pc.HoraFin = null;
                pc.TiempoLimiteMinutos = 0;
                pc.ImporteExtra = 0;
                pc.ImportePorTiempo = 0;
                pc.NotaAdmin = "";
                pc.InicioPausa = null;
                
                if (pc.Consumos != null) pc.Consumos.Clear();

                await context.SaveChangesAsync();

                // Intentamos enviar la orden de bloqueo por si la PC vuelve a estar online
                await _hubContext.Clients.Group(pc.Nombre).SendAsync("RecibirOrden", "Bloquear", 0, "Sesión Anulada por Admin");
                await _hubContext.Clients.All.SendAsync("RefrescarDashboard");
            }
        }
        

        public async Task EnviarComandoAsync(string nombrePc, string comando, string parametro = "")
        {
            await _hubContext.Clients.Group(nombrePc).SendAsync("RecibirOrden", comando, 0, parametro);
            if (comando == "StreamStop") await _hubContext.Clients.All.SendAsync("StreamDetenido", nombrePc);
        }

        public async Task EnviarMensajeAsync(string nombrePc, string mensaje)
        {
            await _hubContext.Clients.Group(nombrePc).SendAsync("RecibirMensaje", mensaje);
        }

        public async Task NotificarCambioManual()
        {
            await _hubContext.Clients.All.SendAsync("RefrescarDashboard");
        }

        private async Task SendWakeOnLan(string mac)
        {
            try
            {
                var macBytes = System.Net.NetworkInformation.PhysicalAddress.Parse(mac.Replace(":", "-")).GetAddressBytes();
                var payload = new byte[102];
                for (int i = 0; i < 6; i++) payload[i] = 0xFF;
                for (int i = 0; i < 16; i++) Buffer.BlockCopy(macBytes, 0, payload, 6 + (i * 6), 6);
                using var client = new UdpClient();
                client.EnableBroadcast = true;
                await client.SendAsync(payload, payload.Length, new IPEndPoint(IPAddress.Broadcast, 9));
            }
            catch (Exception ex) { Console.WriteLine($"Error WoL: {ex.Message}"); }
        }
    }
}