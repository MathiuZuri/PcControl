using System.ComponentModel.DataAnnotations.Schema;

namespace PcControl.Shared.Models;

// Esta clase representa cada PC en tu local
public class Computadora
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty; // Ej: "PC-Gamer-01"
    public string IpAddress { get; set; } = string.Empty; // Para identificarla en la red
    public string MacAddress { get; set; } = string.Empty; // Identificador único físico
    
    [NotMapped] // No se guarda en BD, vive en la memoria RAM del servidor
    public DateTime UltimoLatido { get; set; } = DateTime.MinValue;
    
    [NotMapped]
    public bool EstaOnline => (DateTime.Now - UltimoLatido).TotalSeconds < 45;
        
    [NotMapped]
    public string? UltimaCapturaBase64 { get; set; }
    
    [NotMapped]
    public bool EstaPausada { get; set; } = false;

    // Estado actual: "Disponible", "Ocupada", "Apagada"
    public string Estado { get; set; } = "Disponible";

    // Datos de la sesión actual (si está ocupada)
    public DateTime? HoraInicio { get; set; }
    public DateTime? HoraFin { get; set; }

    [Column(TypeName = "decimal(18,2)")] public decimal TarifaPorHora { get; set; } = 2.50m;

    public int? TiempoLimiteMinutos { get; set; } // Tiempo máximo (null = libre)

    [Column(TypeName = "decimal(18,2)")]
    public decimal ImporteExtra { get; set; } = 0; // Para cobrar impresiones, snacks, etc. dentro de la sesión

    public string? NotaAdmin { get; set; } // Notas internas

    // 1. Calcula cuánto debe SOLO por el tiempo transcurrido
    [NotMapped]
    public decimal ImportePorTiempo { get; set; }

    // 2. Suma el tiempo + los extras (Gaseosas, impresiones)
    [NotMapped] public decimal TotalGeneral => ImportePorTiempo + ImporteExtra;

    // 3. Calcula cuántos minutos le quedan (si puso límite)
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
