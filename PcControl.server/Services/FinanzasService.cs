using Microsoft.EntityFrameworkCore;
using PcControl.Server.Data;
using PcControl.Shared.Models;
using System.Globalization;

namespace PcControl.Server.Services
{
    public class FinanzasService
    {
        private readonly AppDbContext _context;

        public FinanzasService(AppDbContext context)
        {
            _context = context;
        }

        public async Task RegistrarGastoAsync(Gasto gasto)
        {
            _context.Gastos.Add(gasto);
            await _context.SaveChangesAsync();
        }

        // NUEVO: Método para eliminar un gasto si te equivocaste
        public async Task EliminarGastoAsync(int id)
        {
            var gasto = await _context.Gastos.FindAsync(id);
            if (gasto != null)
            {
                _context.Gastos.Remove(gasto);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<ResumenAnual> ObtenerResumenAnualAsync(int anio)
        {
            // ... (Este método queda IGUAL que antes) ...
            var sesiones = await _context.HistorialSesiones
                .Where(h => h.FechaFin.Year == anio).AsNoTracking().ToListAsync();
            var gastos = await _context.Gastos
                .Where(g => g.Fecha.Year == anio).AsNoTracking().ToListAsync();

            var resumen = new ResumenAnual { Anio = anio };

            for (int mes = 1; mes <= 12; mes++)
            {
                var sesionesMes = sesiones.Where(s => s.FechaFin.Month == mes).ToList();
                var gastosMes = gastos.Where(g => g.Fecha.Month == mes).ToList();

                resumen.Meses.Add(new FilaMensual
                {
                    NumeroMes = mes,
                    NombreMes = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(mes),
                    TotalInternet = sesionesMes.Sum(s => s.ImporteTiempo),
                    TotalExtras = sesionesMes.Sum(s => s.ImporteExtra),
                    TotalGastos = gastosMes.Sum(g => g.Monto)
                });
            }
            return resumen;
        }
        
        public async Task<ResumenMensual> ObtenerDetalleMensualAsync(int anio, int mes)
        {
            var sesiones = await _context.HistorialSesiones
                .Where(h => h.FechaFin.Year == anio && h.FechaFin.Month == mes).AsNoTracking().ToListAsync();

            var gastos = await _context.Gastos
                .Where(g => g.Fecha.Year == anio && g.Fecha.Month == mes).AsNoTracking().ToListAsync();

            var resumen = new ResumenMensual 
            { 
                Anio = anio, 
                Mes = mes,
                NombreMes = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(mes),
                
                // NUEVO: Llenamos la lista detallada de gastos aquí
                DetalleGastos = gastos.OrderBy(g => g.Fecha).ToList()
            };

            int diasEnMes = DateTime.DaysInMonth(anio, mes);

            for (int dia = 1; dia <= diasEnMes; dia++)
            {
                var sesionesDia = sesiones.Where(s => s.FechaFin.Day == dia).ToList();
                var gastosDia = gastos.Where(g => g.Fecha.Day == dia).ToList();

                if (sesionesDia.Any() || gastosDia.Any())
                {
                    resumen.Dias.Add(new FilaDiaria
                    {
                        Fecha = new DateTime(anio, mes, dia),
                        IngresoInternet = sesionesDia.Sum(s => s.ImporteTiempo),
                        IngresoExtras = sesionesDia.Sum(s => s.ImporteExtra),
                        Gastos = gastosDia.Sum(g => g.Monto)
                    });
                }
            }

            return resumen;
        }
        
        public async Task<List<int>> ObtenerAniosDisponiblesAsync()
        {
            // ... (Igual que antes) ...
            int anioActual = DateTime.Now.Year;
            int minAnioHistorial = anioActual;
            if (await _context.HistorialSesiones.AnyAsync()) 
                minAnioHistorial = await _context.HistorialSesiones.MinAsync(h => h.FechaFin.Year);
            
            int minAnioGastos = anioActual;
            if (await _context.Gastos.AnyAsync()) 
                minAnioGastos = await _context.Gastos.MinAsync(g => g.Fecha.Year);

            int anioInicio = Math.Min(minAnioHistorial, minAnioGastos);
            if (anioInicio > anioActual) anioInicio = anioActual;

            var listaAnios = new List<int>();
            for (int i = anioActual; i >= anioInicio; i--) listaAnios.Add(i);
            
            return listaAnios;
        }
    }

    // --- CLASES DTO ACTUALIZADAS ---
    public class ResumenAnual
    {
        public int Anio { get; set; }
        public List<FilaMensual> Meses { get; set; } = new();
        public decimal AnualInternet => Meses.Sum(m => m.TotalInternet);
        public decimal AnualExtras => Meses.Sum(m => m.TotalExtras);
        public decimal AnualBruto => Meses.Sum(m => m.TotalBruto);
        public decimal AnualGastos => Meses.Sum(m => m.TotalGastos);
        public decimal AnualLiquido => Meses.Sum(m => m.TotalLiquido);
    }

    public class FilaMensual
    {
        public int NumeroMes { get; set; }
        public string NombreMes { get; set; } = "";
        public decimal TotalInternet { get; set; }
        public decimal TotalExtras { get; set; }
        public decimal TotalGastos { get; set; }
        public decimal TotalBruto => TotalInternet + TotalExtras;
        public decimal TotalLiquido => TotalBruto - TotalGastos;
    }
    
    public class ResumenMensual
    {
        public int Anio { get; set; }
        public int Mes { get; set; }
        public string NombreMes { get; set; } = "";
        public List<FilaDiaria> Dias { get; set; } = new();
        
        // NUEVO: Lista para ver en qué se gastó
        public List<Gasto> DetalleGastos { get; set; } = new();

        public decimal TotalInternet => Dias.Sum(d => d.IngresoInternet);
        public decimal TotalExtras => Dias.Sum(d => d.IngresoExtras);
        public decimal TotalBruto => Dias.Sum(d => d.TotalBruto);
        public decimal TotalGastos => Dias.Sum(d => d.Gastos);
        public decimal TotalLiquido => Dias.Sum(d => d.TotalLiquido);
    }

    public class FilaDiaria
    {
        public DateTime Fecha { get; set; }
        public decimal IngresoInternet { get; set; }
        public decimal IngresoExtras { get; set; }
        public decimal Gastos { get; set; }
        public decimal TotalBruto => IngresoInternet + IngresoExtras;
        public decimal TotalLiquido => TotalBruto - Gastos;
    }
}