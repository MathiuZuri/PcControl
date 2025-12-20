namespace PcControl.Shared.DTOs;

public class ClienteInfoDto // Lo que ve el cliente en la pantalla de bloqueo
{
    public string Nombres { get; set; }
    public decimal SaldoActual { get; set; }
    public int TiempoRestanteMinutos { get; set; } // Calculado en base al saldo
}