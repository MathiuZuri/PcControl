using System.ComponentModel.DataAnnotations.Schema;

namespace PcControl.Shared.Models;

public class Computadora
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty; 
    public string IpAddress { get; set; } = string.Empty; 
    public string MacAddress { get; set; } = string.Empty; 
    
    [NotMapped] 
    public DateTime UltimoLatido { get; set; } = DateTime.MinValue;
    
    [NotMapped]
    public bool EstaOnline => (DateTime.Now - UltimoLatido).TotalSeconds < 45;
        
    [NotMapped]
    public string? UltimaCapturaBase64 { get; set; }
    
    [NotMapped]
    public bool EstaPausada { get; set; } = false;
    
    public string Estado { get; set; } = "Disponible";
    
    public DateTime? HoraInicio { get; set; }
    public DateTime? HoraFin { get; set; }

    [Column(TypeName = "decimal(18,2)")] public decimal TarifaPorHora { get; set; } = 2.50m;

    public int? TiempoLimiteMinutos { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ImporteExtra { get; set; } 

    public string? NotaAdmin { get; set; }
    
    [NotMapped]
    public decimal ImportePorTiempo { get; set; }
    
    [NotMapped] public decimal TotalGeneral => ImportePorTiempo + ImporteExtra;
    
    [NotMapped]
    public int MinutosRestantes
    {
        get
        {
            if (HoraInicio != null && TiempoLimiteMinutos.HasValue)
            {
                var minutosUsados = (int)(DateTime.Now - HoraInicio.Value).TotalMinutes;
                var restantes = TiempoLimiteMinutos.Value - minutosUsados;
                return restantes > 0 ? restantes : 0;
            }

            return 0;
        }
    }
    [NotMapped] 
    public List<ProductoConsumo> Consumos { get; set; } = new();
    
    public DateTime? InicioPausa { get; set; }
    
}
