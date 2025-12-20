using Microsoft.EntityFrameworkCore;
using PcControl.Server.Data;
using PcControl.Shared.Models;

namespace PcControl.Server.Services
{
    public class HistorialService
    {
        private readonly AppDbContext _context;

        public HistorialService(AppDbContext context)
        {
            _context = context;
        }

        // Obtener historial de UN día específico
        public async Task<List<HistorialSesion>> ObtenerPorFechaAsync(DateTime fecha)
        {
            return await _context.HistorialSesiones
                .Where(h => h.FechaFin.Date == fecha.Date) // Filtramos por día
                .OrderByDescending(h => h.FechaFin) // Los más recientes primero
                .AsNoTracking()
                .ToListAsync();
        }

        // Método para guardar (lo usaremos desde ComputadoraService)
        public async Task RegistrarSesionAsync(HistorialSesion sesion)
        {
            _context.HistorialSesiones.Add(sesion);
            await _context.SaveChangesAsync();
        }
    }
}