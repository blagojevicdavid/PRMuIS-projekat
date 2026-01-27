using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using CollaborativeServer.Core;
using Shared.Models;
using Shared.Protocol;

namespace CollaborativeServer.Networking
{
    public sealed class TcpServer
    {
        private readonly TaskStore _store;
        private readonly Dictionary<Socket, (bool Identified, ClientRole Role, string Username)> _clients = new();

        public TcpServer(TaskStore store)
        {
            _store = store;
        }

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
                        _clients[client] = (false, ClientRole.Menadzer, "");
                        Console.WriteLine("[TCP] Client connected");
                    }
                    else
                    {
                        if (!HandleClient(socket))
                        {
                            _clients.Remove(socket);
                            clients.Remove(socket);
                            try { socket.Close(); } catch { }
                            Console.WriteLine("[TCP] Client disconnected");
                        }
                    }
                }
            }
        }

        private bool HandleClient(Socket client)
        {
            var buffer = new byte[2048];

            try
            {
                int received = client.Receive(buffer);
                if (received == 0) return false;

                string message = Encoding.UTF8.GetString(buffer, 0, received).Trim();

                int nl = message.IndexOf('\n');
                if (nl >= 0) message = message.Substring(0, nl).Trim();

                if (_clients.TryGetValue(client, out var info) && !info.Identified)
                    return HandleIdentify(client, message);

                var (_, role, username) = _clients[client];

                if (role == ClientRole.Menadzer)
                    return HandleManagerMessage(client, username, message);

                if (role == ClientRole.Zaposleni)
                    return HandleEmployeeMessage(client, username, message);

                SendLine(client, "ERR");
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool HandleIdentify(Socket client, string message)
        {
            if (message.StartsWith(ProtocolConstants.UdpLoginManagerPrefix))
            {
                var username = message.Substring(ProtocolConstants.UdpLoginManagerPrefix.Length).Trim();
                _clients[client] = (true, ClientRole.Menadzer, username);

                _store.EnsureManager(username);

                Console.WriteLine($"[TCP] Identified manager: {username}");
                SendLine(client, "ID_OK");
                return true;
            }

            if (message.StartsWith(ProtocolConstants.UdpLoginEmployeePrefix))
            {
                var username = message.Substring(ProtocolConstants.UdpLoginEmployeePrefix.Length).Trim();
                _clients[client] = (true, ClientRole.Zaposleni, username);

                Console.WriteLine($"[TCP] Identified employee: {username}");
                SendLine(client, "ID_OK");
                return true;
            }

            SendLine(client, "ID_REQUIRED");
            return true;
        }

        private bool HandleManagerMessage(Socket client, string managerUsername, string message)
        {
            Console.WriteLine($"[TCP][MENADZER:{managerUsername}] {message}");

            if (message.StartsWith(ProtocolConstants.TcpSendPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var payload = message.Substring(ProtocolConstants.TcpSendPrefix.Length);
                var parts = payload.Split('|');

                if (parts.Length != 4)
                {
                    SendLine(client, "ERR_SEND_FORMAT");
                    return true;
                }

                string naziv = parts[0].Trim();
                string zaposleni = parts[1].Trim();

                if (!DateTime.TryParse(parts[2].Trim(), out var rok))
                {
                    SendLine(client, "ERR_BAD_DATE");
                    return true;
                }

                if (!int.TryParse(parts[3].Trim(), out var prioritet))
                {
                    SendLine(client, "ERR_BAD_PRIORITY");
                    return true;
                }

                var task = new ZadatakProjekta
                {
                    Naziv = naziv,
                    Zaposleni = zaposleni,
                    Rok = rok,
                    Prioritet = prioritet,
                    Status = StatusZadatka.NaCekanju,
                    Komentar = ""
                };

                _store.AddTask(managerUsername, task);

                SendLine(client, "OK");
                return true;
            }

            int idx = message.LastIndexOf(':');
            if (idx > 0)
            {
                string taskName = message.Substring(0, idx).Trim();
                string prStr = message.Substring(idx + 1).Trim();

                if (int.TryParse(prStr, out int newPr))
                {
                    bool ok = _store.TryIncreasePriority(managerUsername, taskName, newPr);
                    SendLine(client, ok ? "OK" : "ERR_NOT_FOUND");
                    return true;
                }
            }

            SendLine(client, "ERR_UNKNOWN");
            return true;
        }

        private bool HandleEmployeeMessage(Socket client, string employeeUsername, string message)
        {
            bool massageIsLIST = false;

            if (message.Equals("LIST", StringComparison.OrdinalIgnoreCase))
            {
                massageIsLIST = true;

                var tasks = _store.GetTasksForEmployeeWithManager(employeeUsername);

                if (tasks.Count == 0)
                {
                    SendLine(client, "NO_TASKS");
                    return true;
                }

                var payload = string.Join("^", tasks.ConvertAll(x =>
                    $"{x.Task.Naziv}|{x.ManagerUsername}|{x.Task.Rok:yyyy-MM-dd}|{x.Task.Prioritet}|{x.Task.Status}"
                ));

                SendLine(client, payload);
                return true;
            }

            if (!massageIsLIST)
                Console.WriteLine($"[TCP][ZAPOSLENI:{employeeUsername}] {message}");

            if (message.StartsWith(ProtocolConstants.TcpTakePrefix, StringComparison.OrdinalIgnoreCase))
            {
                string taskName = message.Substring(ProtocolConstants.TcpTakePrefix.Length).Trim();
                bool ok = _store.TrySetStatus(taskName, StatusZadatka.UToku);
                SendLine(client, ok ? "OK" : "ERR_NOT_FOUND");
                return true;
            }

            if (message.StartsWith(ProtocolConstants.TcpDonePrefix, StringComparison.OrdinalIgnoreCase))
            {
                string rest = message.Substring(ProtocolConstants.TcpDonePrefix.Length);

                
                var parts = rest.Split('|', 2);
                string taskName = parts[0].Trim();
                string? comment = parts.Length == 2 ? parts[1].Trim() : null;

                bool ok = _store.TryCompleteTask(taskName, comment);
                SendLine(client, ok ? "OK" : "ERR_NOT_FOUND");
                return true;
            }

            SendLine(client, "ERR_UNKNOWN");
            return true;
        }

        private static void SendLine(Socket client, string text)
        {
            client.Send(Encoding.UTF8.GetBytes(text + "\n"));
        }
    }
}
