using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace CollaborativeServer.Networking
{
    public sealed class TcpServer
    {
        private readonly Dictionary<Socket, (string Role, string Username)> _clients = new();

        public void Start(string bindIp, int tcpPort)
        {
            var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listener.Bind(new IPEndPoint(IPAddress.Parse(bindIp), tcpPort));
            listener.Listen(10);

            Console.WriteLine($"[TCP] Listening on {bindIp}:{tcpPort}");

            var clients = new List<Socket>();

            while (true)
            {
                var readList = new List<Socket>(clients) { listener };

                Socket.Select(readList, null, null, 1_000_000);

                foreach (var socket in readList)
                {
                    if (socket == listener)
                    {
                        var client = listener.Accept();
                        clients.Add(client);
                        _clients[client] = ("", ""); // Placeholder
                        Console.WriteLine("[TCP] Client connected");
                    }
                    else
                    {
                        if (!HandleClient(socket))
                        {
                            _clients.Remove(socket);
                            clients.Remove(socket);
                            socket.Close();
                            Console.WriteLine("[TCP] Client disconnected");
                        }
                    }
                }
            }
        }

        private bool HandleClient(Socket client)
        {
            var buffer = new byte[1024];

            try
            {
                int received = client.Receive(buffer);
                if (received == 0)
                    return false;

                string message = Encoding.UTF8.GetString(buffer, 0, received);

                message = message.Trim();

                if (_clients.TryGetValue(client, out var info) && string.IsNullOrEmpty(info.Role))
                {
                    if (message.StartsWith("MENADZER:"))
                    {
                        var username = message.Substring("MENADZER:".Length).Trim();
                        _clients[client] = ("MENADZER", username);
                        Console.WriteLine($"[TCP] Identified manager: {username}");
                        client.Send(Encoding.UTF8.GetBytes("ID_OK\n"));
                        return true;
                    }


                    if (message.StartsWith("ZAPOSLENI:"))
                    {
                        var username = message.Substring("ZAPOSLENI:".Length).Trim();
                        _clients[client] = ("ZAPOSLENI", username);
                        Console.WriteLine($"[TCP] Identified employee: {username}");
                        client.Send(Encoding.UTF8.GetBytes("ID_OK\n"));
                        return true;
                    }

                    client.Send(Encoding.UTF8.GetBytes("ID_REQUIRED\n"));
                    return true;
                }

                Console.WriteLine($"[TCP] Received: {message}");

                // echo test
                client.Send(Encoding.UTF8.GetBytes($"OK  Recieved: {message}"));

                return true;
            }
            catch
            {
                return false;
            }
        }

    }
}
