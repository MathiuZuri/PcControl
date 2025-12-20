namespace PcControl.Shared.DTOs;

public class UsuarioDto // Para mostrar en la lista de empleados (SIN PASSWORD)
{
    public int Id { get; set; }
    public string Username { get; set; }
    public string NombreCompleto { get; set; }
    public string Rol { get; set; }
}