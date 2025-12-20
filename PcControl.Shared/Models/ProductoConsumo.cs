namespace PcControl.Shared.Models;

public class ProductoConsumo
{
    public string Nombre { get; set; } = "";
    public decimal Precio { get; set; }
    public int Cantidad { get; set; }
    public decimal Total => Precio * Cantidad;
}