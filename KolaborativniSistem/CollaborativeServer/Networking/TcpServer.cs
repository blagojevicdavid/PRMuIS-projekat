using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Shared.Models;
using Shared.Protocol;
using System.Text.Json;
using Shared.Logging;
using System.Linq;
using System.Security.Cryptography;

namespace CollaborativeServer.Networking
{
    public sealed class TcpServer
    {
        private readonly TaskStore _store;
        private readonly Dictionary<Socket, (bool Identified, ClientRole Role, string Username)> _clients = new();

        private readonly Dictionary<string, string> _lastEmployeeListSig = new();
        private readonly object _sigLock = new();

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
            AuditLogger.Info($"TCP start {bindIp}:{tcpPort}");

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

                SendLine(client, "ERR", "ERR");
                return true;
            }
            catch (Exception ex)
            {
                AuditLogger.Error("TcpServer.HandleClient", ex);
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
                AuditLogger.Info($"MANAGER login (TCP): {username}");

                SendLine(client, "ID_OK", "ID_OK");
                return true;
            }

            if (message.StartsWith(ProtocolConstants.UdpLoginEmployeePrefix))
            {
                var username = message.Substring(ProtocolConstants.UdpLoginEmployeePrefix.Length).Trim();
                _clients[client] = (true, ClientRole.Zaposleni, username);

                Console.WriteLine($"[TCP] Identified employee: {username}");
                AuditLogger.Info($"EMPLOYEE login (TCP): {username}");

                SendLine(client, "ID_OK", "ID_OK");
                return true;
            }

            SendLine(client, "ID_REQUIRED", "ID_REQUIRED");
            return true;
        }

        private bool HandleManagerMessage(Socket client, string managerUsername, string message)
        {
            Console.WriteLine($"[TCP][MENADZER:{managerUsername}] {message}");

            if (message.StartsWith(ProtocolConstants.TcpSendPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var json = message.Substring(ProtocolConstants.TcpSendPrefix.Length).Trim();

                ZadatakProjekta? task;
                try
                {
                    task = JsonSerializer.Deserialize<ZadatakProjekta>(json);
                }
                catch
                {
                    SendLine(client, "ERR_SEND_FORMAT", "ERR_SEND_FORMAT");
                    return true;
                }

                if (task == null || string.IsNullOrWhiteSpace(task.Naziv) || string.IsNullOrWhiteSpace(task.Zaposleni))
                {
                    SendLine(client, "ERR_SEND_FORMAT", "ERR_SEND_FORMAT");
                    return true;
                }

                task.Naziv = task.Naziv.Trim();
                task.Zaposleni = task.Zaposleni.Trim();
                task.Status = StatusZadatka.NaCekanju;
                task.Komentar ??= string.Empty;

                _store.AddTask(managerUsername, task);

                AuditLogger.Info($"TASK created: '{task.Naziv}' -> {task.Zaposleni} (manager={managerUsername}, rok={task.Rok:yyyy-MM-dd}, prioritet={task.Prioritet})");

                SendLine(client, "OK", "OK");
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

                    if (ok)
                        AuditLogger.Info($"PRIORITY changed: '{taskName}' -> {newPr} (manager={managerUsername})");
                    else
                        AuditLogger.Warn($"PRIORITY change failed: '{taskName}' not found (manager={managerUsername})");

                    SendLine(client, ok ? "OK" : "ERR_NOT_FOUND", ok ? "OK" : "ERR_NOT_FOUND");
                    return true;
                }
            }

            SendLine(client, "ERR_UNKNOWN", "ERR_UNKNOWN");
            return true;
        }

        private bool HandleEmployeeMessage(Socket client, string employeeUsername, string message)
        {
            if (!string.IsNullOrWhiteSpace(message) && message.TrimStart().StartsWith("{"))
            {
                try
                {
                    using var doc = JsonDocument.Parse(message);
                    var root = doc.RootElement;

                    string type = root.TryGetProperty("type", out var t) ? (t.GetString() ?? "") : "";

                    if (type.Equals("employee_list", StringComparison.OrdinalIgnoreCase))
                    {
                        return HandleEmployeeList(client, employeeUsername, mode: "JSON");
                    }

                    if (type.Equals("employee_action", StringComparison.OrdinalIgnoreCase))
                    {
                        string action = root.TryGetProperty("action", out var a) ? (a.GetString() ?? "") : "";
                        string taskName = root.TryGetProperty("taskName", out var tn) ? (tn.GetString() ?? "") : "";

                        if (string.IsNullOrWhiteSpace(taskName))
                        {
                            SendLine(client, "ERR_BAD_REQUEST", "ERR_BAD_REQUEST");
                            return true;
                        }

                        if (action.Equals("take", StringComparison.OrdinalIgnoreCase))
                        {
                            bool ok = _store.TrySetStatus(taskName.Trim(), StatusZadatka.UToku);

                            if (ok)
                                AuditLogger.Info($"STATUS: {employeeUsername} preuzeo '{taskName.Trim()}' -> UToku");
                            else
                                AuditLogger.Warn($"STATUS take failed: '{taskName.Trim()}' not found (employee={employeeUsername})");

                            SendLine(client, ok ? "OK" : "ERR_NOT_FOUND", ok ? "OK" : "ERR_NOT_FOUND");
                            return true;
                        }

                        if (action.Equals("finish", StringComparison.OrdinalIgnoreCase))
                        {
                            string? comment = null;
                            if (root.TryGetProperty("comment", out var c) && c.ValueKind != JsonValueKind.Null)
                                comment = c.GetString();

                            bool ok = _store.TryCompleteTask(taskName.Trim(), comment);

                            if (ok)
                                AuditLogger.Info($"STATUS: {employeeUsername} zavrsio '{taskName.Trim()}' -> Zavrsen komentar='{(comment ?? "").Trim()}'");
                            else
                                AuditLogger.Warn($"STATUS finish failed: '{taskName.Trim()}' not found (employee={employeeUsername})");

                            SendLine(client, ok ? "OK" : "ERR_NOT_FOUND", ok ? "OK" : "ERR_NOT_FOUND");
                            return true;
                        }

                        SendLine(client, "ERR_UNKNOWN_ACTION", "ERR_UNKNOWN_ACTION");
                        return true;
                    }

                    SendLine(client, "ERR_UNKNOWN_JSON", "ERR_UNKNOWN_JSON");
                    return true;
                }
                catch
                {
                    SendLine(client, "ERR_JSON", "ERR_JSON");
                    return true;
                }
            }

           
            if (message.Equals("LIST", StringComparison.OrdinalIgnoreCase))
            {
                return HandleEmployeeList(client, employeeUsername, mode: "LEGACY");
            }

            Console.WriteLine($"[TCP][ZAPOSLENI:{employeeUsername}] {message}");

            if (message.StartsWith(ProtocolConstants.TcpTakePrefix, StringComparison.OrdinalIgnoreCase))
            {
                string taskName = message.Substring(ProtocolConstants.TcpTakePrefix.Length).Trim();
                bool ok = _store.TrySetStatus(taskName, StatusZadatka.UToku);

                if (ok)
                    AuditLogger.Info($"STATUS: {employeeUsername} preuzeo '{taskName}' -> UToku");
                else
                    AuditLogger.Warn($"STATUS take failed: '{taskName}' not found (employee={employeeUsername})");

                SendLine(client, ok ? "OK" : "ERR_NOT_FOUND", ok ? "OK" : "ERR_NOT_FOUND");
                return true;
            }

            if (message.StartsWith(ProtocolConstants.TcpDonePrefix, StringComparison.OrdinalIgnoreCase))
            {
                string rest = message.Substring(ProtocolConstants.TcpDonePrefix.Length);

                var parts = rest.Split('|', 2);
                string taskName = parts[0].Trim();
                string? comment = parts.Length == 2 ? parts[1].Trim() : null;

                bool ok = _store.TryCompleteTask(taskName, comment);

                if (ok)
                    AuditLogger.Info($"STATUS: {employeeUsername} zavrsio '{taskName}' -> Zavrsen komentar='{(comment ?? "").Trim()}'");
                else
                    AuditLogger.Warn($"STATUS finish failed: '{taskName}' not found (employee={employeeUsername})");

                SendLine(client, ok ? "OK" : "ERR_NOT_FOUND", ok ? "OK" : "ERR_NOT_FOUND");
                return true;
            }

            SendLine(client, "ERR_UNKNOWN", "ERR_UNKNOWN");
            return true;
        }

        private bool HandleEmployeeList(Socket client, string employeeUsername, string mode)
        {
            var tasks = _store.GetTasksForEmployeeWithManager(employeeUsername);

            if (mode == "JSON")
            {
                var payload = new
                {
                    type = "employee_list_reply",
                    tasks = tasks.Select(x => new
                    {
                        naziv = x.Task.Naziv,
                        menadzer = x.ManagerUsername,
                        rok = x.Task.Rok.ToString("yyyy-MM-dd"),
                        prioritet = x.Task.Prioritet.ToString(),
                        status = x.Task.Status.ToString()
                    }).ToList()
                };

                SendLine(client, JsonSerializer.Serialize(payload), "TASK_LIST_REPLY");
            }
            else
            {
                if (tasks.Count == 0)
                {
                    SendLine(client, "NO_TASKS", "NO_TASKS");
                    MaybeLogTasksDelivered(employeeUsername, tasks, mode);
                    return true;
                }

                var payload = string.Join("^", tasks.ConvertAll(x =>
                    $"{x.Task.Naziv}|{x.ManagerUsername}|{x.Task.Rok:yyyy-MM-dd}|{x.Task.Prioritet}|{x.Task.Status}"
                ));

                SendLine(client, payload, "TASK_LIST_REPLY");
            }

            MaybeLogTasksDelivered(employeeUsername, tasks, mode);
            return true;
        }

        private void MaybeLogTasksDelivered(string employee, List<(string ManagerUsername, ZadatakProjekta Task)> tasks, string mode)
        {
            string signature = BuildSignature(tasks);

            lock (_sigLock)
            {
                if (_lastEmployeeListSig.TryGetValue(employee, out var last) && last == signature)
                    return;

                _lastEmployeeListSig[employee] = signature;
            }

            if (tasks.Count == 0)
            {
                AuditLogger.Info($"TASKS delivered to {employee}: 0");
                return;
            }

            var names = tasks.Select(t => t.Task.Naziv).Distinct().ToList();
            string preview = string.Join(", ", names.Take(8));
            if (names.Count > 8) preview += $" ... (+{names.Count - 8})";

            AuditLogger.Info($"TASKS delivered to {employee}: {tasks.Count} -> {preview}");

        }

        private static string BuildSignature(List<(string ManagerUsername, ZadatakProjekta Task)> tasks)
        {
            var raw = string.Join(";", tasks.Select(t =>
                $"{t.Task.Naziv}|{t.ManagerUsername}|{t.Task.Rok:yyyy-MM-dd}|{t.Task.Prioritet}|{t.Task.Status}"
            ));

            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(hash);
        }

        
        private static void SendLine(Socket client, string text, string type)
        {
            client.Send(Encoding.UTF8.GetBytes(text + "\n"));
        }
    }
}
