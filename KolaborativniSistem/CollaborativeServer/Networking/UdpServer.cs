using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

using Shared.Protocol;
using Shared.Logging;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace CollaborativeServer.Networking
{
    public sealed class UdpServer
    {
        private readonly TaskStore _store;

        private readonly Dictionary<string, string> _lastAllTasksSig = new();
        private readonly object _sigLock = new();

        public UdpServer(TaskStore store)
        {
            _store = store;
        }

        public void Start(string bindIp, int udpPort, int tcpPort)
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Bind(new IPEndPoint(IPAddress.Parse(bindIp), udpPort));

            Console.WriteLine($"[UDP] Listening on {bindIp}:{udpPort}");
            AuditLogger.Info($"UDP start {bindIp}:{udpPort}");

            var buffer = new byte[1024];
            EndPoint remote = new IPEndPoint(IPAddress.Any, 0);

            while (true)
            {
                int received = socket.ReceiveFrom(buffer, ref remote);
                string message = Encoding.UTF8.GetString(buffer, 0, received).Trim();

                string response = HandleMessage(message, tcpPort, remote?.ToString());

                byte[] responseBytes = Encoding.UTF8.GetBytes(response);
                socket.SendTo(responseBytes, remote);
            }
        }

        private string HandleMessage(string message, int tcpPort, string? remoteEndpoint)
        {
            if (message.StartsWith(ProtocolConstants.UdpLoginManagerPrefix))
            {
                string username = message.Substring(ProtocolConstants.UdpLoginManagerPrefix.Length).Trim();
                Console.WriteLine($"[UDP] Manager login: {username}");

                _store.EnsureManager(username);

                AuditLogger.Info($"MANAGER login (UDP): {username} ({remoteEndpoint})");
                return $"{ProtocolConstants.UdpTcpInfoPrefix}{tcpPort}";
            }

            if (message.StartsWith(ProtocolConstants.UdpLoginEmployeePrefix))
            {
                string username = message.Substring(ProtocolConstants.UdpLoginEmployeePrefix.Length).Trim();
                Console.WriteLine($"[UDP] Employee login: {username}");

                AuditLogger.Info($"EMPLOYEE login (UDP): {username} ({remoteEndpoint})");
                return $"{ProtocolConstants.UdpTcpInfoPrefix}{tcpPort}";
            }

            if (message.StartsWith(ProtocolConstants.UdpAllTasksPrefix))
            {
                string username = message.Substring(ProtocolConstants.UdpAllTasksPrefix.Length).Trim();

                string allTasks = _store.GetAllTasks(username);
                MaybeLogAllTasks(username, allTasks);

                return $"{ProtocolConstants.UdpTaskPrefix}{allTasks}";
            }

            
            AuditLogger.Warn($"UDP unknown msg from {remoteEndpoint}: {message}");
            return "ERROR";
        }

        private void MaybeLogAllTasks(string manager, string allTasks)
        {
            string sig = Hash(allTasks ?? "");

            lock (_sigLock)
            {
                if (_lastAllTasksSig.TryGetValue(manager, out var last) && last == sig)
                    return; 

                _lastAllTasksSig[manager] = sig;
            }

           
            int count = 0;
            if (!string.IsNullOrWhiteSpace(allTasks))
            {
                count = allTasks.Split(';', StringSplitOptions.RemoveEmptyEntries).Length;
            }

            AuditLogger.Info($"MANAGER ALL TASKS changed (UDP): {manager} (count={count})");
        }

        private static string Hash(string s)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(s));
            return Convert.ToHexString(bytes);
        }
    }
}
