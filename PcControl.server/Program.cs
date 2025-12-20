using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using PcControl.server.Components;
using PcControl.server.Hubs;
using PcControl.Server.Data;
using PcControl.Server.Services;
using Microsoft.AspNetCore.StaticFiles; 
using System.Net;
using System.Net.Sockets;

var builder = WebApplication.CreateBuilder(args);

// 1. CONFIGURACIÓN DE RED (Escuchar en todo)
builder.WebHost.UseUrls("http://0.0.0.0:5249");

// 2. BASE DE DATOS (Con Factory para evitar errores de hilos)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// 3. SERVICIOS
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ComputadoraService>();
builder.Services.AddScoped<ConfiguracionService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers();
builder.Services.AddScoped<UiRefreshService>();
builder.Services.AddSingleton<PcStatusService>(); // Vital para el estado Online/Offline

// Servicios de negocio
builder.Services.AddScoped<TarifaService>();
builder.Services.AddScoped<ProductoService>();
builder.Services.AddScoped<HistorialService>();
builder.Services.AddScoped<FinanzasService>();
builder.Services.AddScoped<EstadisticasService>();

// Servicio de descubrimiento automático (UDP)
builder.Services.AddHostedService<PcControl.Server.Services.UdpDiscoveryService>();

// 4. SIGNALR (Aumentado tamaño para transferencias si fuera necesario)
builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 2 * 1024 * 1024; // 2MB
    options.EnableDetailedErrors = true;
});

// 5. AUTENTICACIÓN
builder.Services.AddAuthentication("Cookies")
    .AddCookie("Cookies", options =>
    {
        options.LoginPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromHours(12); // Duración de sesión
    });

builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();
builder.Services.AddScoped<ProtectedLocalStorage>(); // Persistencia de login
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();

// 6. CORS (Permitir todo para evitar bloqueos en red local)
builder.Services.AddCors(options =>
{
    options.AddPolicy("PermitirTodo", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

// --- ZONA DE REDIRECCIÓN INTELIGENTE (VERSIÓN UNIVERSAL) ---
// =======================================================================
app.Use(async (context, next) =>
{
    string ipOficial = "";
    var hostName = Dns.GetHostName();
    var ips = await Dns.GetHostAddressesAsync(hostName);
    
    // Buscamos una IP que pertenezca a CUALQUIER rango de red local estándar:
    // - 192.168.x.x (Clásica doméstica)
    // - 10.x.x.x    (Común en oficinas o fibra óptica moderna)
    // - 172.x.x.x   (Común en empresas)
    // Y mantenemos el filtro !EndsWith(".1") para evitar VirtualBox/VMware
    
    var ipLan = ips.FirstOrDefault(i => 
        i.AddressFamily == AddressFamily.InterNetwork && 
        !IPAddress.IsLoopback(i) && 
        (
            i.ToString().StartsWith("192.168.") || 
            i.ToString().StartsWith("10.") || 
            i.ToString().StartsWith("172.")
        ) &&
        !i.ToString().EndsWith(".1") 
    );

    // Fallback: Si no encuentra ninguna "perfecta", agarra cualquiera que no sea localhost
    if (ipLan == null) 
        ipLan = ips.FirstOrDefault(i => i.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(i));

    if (ipLan != null) ipOficial = ipLan.ToString();

    // Redirección
    if (!string.IsNullOrEmpty(ipOficial))
    {
        var hostEntrante = context.Request.Host.Host;
        
        // Si no entran por la IP oficial (y no es localhost), redirigir.
        if (hostEntrante != ipOficial && hostEntrante != "127.0.0.1" && hostEntrante != "localhost") 
        {
            string urlCorrecta = $"http://{ipOficial}:5249{context.Request.Path}{context.Request.QueryString}";
            context.Response.Redirect(urlCorrecta);
            return; 
        }
    }
    await next();
});

// --- PIPELINE DE LA APLICACIÓN ---

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

// ¡¡IMPORTANTE!! ESTA LÍNEA DEBE ESTAR COMENTADA PARA QUE FUNCIONE EN RED LOCAL SIN SSL
// app.UseHttpsRedirection(); 

// 7. CONFIGURACIÓN DE ARCHIVOS ESTÁTICOS (VIDEOS E IMÁGENES)
// Esto permite que el cliente descargue .mp4, .mkv, .webp, etc.
var provider = new FileExtensionContentTypeProvider();
provider.Mappings[".mp4"] = "video/mp4";
provider.Mappings[".avi"] = "video/x-msvideo";
provider.Mappings[".mkv"] = "video/x-matroska";
provider.Mappings[".mov"] = "video/quicktime";
provider.Mappings[".webp"] = "image/webp";

app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = provider
});

app.UseCors("PermitirTodo");
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// Mapeos
app.MapControllers();
app.MapHub<CiberHub>("/ciberhub");
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

// --- 8. SEMILLA DE DATOS (Crear Admin y Tarifas) ---
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        // context.Database.EnsureDeleted(); // Descomentar solo si quieres resetear la BD
        context.Database.EnsureCreated();

        if (!context.Usuarios.Any())
        {
            context.Usuarios.Add(new PcControl.Shared.Models.Usuario
            {
                Username = "admin",
                PasswordHash = "admin123", // En producción usa Hashing real
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
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine("Error en semilla de datos: " + ex.Message);
    }
}

// --- 9. MOSTRAR IPs DISPONIBLES EN CONSOLA ---
// (Esto es mejor que forzar la redirección, así sabes a dónde entrar)
Console.WriteLine("=================================================");
Console.WriteLine("SERVIDOR INICIADO CORRECTAMENTE");
Console.WriteLine("Accede desde las otras PCs usando estas IPs:");

var host = Dns.GetHostEntry(Dns.GetHostName());
foreach (var ip in host.AddressList)
{
    if (ip.AddressFamily == AddressFamily.InterNetwork)
    {
        Console.WriteLine($" -> http://{ip}:5249");
    }
}
Console.WriteLine("=================================================");

app.Run();