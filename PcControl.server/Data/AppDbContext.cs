using Microsoft.EntityFrameworkCore;
using PcControl.Shared.Models;

namespace PcControl.Server.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // --- TABLAS DE GESTIÓN DE PCs ---
        public DbSet<Computadora> Computadoras { get; set; }
        public DbSet<Sesion> Sesiones { get; set; }
        
        // --- TABLAS DE CONFIGURACIÓN Y VENTAS ---
        public DbSet<Configuracion> Configuraciones { get; set; }
        public DbSet<Producto> Productos { get; set; }
        public DbSet<DetalleVenta> DetallesVenta { get; set; }

        // --- NUEVAS TABLAS (SEGURIDAD, CLIENTES Y CAJA) ---
        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Cliente> Clientes { get; set; }
        public DbSet<MovimientoCaja> MovimientosCaja { get; set; }
    
        // Tarifas
        public DbSet<Tarifa> Tarifas { get; set; } 
        
        public DbSet<Gasto> Gastos { get; set; } 
        
        public DbSet<VentaRegistrada> VentasRegistradas { get; set; }
        
        public DbSet<HistorialSesion> HistorialSesiones { get; set; } 
        

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configuración clave primaria para la tabla Configuracion
            modelBuilder.Entity<Configuracion>().HasKey(c => c.Clave);

            // --------------------------------------------------------
            // DATOS SEMILLA (SEED DATA)
            // --------------------------------------------------------

            // 1. Computadoras Iniciales
            modelBuilder.Entity<Computadora>().HasData(
                new Computadora { Id = 1, Nombre = "PC-TEST", Estado = "Disponible" },
                new Computadora { Id = 2, Nombre = "PC-02", Estado = "Disponible" },
                new Computadora { Id = 3, Nombre = "PC-03", Estado = "Disponible" }
            );

            // 2. Configuración Inicial
            modelBuilder.Entity<Configuracion>().HasData(
                new Configuracion { Clave = "PrecioHora", Valor = "2.50" },
                new Configuracion { Clave = "NombreCiber", Valor = "Mi Ciber Lan" }
            );

            // 4. USUARIO ADMINISTRADOR POR DEFECTO
            // IMPORTANTE: En un sistema real, la contraseña "admin123" debería estar Hasheada.
            // Por ahora la ponemos simple para que puedas entrar, pero luego implementaremos el Hash.
            modelBuilder.Entity<Usuario>().HasData(
                new Usuario 
                { 
                    Id = 1, 
                    Username = "admin", 
                    NombreCompleto = "Super Administrador",
                    PasswordHash = "admin123", // Contraseña temporal
                    Rol = "Admin",
                    Activo = true
                }
            );
        }
    }
}