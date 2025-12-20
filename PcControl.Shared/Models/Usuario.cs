namespace PcControl.Shared.Models;

public class Usuario
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
        
    // contraseñas se guarda el Hash.
    public string PasswordHash { get; set; } = string.Empty; 
        
    // Roles: "Admin", "Operador"
    public string Rol { get; set; } = "Operador"; 
        
    public bool Activo { get; set; } = true;
}