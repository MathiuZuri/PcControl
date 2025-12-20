namespace PcControl.Shared.Models;

public class Configuracion
{
    // Usaremos una clave (ej: "PrecioHora") para buscar el valor
    public string Clave { get; set; } = string.Empty; 
    public string Valor { get; set; } = string.Empty;
}