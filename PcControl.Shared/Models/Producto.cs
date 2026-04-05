using System.ComponentModel.DataAnnotations;

namespace PcControl.Shared.Models;

public class Producto
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El nombre es obligatorio")]
    public string Nombre { get; set; } = "";

    [Required(ErrorMessage = "La categoría es obligatoria")]
    public string Categoria { get; set; } = "General";

    [Range(0.1, 999, ErrorMessage = "El precio debe ser mayor a 0")]
    public decimal Precio { get; set; }

    [Range(0, 9999, ErrorMessage = "El stock no puede ser negativo")]
    public int Stock { get; set; }
}