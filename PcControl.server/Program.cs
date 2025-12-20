using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage; // Importante
using PcControl.server.Components;
using PcControl.server.Hubs;
using PcControl.Server.Data;
using PcControl.Server.Services;
using System.Net;
using System.Net.Sockets;

var builder = WebApplication.CreateBuilder(args);

// --- CAMBIO 1: FORZAR QUE ESCUCHE EN TODAS LAS IPs (0.0.0.0) ---
builder.WebHost.UseUrls("http://0.0.0.0:5249"); 
// -------------------------------------------------------------

// Configuración de SQLite
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// Servicios
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ComputadoraService>();
builder.Services.AddScoped<ConfiguracionService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers();
builder.Services.AddScoped<UiRefreshService>();
builder.Services.AddSingleton<PcStatusService>();

// SignalR
builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 2 * 1024 * 1024;
    options.EnableDetailedErrors = true;
});

// Autenticación
builder.Services.AddAuthentication("Cookies")
    .AddCookie("Cookies", options =>
    {
        options.LoginPath = "/login"; 
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });

builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();

// --- CAMBIO 2: USAR LOCAL STORAGE EN VEZ DE SESSION STORAGE ---
// Esto hace que el login se comparta entre pestañas y no se borre al recargar
builder.Services.AddScoped<ProtectedLocalStorage>(); 
// --------------------------------------------------------------

builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddScoped<TarifaService>();
builder.Services.AddScoped<ProductoService>();
builder.Services.AddScoped<HistorialService>();
builder.Services.AddScoped<FinanzasService>();
builder.Services.AddScoped<EstadisticasService>();
builder.Services.AddHostedService<PcControl.Server.Services.UdpDiscoveryService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("PermitirTodo", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

// --- MIDDLEWARE DE REDIRECCIÓN A IP FIJA ---
app.Use(async (context, next) =>
{
    // Detectar IP real automáticamente (Lógica mejorada)
    string ipOficial = "127.0.0.1";
    var hostName = Dns.GetHostName();
    var ips = await Dns.GetHostAddressesAsync(hostName);
    
    // Buscamos una IP típica de router (192.168.x.x) y evitamos las de VirtualBox que terminan en .1
    var ipLan = ips.FirstOrDefault(i => 
        i.AddressFamily == AddressFamily.InterNetwork && 
        !IPAddress.IsLoopback(i) && 
        i.ToString().StartsWith("192.168.") &&
        !i.ToString().EndsWith(".1")); // Evita puertas de enlace virtuales

    // Si no encuentra una perfecta, agarra cualquiera que parezca LAN
    if (ipLan == null) 
        ipLan = ips.FirstOrDefault(i => i.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(i));

    if (ipLan != null) ipOficial = ipLan.ToString();

    int puerto = 5249;
    var hostEntrante = context.Request.Host.Host;

    // Si entra por nombre de PC o localhost, forzar a la IP numérica
    if (hostEntrante != ipOficial && hostEntrante != "127.0.0.1" && hostEntrante != "localhost") 
    {
        // Validación extra: no redirigir si ya estamos en la IP correcta
        if (!string.IsNullOrEmpty(ipOficial))
        {
            string urlCorrecta = $"http://{ipOficial}:{puerto}{context.Request.Path}{context.Request.QueryString}";
            context.Response.Redirect(urlCorrecta);
            return; 
        }
    }
    await next();
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

// --- CAMBIO 3: QUITAR HTTPS REDIRECTION ---
// app.UseHttpsRedirection(); // <--- COMENTADO PARA EVITAR ERRORES EN RED LOCAL
// ------------------------------------------

app.UseCors("PermitirTodo");
app.UseStaticFiles(); 
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapControllers();
app.MapHub<CiberHub>("/ciberhub");
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

// --- SEMILLA DE DATOS (Crear Admin si no existe) ---
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        context.Database.EnsureCreated();

        if (!context.Usuarios.Any())
        {
            context.Usuarios.Add(new PcControl.Shared.Models.Usuario
            {
                Username = "admin",
                PasswordHash = "admin123",
                NombreCompleto = "Super Administrador",
                Rol = "Admin",
                Activo = true 
            });
            context.SaveChanges();
            Console.WriteLine("✅ USUARIO ADMIN CREADO: admin / admin123");
        }
        
        if (!context.Tarifas.Any())
        {
            context.Tarifas.AddRange(
                new PcControl.Shared.Models.Tarifa { Nombre = "1 Hora", Minutos = 60, Precio = 1.00m },
                new PcControl.Shared.Models.Tarifa { Nombre = "30 Minutos", Minutos = 30, Precio = 0.50m },
                new PcControl.Shared.Models.Tarifa { Nombre = "15 Minutos", Minutos = 15, Precio = 0.30m }
            );
            context.SaveChanges();
            Console.WriteLine("✅ TARIFAS POR DEFECTO CREADAS (S/.)");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine("Error creando usuario inicial: " + ex.Message);
    }
}

// MOSTRAR LA IP REAL EN CONSOLA
var host = Dns.GetHostEntry(Dns.GetHostName());
foreach (var ip in host.AddressList)
{
    if (ip.AddressFamily == AddressFamily.InterNetwork)
    {
        Console.WriteLine($" ACCESO LAN DISPONIBLE: http://{ip}:5249");
    }
}

app.Run();