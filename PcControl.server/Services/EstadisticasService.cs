using Microsoft.EntityFrameworkCore;
using PcControl.Server.Data;
using PcControl.Shared.Models;
using System.Globalization;

namespace PcControl.Server.Services
{
    public class EstadisticasService
    {
        // CAMBIO: Usamos Factory en lugar de Context directo
        private readonly IDbContextFactory<AppDbContext> _dbFactory;

        public EstadisticasService(IDbContextFactory<AppDbContext> dbFactory)
        {
            _dbFactory = dbFactory;
        }

        // 1. PC CON MÁS HORAS DE USO
        public async Task<List<DatoEstadistico>> ObtenerTopPcsAsync()
        {
            using var context = _dbFactory.CreateDbContext(); // Nuevo contexto seguro
            
            var sesiones = await context.HistorialSesiones
                .AsNoTracking()
                .ToListAsync();

            var resultado = sesiones
                .GroupBy(s => s.PcNombre)
                .Select(g => new DatoEstadistico
                {
                    Etiqueta = g.Key,
                    Valor = g.Sum(s => (s.FechaFin - s.FechaInicio).TotalHours),
                    ValorSecundario = g.Count()
                })
                .OrderByDescending(x => x.Valor)
                .Take(5)
                .ToList();

            return resultado;
        }

        // 2. PRODUCTO MÁS VENDIDO
        public async Task<List<DatoEstadistico>> ObtenerTopProductosAsync()
        {
            using var context = _dbFactory.CreateDbContext(); // Nuevo contexto seguro

            var rawData = await context.VentasRegistradas
                .GroupBy(v => v.NombreProducto)
                .Select(g => new 
                {
                    Nombre = g.Key,
                    Cantidad = g.Sum(v => v.Cantidad),
                    Dinero = g.Sum(v => v.Total)
                })
                .ToListAsync();

            return rawData
                .OrderByDescending(x => x.Cantidad)
                .Take(5)
                .Select(x => new DatoEstadistico
                {
                    Etiqueta = x.Nombre,
                    Valor = x.Cantidad,
                    ValorMonetario = x.Dinero
                })
                .ToList();
        }

        // 3. AÑO CON MÁS INGRESOS
        public async Task<List<DatoEstadistico>> ObtenerMejoresAniosAsync()
        {
            using var context = _dbFactory.CreateDbContext(); // Nuevo contexto seguro

            var rawData = await context.HistorialSesiones
                .GroupBy(h => h.FechaFin.Year)
                .Select(g => new 
                {
                    Anio = g.Key,
                    Total = g.Sum(s => s.TotalCobrado)
                })
                .ToListAsync();

            return rawData
                .OrderByDescending(x => x.Total)
                .Select(x => new DatoEstadistico
                {
                    Etiqueta = x.Anio.ToString(),
                    ValorMonetario = x.Total
                })
                .ToList();
        }

        // 4. MEJOR MES DEL AÑO SELECCIONADO
        public async Task<List<DatoEstadistico>> ObtenerMejoresMesesAsync(int anio)
        {
            using var context = _dbFactory.CreateDbContext(); // Nuevo contexto seguro

            var rawData = await context.HistorialSesiones
                .Where(h => h.FechaFin.Year == anio)
                .GroupBy(h => h.FechaFin.Month)
                .Select(g => new 
                {
                    Mes = g.Key,
                    Total = g.Sum(s => s.TotalCobrado)
                })
                .ToListAsync();

            return rawData
                .OrderByDescending(x => x.Total)
                .Select(d => new DatoEstadistico
                {
                    Etiqueta = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(d.Mes),
                    ValorMonetario = d.Total
                }).ToList();
        }
    }

    public class DatoEstadistico
    {
        public string Etiqueta { get; set; } = "";
        public double Valor { get; set; } 
        public decimal ValorMonetario { get; set; } 
        public int ValorSecundario { get; set; } 
    }
}