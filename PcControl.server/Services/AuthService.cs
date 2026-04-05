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

        public async Task<(Usuario? User, string ErrorMessage)> LoginAsync(string username, string password)
        {
            var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Username == username);

            if (usuario == null) 
                return (null, "Credenciales incorrectas.");

            if (!usuario.Activo)
                return (null, "Usuario inactivo.");

            // 1. VERIFICAR SI ESTÁ BLOQUEADO TEMPORALMENTE
            if (usuario.BloqueadoHasta.HasValue && usuario.BloqueadoHasta > DateTime.Now)
            {
                var tiempoRestante = (int)(usuario.BloqueadoHasta.Value - DateTime.Now).TotalMinutes;
                // Si falta menos de 1 minuto, mostramos 1 para no decir "0 minutos"
                tiempoRestante = tiempoRestante == 0 ? 1 : tiempoRestante; 
                return (null, $"Cuenta bloqueada por seguridad. Intente en {tiempoRestante} min.");
            }

            // 2. VERIFICACIÓN DE HASH CRIPTOGRÁFICO CON BCRYPT
            bool passwordValido = BCrypt.Net.BCrypt.Verify(password, usuario.PasswordHash);

            if (!passwordValido)
            {
                // 3. REGISTRAR INTENTO FALLIDO
                usuario.IntentosFallidos++;
                string mensajeError;

                if (usuario.IntentosFallidos >= 6)
                {
                    usuario.BloqueadoHasta = DateTime.Now.AddMinutes(3); // Bloqueo de media hora
                    mensajeError = "Límite de intentos superado. Cuenta bloqueada por 3 minutos.";
                }
                else
                {
                    mensajeError = $"Credenciales incorrectas. Intento {usuario.IntentosFallidos} de 5.";
                }

                await _context.SaveChangesAsync();
                return (null, mensajeError);
            }
            
            usuario.IntentosFallidos = 0;
            usuario.BloqueadoHasta = null;
            await _context.SaveChangesAsync();

            return (usuario, string.Empty);
        }
    }
}