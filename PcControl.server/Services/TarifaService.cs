using Microsoft.EntityFrameworkCore;
using PcControl.Server.Data;
using PcControl.Shared.Models;

namespace PcControl.Server.Services;

public class TarifaService
    {
        private readonly AppDbContext _context;

        public TarifaService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<Tarifa>> ObtenerTodasAsync()
        {
            // Ordenamos por minutos descendente (de mayor a menor) para que el cálculo funcione
            return await _context.Tarifas.OrderByDescending(t => t.Minutos).ToListAsync();
        }

        public async Task GuardarTarifaAsync(Tarifa tarifa)
        {
            if (tarifa.Id == 0) _context.Tarifas.Add(tarifa);
            else _context.Tarifas.Update(tarifa);
            await _context.SaveChangesAsync();
        }

        public async Task EliminarTarifaAsync(int id)
        {
            var t = await _context.Tarifas.FindAsync(id);
            if (t != null)
            {
                _context.Tarifas.Remove(t);
                await _context.SaveChangesAsync();
            }
        }

        // --- ALGORITMO DE CÁLCULO INTELIGENTE ---
        public async Task<decimal> CalcularPrecioTotal(int minutosTotales)
        {
            if (minutosTotales <= 0) return 0;

            // 1. Traemos las tarifas de MAYOR a MENOR duración
            var tarifas = await _context.Tarifas
                .OrderByDescending(t => t.Minutos)
                .ToListAsync();

            decimal total = 0;
            int tiempoRestante = minutosTotales;

            // 2. Llenamos los bloques de tiempo
            foreach (var tarifa in tarifas)
            {
                if (tiempoRestante <= 0) break;

                // Cuántas veces cabe esta tarifa en el tiempo restante
                int cantidad = tiempoRestante / tarifa.Minutos;

                if (cantidad > 0)
                {
                    total += cantidad * tarifa.Precio;
                    tiempoRestante -= cantidad * tarifa.Minutos;
                }
            }

            // 3. (Opcional) Si sobran minutos que no encajan en ninguna tarifa (ej: sobran 2 min)
            // Política: Cobrar la tarifa más pequeña disponible o regalar.
            // Aquí cobraremos la tarifa más pequeña si sobra algo.
            if (tiempoRestante > 0 && tarifas.Any())
            {
                var tarifaMasPequena = tarifas.Last(); // La de 15 min (o la que agregue el admin)
                total += tarifaMasPequena.Precio;
            }

            return total;
        }
    }