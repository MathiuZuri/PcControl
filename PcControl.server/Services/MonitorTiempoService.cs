using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PcControl.Server.Data;
using PcControl.server.Hubs;

namespace PcControl.Server.Services;

public class MonitorTiempoService : BackgroundService
{
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IHubContext<CiberHub> _hubContext;

    public MonitorTiempoService(IDbContextFactory<AppDbContext> dbFactory, IHubContext<CiberHub> hubContext)
    {
        _dbFactory = dbFactory;
        _hubContext = hubContext;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using (var context = _dbFactory.CreateDbContext())
            {
                var pcsOcupadas = await context.Computadoras
                    .Where(c => c.Estado == "Ocupada" && c.TiempoLimiteMinutos > 0 && c.HoraInicio != null)
                    .ToListAsync();

                foreach (var pc in pcsOcupadas)
                {
                    var usados = (DateTime.Now - pc.HoraInicio.Value).TotalMinutes;
                    if (usados >= pc.TiempoLimiteMinutos)
                    {
                        // TIEMPO CUMPLIDO: Mandar orden de bloqueo
                        await _hubContext.Clients.Group(pc.Nombre).SendAsync("RecibirOrden", "Bloquear", 0, "Tiempo Agotado");
                        await _hubContext.Clients.All.SendAsync("PC_TiempoAgotado_Alerta", pc.Nombre);
                    }
                }
            }
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); // Revisar cada 30 seg
        }
    }
}