using Microsoft.EntityFrameworkCore;
using PcControl.Server.Data;
using PcControl.Shared.Models;

namespace PcControl.Server.Services
{
    public class AuthService
    {
        private readonly AppDbContext _context;

        public AuthService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Usuario?> LoginAsync(string username, string password)
        {
            // Busca el usuario por nombre
            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Username == username);

            if (usuario == null) return null;

            // VERIFICACIÓN DE PASSWORD
            // NOTA: Aquí estamos comparando texto plano porque en el Seed Data pusimos "admin123".
            // Más adelante, cambiaremos esto para usar un verificador de Hash real (BCrypt).
            if (usuario.PasswordHash == password && usuario.Activo)
            {
                return usuario;
            }

            return null;
        }
    }
}