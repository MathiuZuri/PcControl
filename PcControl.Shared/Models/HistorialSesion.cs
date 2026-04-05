namespace PcControl.Shared.Models;

public class HistorialSesion
{
    public int Id { get; set; }

    public string PcNombre { get; set; } = "";

    public DateTime FechaInicio { get; set; }
    public DateTime FechaFin { get; set; }
    
    public decimal ImporteTiempo { get; set; }
    public decimal ImporteExtra { get; set; }
        
    public decimal TotalCobrado { get; set; } 
    public string MetodoPago { get; set; } = "Efectivo"; 
    
    public double DuracionMinutos => (FechaFin - FechaInicio).TotalMinutes;
}