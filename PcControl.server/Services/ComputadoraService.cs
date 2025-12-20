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

        // --- MÉTODOS DE LECTURA ---
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

        // --- MÉTODOS DE ESCRITURA ---
        public async Task ActualizarPcAsync(Computadora pc)
        {
            using var context = _dbFactory.CreateDbContext();
            var pcEnDb = await context.Computadoras.FindAsync(pc.Id);
            
            if (pcEnDb != null)
            {
                bool nombreCambio = pcEnDb.Nombre != pc.Nombre;
                string nombreViejo = pcEnDb.Nombre;
                
                // 1. Actualizar datos básicos
                pcEnDb.Nombre = pc.Nombre;
                pcEnDb.IpAddress = pc.IpAddress;   
                pcEnDb.MacAddress = pc.MacAddress; 
                pcEnDb.Estado = pc.Estado;
                pcEnDb.HoraInicio = pc.HoraInicio;
                pcEnDb.TiempoLimiteMinutos = pc.TiempoLimiteMinutos;
                pcEnDb.ImporteExtra = pc.ImporteExtra;
                pcEnDb.NotaAdmin = pc.NotaAdmin;

                // Si hay productos nuevos en la lista temporal, los procesamos AHORA.
                if (pc.Consumos != null && pc.Consumos.Any())
                {
                    var inventario = await context.Productos.ToListAsync();

                    foreach (var consumo in pc.Consumos)
                    {
                        // Buscamos el producto en la BD
                        var productoReal = inventario.FirstOrDefault(p => 
                            p.Nombre.Trim().Equals(consumo.Nombre.Trim(), StringComparison.OrdinalIgnoreCase));
                        
                        // Si existe (y no es el item fantasma "Acumulado Anterior"), procesamos
                        if (productoReal != null)
                        {
                            Console.WriteLine($"[VENTA] Descontando: {productoReal.Nombre} - Cant: {consumo.Cantidad}");

                            // A. Descontar Stock
                            productoReal.Stock -= consumo.Cantidad;
                            context.Entry(productoReal).State = EntityState.Modified;

                            // B. Registrar en Estadísticas (VentasRegistradas)
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

                // 3. Guardar(PC actualizada + Stock descontado + Ventas registradas)
                await context.SaveChangesAsync();
                
                // 4. Notificaciones
                if (nombreCambio) await _hubContext.Clients.Group(nombreViejo).SendAsync("CambiarNombreIdentity", pc.Nombre);

                if (pc.Estado == "Ocupada")
                    await _hubContext.Clients.Group(pc.Nombre).SendAsync("RecibirOrden", "Desbloquear", pc.TiempoLimiteMinutos, "Bienvenido");
                else
                    await _hubContext.Clients.Group(pc.Nombre).SendAsync("RecibirOrden", "Bloquear", 0, "Tiempo Terminado");
                
                // AVISAR A TODOS QUE EL STOCK CAMBIÓ
                await _hubContext.Clients.All.SendAsync("RefrescarDashboard");
                await _hubContext.Clients.All.SendAsync("RefrescarProductos");
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
                    await _hubContext.Clients.Group(pc.Nombre).SendAsync("RecibirOrden", "Desbloquear", nuevosMinutosRestantes, "Tiempo Reanudado");
                }
                else 
                {
                     await _hubContext.Clients.Group(pc.Nombre).SendAsync("RecibirOrden", "Desbloquear", pc.MinutosRestantes, "Desbloqueado");
                }
            }
            await _hubContext.Clients.All.SendAsync("RefrescarDashboard");
        }

        // --- AQUÍ ESTÁ LA CORRECCIÓN CRÍTICA ---
        public async Task CobrarYFinalizarAsync(Computadora pc, decimal totalCobrado, string metodoPago)
        {
            using var context = _dbFactory.CreateDbContext();
            var pcDb = await context.Computadoras.FindAsync(pc.Id);

            if (pcDb != null)
            {
                var fechaInicioReal = pcDb.HoraInicio ?? DateTime.Now;
                var fechaFinReal = DateTime.Now;

                // 1. Guardar en Caja (Historial)
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

                // (Ya no procesamos productos aquí para evitar duplicados o listas vacías)

                // 2. Limpiar PC
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

        // --- MÉTODOS AUXILIARES ---

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