using System.ComponentModel.DataAnnotations;

namespace PcControl.Shared.Models;

public class Tarifa
{
    public int Id { get; set; }

    [Required]
    public string Nombre { get; set; } = "";

    [Range(1, 1440)]
    public int Minutos { get; set; } 

    [Range(0, 999)]
    public decimal Precio { get; set; }
}