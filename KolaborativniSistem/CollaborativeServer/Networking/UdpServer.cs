using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace CollaborativeServer.Networking
{
    public sealed class UdpServer
    {
        
        public void Start(string bindIp, int udpPort, int tcpPort)
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Bind(new IPEndPoint(IPAddress.Parse(bindIp), udpPort));

            Console.WriteLine($"[UDP] Listening on {bindIp}:{udpPort}");

            var buffer = new byte[1024];
            EndPoint remote = new IPEndPoint(IPAddress.Any, 0);

            while (true)
            {
                int received = socket.ReceiveFrom(buffer, ref remote);
                string message = Encoding.UTF8.GetString(buffer, 0, received);

                Console.WriteLine($"[UDP] Received: {message}");

                // Obrada poruke
                string response = HandleMessage(message, tcpPort);

                byte[] responseBytes = Encoding.UTF8.GetBytes(response);
                socket.SendTo(responseBytes, remote);
            }
        }

        private string HandleMessage(string message, int tcpPort)
        {
            if (message.StartsWith("MENADZER:"))
            {
                string username = message.Substring("MENADZER:".Length);
                Console.WriteLine($"[UDP] Manager login: {username}");
                return $"TCP:{tcpPort}";
            }

            if (message.StartsWith("ZAPOSLENI:"))
            {
                string username = message.Substring("ZAPOSLENI:".Length);
                Console.WriteLine($"[UDP] Employee login: {username}");
                return $"TCP:{tcpPort}";
            }

            return "ERROR";
        }


    }
}
