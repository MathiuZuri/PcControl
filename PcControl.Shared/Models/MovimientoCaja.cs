using System.ComponentModel.DataAnnotations.Schema;

namespace PcControl.Shared.Models;

public enum TipoMovimiento
{
    Ingreso,  // Ej: Iniciar caja con cambio
    Egreso    // Ej: Pagar delivery de comida, comprar hojas
}

public class MovimientoCaja
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; }
    public TipoMovimiento Tipo { get; set; }
        
    public string Descripcion { get; set; } = string.Empty; // "Compra de gaseosas"
        
    [Column(TypeName = "decimal(18,2)")]
    public decimal Monto { get; set; }
        
    public int UsuarioId { get; set; } // Quién hizo el movimiento
}