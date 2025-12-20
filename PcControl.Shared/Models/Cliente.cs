using System.ComponentModel.DataAnnotations.Schema;

namespace PcControl.Shared.Models;

public class Cliente
{
    public int Id { get; set; }
    public string Dni { get; set; } = string.Empty; // Identificador único
    public string Nombres { get; set; } = string.Empty;
    public string UsuarioAcceso { get; set; } = string.Empty; // Para loguearse en la PC
    public string PasswordAcceso { get; set; } = string.Empty; // Simple, para la PC
        
    [Column(TypeName = "decimal(18,2)")]
    public decimal Saldo { get; set; } = 0; // Dinero a favor
        
    public int PuntosFidelidad { get; set; } = 0; // Opcional: Puntos para canjear horas
    public DateTime FechaRegistro { get; set; } = DateTime.Now;
}