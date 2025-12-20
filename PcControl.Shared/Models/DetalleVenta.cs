using System.ComponentModel.DataAnnotations.Schema;

namespace PcControl.Shared.Models;

public class DetalleVenta
{
    public int Id { get; set; }
    public int VentaId { get; set; } // A qué venta pertenece
        
    public int ProductoId { get; set; }
    public string NombreProductoSnapshot { get; set; } = string.Empty; // Guardamos el nombre por si luego cambia
        
    public int Cantidad { get; set; }
        
    [Column(TypeName = "decimal(18,2)")]
    public decimal PrecioUnitario { get; set; } // Guardamos el precio al momento de la venta
}