using System.ComponentModel.DataAnnotations;

namespace PcControl.Shared.Models;

public class Tarifa
{
    public int Id { get; set; }

    [Required]
    public string Nombre { get; set; } = ""; // Ej: "1 Hora", "Media Hora"

    [Range(1, 1440)]
    public int Minutos { get; set; } // Ej: 60, 30, 15

    [Range(0, 999)]
    public decimal Precio { get; set; } // Ej: 1.00, 0.50
}