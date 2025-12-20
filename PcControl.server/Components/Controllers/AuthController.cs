using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using PcControl.Server.Services;

namespace PcControl.server.Components.Controllers
{
    [Route("account")] 
    public class AuthController : Controller
    {
        private readonly AuthService _authService;

        public AuthController(AuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromForm] string username, [FromForm] string password)
        {
            // 1. Validar credenciales usando tu servicio existente
            var usuario = await _authService.LoginAsync(username, password);

            if (usuario == null)
            {
                // Si falla, volvemos al login con un error en la URL
                return Redirect("/login?error=true");
            }

            // 2. Crear los Claims (La credencial)
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, usuario.Username),
                new Claim(ClaimTypes.Role, usuario.Rol),
                new Claim("UserId", usuario.Id.ToString())
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true, // Mantener sesión abierta
                ExpiresUtc = DateTime.UtcNow.AddHours(8)
            };

            // 3. ¡AQUÍ SÍ PODEMOS CREAR LA COOKIE!
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            // 4. Redirigir al Home
            return Redirect("/");
        }

        [HttpGet("logout")]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Redirect("/login");
        }
    }
}