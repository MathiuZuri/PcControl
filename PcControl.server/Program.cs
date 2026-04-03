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
using System.Net.NetworkInformation; // NUEVO: Requerido para analizar tarjetas de red

var builder = WebApplication.CreateBuilder(args);

// --- 1. CONFIGURACIÓN DE RED INTELIGENTE (IGNORANDO VPNs) ---
string ipLocal = "127.0.0.1"; // Fallback por defecto
var interfaces = NetworkInterface.GetAllNetworkInterfaces()
    .Where(i => i.OperationalStatus == OperationalStatus.Up && 
                (i.NetworkInterfaceType == NetworkInterfaceType.Ethernet || 
                 i.NetworkInterfaceType == NetworkInterfaceType.Wireless80211));

foreach (var adapter in interfaces)
{
    // Ignorar adaptadores virtuales y VPNs por su nombre
    string desc = adapter.Description.ToLower();
    if (desc.Contains("zerotier") || desc.Contains("radmin") || desc.Contains("virtual") || desc.Contains("pseudo"))
        continue;

    var props = adapter.GetIPProperties();
    // Buscamos la tarjeta que tenga salida al router (Puerta de Enlace)
    if (props.GatewayAddresses.Any())
    {
        var ip = props.UnicastAddresses
            .FirstOrDefault(u => u.Address.AddressFamily == AddressFamily.InterNetwork)?.Address;
        
        if (ip != null)
        {
            ipLocal = ip.ToString();
            break; 
        }
    }
}

// Obligamos a Kestrel a escuchar SOLO en localhost y en la IP real del Wi-Fi/Cable
builder.WebHost.UseUrls($"http://localhost:5249", $"http://{ipLocal}:5249");


// 2. BASE DE DATOS
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
builder.Services.AddSingleton<PcStatusService>(); 
builder.Services.AddHostedService<MonitorTiempoService>();

// Servicios de negocio
builder.Services.AddScoped<TarifaService>();
builder.Services.AddScoped<ProductoService>();
builder.Services.AddScoped<HistorialService>();
builder.Services.AddScoped<FinanzasService>();
builder.Services.AddScoped<EstadisticasService>();

// Servicio de descubrimiento automático (UDP)
builder.Services.AddHostedService<PcControl.Server.Services.UdpDiscoveryService>();

// 4. SIGNALR
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
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
    });

builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();
builder.Services.AddScoped<ProtectedLocalStorage>(); 
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();

// 6. CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("PermitirTodo", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

var app = builder.Build();

// --- PIPELINE DE LA APLICACIÓN ---

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

// app.UseHttpsRedirection(); // Comentado para uso en red local sin SSL

// 7. CONFIGURACIÓN DE ARCHIVOS ESTÁTICOS (VIDEOS E IMÁGENES)
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

app.MapControllers();
app.MapHub<CiberHub>("/ciberhub");
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

// 8. SEMILLA DE DATOS
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

// 9. MOSTRAR IP DE CONEXIÓN EN CONSOLA
Console.WriteLine("=================================================");
Console.WriteLine("SERVIDOR INICIADO CORRECTAMENTE");
Console.WriteLine("Accede desde el celular u otras PCs usando esta IP oficial:");
Console.WriteLine($" -> http://{ipLocal}:5249");
Console.WriteLine("=================================================");

app.Run();