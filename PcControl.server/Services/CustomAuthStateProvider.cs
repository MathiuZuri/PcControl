using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using PcControl.Shared.Models;

namespace PcControl.Server.Services
{
    public class CustomAuthStateProvider : AuthenticationStateProvider
    {
        private readonly ProtectedLocalStorage _localStorage;
        private ClaimsPrincipal _currentUser = new ClaimsPrincipal(new ClaimsIdentity());

        public CustomAuthStateProvider(ProtectedLocalStorage localStorage)
        {
            _localStorage = localStorage;
        }
        
        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            // LOG NUEVO: Para saber si entra aquí
            Console.WriteLine($"[AUTH CHECK] Comprobando estado... (Usuario actual: {_currentUser.Identity?.Name ?? "Anónimo"})");

            if (_currentUser.Identity.IsAuthenticated)
            {
                return new AuthenticationState(_currentUser);
            }

            try
            {
                // LOG NUEVO: Intentando leer storage
                Console.WriteLine("[AUTH CHECK] Intentando leer ProtectedSessionStorage...");
        
                var result = await _localStorage.GetAsync<UserSession>("UserSession");

                if (result.Success && result.Value != null)
                {
                    var session = result.Value;
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Name, session.Username),
                        new Claim(ClaimTypes.Role, session.Rol)
                    };

                    var identity = new ClaimsIdentity(claims, "CustomAuth");
                    _currentUser = new ClaimsPrincipal(identity);
            
                    Console.WriteLine($"[AUTH ÉXITO] ¡Sesión restaurada para {session.Username}!");
                }
                else
                {
                    Console.WriteLine("[AUTH FALLO] No se encontró sesión en el navegador.");
                }
            }
            catch (Exception ex)
            {
                // LOG NUEVO: Ver el error real
                Console.WriteLine($"[AUTH ERROR CRÍTICO] {ex.GetType().Name}: {ex.Message}");
            }

            return new AuthenticationState(_currentUser);
        }

        public async Task Login(Usuario usuario)
        {
            // 1. Actualizar memoria
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, usuario.Username),
                new Claim(ClaimTypes.Role, usuario.Rol)
            };
            var identity = new ClaimsIdentity(claims, "CustomAuth");
            _currentUser = new ClaimsPrincipal(identity);

            // 2. Actualizar navegador (Persistencia)
            try
            {
                await _localStorage.SetAsync("UserSession", new UserSession
                {
                    Username = usuario.Username,
                    Rol = usuario.Rol
                });
            }
            catch { /* Ignoramos si falla al guardar (ej: prerender) */ }

            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        }

        public async Task Logout()
        {
            _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
            try
            {
                await _localStorage.DeleteAsync("UserSession");
            }
            catch { /* Ignorar error */ }
            
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        }
    }

    public class UserSession
    {
        public string Username { get; set; } = "";
        public string Rol { get; set; } = "";
    }
}