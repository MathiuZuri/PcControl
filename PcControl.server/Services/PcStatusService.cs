using PcControl.Shared.Models;

namespace PcControl.Server.Services
{
    // Este servicio guardará en memoria quién está online
    public class PcStatusService
    {
        // Diccionario: NombrePC -> Datos de estado
        private readonly Dictionary<string, EstadoPcEnMemoria> _estados = new();

        public event Action? OnEstadoCambiado;

        public void ActualizarLatido(string nombrePc, bool pausada)
        {
            if (!_estados.ContainsKey(nombrePc))
            {
                _estados[nombrePc] = new EstadoPcEnMemoria();
            }

            _estados[nombrePc].UltimoLatido = DateTime.Now;
            _estados[nombrePc].EstaPausada = pausada;
            _estados[nombrePc].EstaOnline = true;

            OnEstadoCambiado?.Invoke();
        }

        public EstadoPcEnMemoria? ObtenerEstado(string nombrePc)
        {
            if (_estados.TryGetValue(nombrePc, out var estado))
            {
                // Si hace más de 10 segundos no hay latido, ya no está online
                if ((DateTime.Now - estado.UltimoLatido).TotalSeconds > 10)
                {
                    estado.EstaOnline = false;
                }
                return estado;
            }
            return null;
        }
    }

    public class EstadoPcEnMemoria
    {
        public DateTime UltimoLatido { get; set; }
        public bool EstaPausada { get; set; }
        public bool EstaOnline { get; set; }
    }
}