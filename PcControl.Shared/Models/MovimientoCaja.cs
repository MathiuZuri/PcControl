using System.ComponentModel.DataAnnotations.Schema;

namespace PcControl.Shared.Models;

public enum TipoMovimiento
{
    Ingreso,
    Egreso 
}

public class MovimientoCaja
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; }
    public TipoMovimiento Tipo { get; set; }
        
    public string Descripcion { get; set; } = string.Empty; 
    [Column(TypeName = "decimal(18,2)")]
    public decimal Monto { get; set; }
    public int UsuarioId { get; set; } 
}