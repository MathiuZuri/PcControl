using System.Net;
using System.Net.Sockets;
using System.Text;

namespace PcControl.Server.Services
{
    public class UdpDiscoveryService : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Puerto para descubrimiento
            int port = 8888; 
            
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var udp = new UdpClient())
                    {
                        udp.EnableBroadcast = true;
                        
                        // Obtenemos la IP local real
                        string ipLocal = GetLocalIp();
                        
                        // Mensaje: "CIBER_SERVER|192.168.1.XX"
                        byte[] data = Encoding.UTF8.GetBytes($"CIBER_SERVER|{ipLocal}");
                        
                        // Enviamos a toda la red (255.255.255.255)
                        await udp.SendAsync(data, data.Length, new IPEndPoint(IPAddress.Broadcast, port));
                    }
                }
                catch { /* Ignorar errores de red */ }

                await Task.Delay(2000, stoppingToken); // Repetir cada 2 segundos
            }
        }

        private string GetLocalIp()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork) return ip.ToString();
            }
            return "127.0.0.1";
        }
    }
}