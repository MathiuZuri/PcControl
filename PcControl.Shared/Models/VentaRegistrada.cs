namespace PcControl.Shared.Models
{
    public class VentaRegistrada
    {
        public int Id { get; set; }
        public string NombreProducto { get; set; } = "";
        public int Cantidad { get; set; }
        public decimal Total { get; set; }
        public DateTime Fecha { get; set; } = DateTime.Now;
    }
}