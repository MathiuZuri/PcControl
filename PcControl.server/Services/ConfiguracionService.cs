using Microsoft.EntityFrameworkCore;
using PcControl.Server.Data;
using PcControl.Shared.Models;

namespace PcControl.Server.Services
{
    public class ConfiguracionService
    {
        private readonly AppDbContext _context;

        public ConfiguracionService(AppDbContext context)
        {
            _context = context;
        }

        // Obtener un valor (ej: "AdminPassword")
        public async Task<string> ObtenerValorAsync(string clave, string valorPorDefecto = "")
        {
            var config = await _context.Configuraciones.FindAsync(clave);
            if (config == null) return valorPorDefecto;
            return config.Valor;
        }

        // Guardar un valor
        public async Task GuardarValorAsync(string clave, string valor)
        {
            var config = await _context.Configuraciones.FindAsync(clave);
            if (config == null)
            {
                _context.Configuraciones.Add(new Configuracion { Clave = clave, Valor = valor });
            }
            else
            {
                config.Valor = valor;
            }
            await _context.SaveChangesAsync();
        }
    }
}