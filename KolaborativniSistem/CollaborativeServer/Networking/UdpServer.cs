using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Shared.Protocol;
using Shared.Logging;
using System.Collections.Generic;
using System.Web;
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

            var buffer = new byte[65507];
            EndPoint remote = new IPEndPoint(IPAddress.Any, 0);

            while (true)
            {
                try
                {
                    int received = socket.ReceiveFrom(buffer, ref remote);
                    string message = Encoding.UTF8.GetString(buffer, 0, received).Trim();

                    string response = HandleMessage(message, tcpPort, remote?.ToString());

                    byte[] responseBytes = Encoding.UTF8.GetBytes(response);
                    socket.SendTo(responseBytes, remote);
                }
                catch (SocketException ex)
                {
                    AuditLogger.Warn($"UDP SocketException: {ex.SocketErrorCode} {ex.Message}");
                }
                catch (Exception ex)
                {
                    AuditLogger.Warn($"UDP error: {ex.Message}");
                }
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

                var tasks = _store.GetAllTasksForManager(username);
                string json = JsonSerializer.Serialize(tasks);

                MaybeLogAllTasks(username, json, tasks.Count);
                return $"{ProtocolConstants.UdpTaskPrefix}{json}";
            }

            if (message.StartsWith(ProtocolConstants.UdpChangePriorityPrefix))
            {
                // PRIORITY:manager:taskNameEncoded:newPriority
                string rest = message.Substring(ProtocolConstants.UdpChangePriorityPrefix.Length);

                // 3 dijela odvojena sa :
                var parts = rest.Split(':', 3, StringSplitOptions.None);
                if (parts.Length != 3)
                    return $"{ProtocolConstants.UdpErrPrefix}BAD_FORMAT";

                string manager = parts[0].Trim();
                string taskNameEncoded = parts[1].Trim();
                string priorityStr = parts[2].Trim();

                string taskName;
                try
                {
                    taskName = Uri.UnescapeDataString(taskNameEncoded);
                }
                catch
                {
                    return $"{ProtocolConstants.UdpErrPrefix}BAD_TASKNAME";
                }

                if (!int.TryParse(priorityStr, out int newPriority))
                    return $"{ProtocolConstants.UdpErrPrefix}BAD_PRIORITY";

                
                if (newPriority < 1) newPriority = 1;
                    bool ok = _store.TryChangeTaskPriority(manager, taskName, newPriority);

                if (!ok)
                    return $"{ProtocolConstants.UdpErrPrefix}NOT_FOUND";

                AuditLogger.Info($"PRIORITY changed (UDP): manager={manager}, task={taskName}, new={newPriority} ({remoteEndpoint})");
                return ProtocolConstants.UdpOk;
            }




            AuditLogger.Warn($"UDP unknown msg from {remoteEndpoint}: {message}");
            return "ERROR";
        }

        private void MaybeLogAllTasks(string manager, string payload, int count)
        {
            string sig = Hash(payload ?? "");

            lock (_sigLock)
            {
                if (_lastAllTasksSig.TryGetValue(manager, out var last) && last == sig)
                    return;

                _lastAllTasksSig[manager] = sig;
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
