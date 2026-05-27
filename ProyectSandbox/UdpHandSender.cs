using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ProyectoSandbox
{
    /// <summary>
    /// Serializa HandData como JSON compacto y lo envía por UDP.
    /// Formato: {"t":1,"x":0.5,"y":0.3,"z":1.1,"f":3,"s":2,"sw":0}
    ///   t  = IsTracked (0/1)
    ///   x,y = posición normalizada [0-1]
    ///   z   = profundidad en metros
    ///   f   = fingers (0-5)
    ///   s   = HandState (0=Unknown,1=Open,2=Closed,3=Lasso)
    ///   sw  = SwipeDirection (0=None,1=Left,2=Right,3=Up,4=Down)
    /// </summary>
    public class UdpHandSender : IDisposable
    {
        private readonly UdpClient _client;
        private readonly IPEndPoint _endpoint;

        public UdpHandSender(string ip, int port)
        {
            _client = new UdpClient();
            _endpoint = new IPEndPoint(IPAddress.Parse(ip), port);
        }

        public void Send(HandData hand)
        {
            try
            {
                // JSON manual — sin dependencias extra, paquete < 100 bytes
                string json = string.Format(
                    "{{\"t\":{0},\"x\":{1:F4},\"y\":{2:F4},\"z\":{3:F4},\"f\":{4},\"s\":{5},\"sw\":{6}}}",
                    hand.IsTracked ? 1 : 0,
                    hand.X,
                    hand.Y,
                    hand.Z,
                    hand.Fingers,
                    (int)hand.State,
                    (int)hand.Swipe);

                byte[] data = Encoding.UTF8.GetBytes(json);
                _client.Send(data, data.Length, _endpoint);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[UdpHandSender] Error: {ex.Message}");
            }
        }

        public void Dispose() => _client?.Dispose();
    }
}