namespace PcControl.Shared.Models;

public class HistorialSesion
{
    public int Id { get; set; }

    public string PcNombre { get; set; } = ""; // Ej: "PC-01"

    public DateTime FechaInicio { get; set; }
    public DateTime FechaFin { get; set; }

    // Guardamos los importes por separado para análisis futuros
    public decimal ImporteTiempo { get; set; }
    public decimal ImporteExtra { get; set; } // Tienda + Extras manuales
        
    public decimal TotalCobrado { get; set; } // El total final con descuento aplicado

    public string MetodoPago { get; set; } = "Efectivo"; 

    // Helper para mostrar duración en la tabla
    public double DuracionMinutos => (FechaFin - FechaInicio).TotalMinutes;
}