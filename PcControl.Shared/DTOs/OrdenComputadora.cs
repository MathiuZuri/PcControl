namespace PcControl.Shared.DTOs;
public enum TipoAccion
{
    Bloquear,
    Desbloquear,
    Apagar,
    Reiniciar,
    EnviarMensaje,
    ActualizarTarifa
}

public class OrdenComputadora
{
    public TipoAccion Accion { get; set; }
    public int? TiempoMinutos { get; set; } // Opcional: "Desbloquear por 30 mins"
    public string? MensajeExtra { get; set; } // Ej: "Te quedan 5 minutos"
}
