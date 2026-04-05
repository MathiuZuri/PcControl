using System.ComponentModel.DataAnnotations;

namespace PcControl.Shared.Models;

public class Gasto
{
    public int Id { get; set; }

    [Required(ErrorMessage = "La descripción es obligatoria")]
    public string Descripcion { get; set; } = "";

    [Required]
    [Range(0.1, 99999, ErrorMessage = "El monto debe ser mayor a 0")]
    public decimal Monto { get; set; }

    public DateTime Fecha { get; set; } = DateTime.Now;
    
    public List<Gasto> ListaGastos { get; set; } = new();
}