namespace PcControl.Shared.Models;

using System.ComponentModel.DataAnnotations.Schema;

public class Sesion
{
    public int Id { get; set; }
        
    public int ComputadoraId { get; set; }

    public DateTime HoraInicio { get; set; }
    public DateTime? HoraFin { get; set; }
        
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalCalculado { get; set; } 
        
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalCobrado { get; set; } 
        
    public bool EstaPagado { get; set; } = false;
    public string MetodoPago { get; set; } = "Efectivo";
}