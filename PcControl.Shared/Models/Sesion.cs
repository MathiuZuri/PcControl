namespace PcControl.Shared.Models;

using System.ComponentModel.DataAnnotations.Schema;

public class Sesion
{
    public int Id { get; set; }
        
    public int ComputadoraId { get; set; } // Qué PC se usó
    // (Opcional: Propiedad de navegación para EF Core)
    // public Computadora? Computadora { get; set; } 

    public DateTime HoraInicio { get; set; }
    public DateTime? HoraFin { get; set; }
        
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalCalculado { get; set; } // Lo que debería cobrar
        
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalCobrado { get; set; } // Lo que realmente se cobró (descuentos, etc.)
        
    public bool EstaPagado { get; set; } = false;
    public string MetodoPago { get; set; } = "Efectivo"; // Efectivo, Yape, Tarjeta
}