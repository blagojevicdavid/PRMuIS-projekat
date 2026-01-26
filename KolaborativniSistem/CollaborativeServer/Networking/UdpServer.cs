using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

using Shared.Protocol;

namespace CollaborativeServer.Networking
{
    public sealed class UdpServer
    {
        private readonly TaskStore _store;

        public UdpServer(TaskStore store)
        {
            _store = store;
        }

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
                string message = Encoding.UTF8.GetString(buffer, 0, received).Trim();

                Console.WriteLine($"[UDP] Received: {message}");

                string response = HandleMessage(message, tcpPort);

                byte[] responseBytes = Encoding.UTF8.GetBytes(response);
                socket.SendTo(responseBytes, remote);
            }
        }

        private string HandleMessage(string message, int tcpPort)
        {
            if (message.StartsWith(ProtocolConstants.UdpLoginManagerPrefix))
            {
                string username = message.Substring(ProtocolConstants.UdpLoginManagerPrefix.Length).Trim();
                Console.WriteLine($"[UDP] Manager login: {username}");

                
                _store.EnsureManager(username); //dodavanje u dictionary

                return $"{ProtocolConstants.UdpTcpInfoPrefix}{tcpPort}";
            }

            if (message.StartsWith(ProtocolConstants.UdpLoginEmployeePrefix))
            {
                string username = message.Substring(ProtocolConstants.UdpLoginEmployeePrefix.Length).Trim();
                Console.WriteLine($"[UDP] Employee login: {username}");

                // Za zaposlenog trenutno ne pravimo entry u Dictionary (spec traži samo za menadžera)
                return $"{ProtocolConstants.UdpTcpInfoPrefix}{tcpPort}";
            }

            return "ERROR";
        }
    }
}
