using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PcControl.Server.Data;
using PcControl.server.Hubs;
using PcControl.Shared.Models;

namespace PcControl.Server.Services
{
    public class ProductoService
    {
        // 1. Usamos la Fábrica en lugar del Contexto directo
        private readonly IDbContextFactory<AppDbContext> _dbFactory;
        private readonly IHubContext<CiberHub> _hubContext;
        
        public ProductoService(IDbContextFactory<AppDbContext> dbFactory, IHubContext<CiberHub> hubContext)
        {
            _dbFactory = dbFactory;
            _hubContext = hubContext;
        }

        public async Task<List<Producto>> ObtenerTodosAsync()
        {
            // Creamos un contexto nuevo para esta lectura
            using var context = _dbFactory.CreateDbContext();
            
            return await context.Productos
                .OrderBy(p => p.Categoria)
                .ThenBy(p => p.Nombre)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task GuardarProductoAsync(Producto producto)
        {
            using var context = _dbFactory.CreateDbContext();

            if (producto.Id == 0)
            {
                context.Productos.Add(producto);
            }
            else
            {
                // AL USAR FÁBRICA, YA NO NECESITAMOS EL CÓDIGO COMPLEJO DE "DETACH".
                // Como el contexto es nuevo, no está rastreando nada, así que el Update funciona directo.
                context.Productos.Update(producto);
            }
            
            await context.SaveChangesAsync();
            
            // Avisamos a todos (Dashboard y otros clientes) que el inventario cambió
            await _hubContext.Clients.All.SendAsync("RefrescarProductos");
        }

        public async Task EliminarProductoAsync(int id)
        {
            using var context = _dbFactory.CreateDbContext();
            
            var p = await context.Productos.FindAsync(id);
            if (p != null)
            {
                context.Productos.Remove(p);
                await context.SaveChangesAsync();
            }
            
            await _hubContext.Clients.All.SendAsync("RefrescarProductos");
        }
        
        public async Task<List<string>> ObtenerCategoriasAsync()
        {
            using var context = _dbFactory.CreateDbContext();
            
            return await context.Productos
                .Select(p => p.Categoria)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();
        }
    }
}