namespace PcControl.Server.Services
{
    public class UiRefreshService
    {
        // Evento que las páginas escucharán
        public event Func<Task>? OnRefreshRequested;

        // Método que llamará el botón del MainLayout
        public async Task TriggerRefreshAsync()
        {
            if (OnRefreshRequested != null)
            {
                // Invoca a todos los suscriptores (las páginas abiertas)
                await OnRefreshRequested.Invoke();
            }
        }
    }
}