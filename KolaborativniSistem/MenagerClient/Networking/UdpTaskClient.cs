using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Shared.Models;
using Shared.Protocol;

namespace ManagerClient.Networking
{
    public static class UdpTasksClient
    {
        public static List<ZadatakProjekta> GetAllForManager(string serverIp, int udpPort, string managerUsername)
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.ReceiveTimeout = 1500;

            var remote = new IPEndPoint(IPAddress.Parse(serverIp), udpPort);

            string req = ProtocolConstants.UdpAllTasksPrefix + managerUsername;
            socket.SendTo(Encoding.UTF8.GetBytes(req), remote);

            var buffer = new byte[65507]; // buffer podesen na maksimalnu velicinu UDP payload-a
            EndPoint from = new IPEndPoint(IPAddress.Any, 0);

            int received = socket.ReceiveFrom(buffer, ref from);
            string resp = Encoding.UTF8.GetString(buffer, 0, received).Trim();

            if (!resp.StartsWith(ProtocolConstants.UdpTaskPrefix))
                return new List<ZadatakProjekta>();

            string payload = resp.Substring(ProtocolConstants.UdpTaskPrefix.Length).Trim();
            if (string.IsNullOrWhiteSpace(payload))
                return new List<ZadatakProjekta>();

            //prebaceno na JSON
            try
            {
                var tasks = JsonSerializer.Deserialize<List<ZadatakProjekta>>(payload);
                return tasks ?? new List<ZadatakProjekta>();
            }
            catch (JsonException)
            {
                //ako server vrati nevalidan JSON
                return new List<ZadatakProjekta>();
            }
        }

        public static bool ChangePriority(string serverIp, int udpPort, string managerUsername, string taskName, int newPriority)
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.ReceiveTimeout = 1500;

            var remote = new IPEndPoint(IPAddress.Parse(serverIp), udpPort);

            string safeName = Uri.EscapeDataString(taskName ?? "");
            string req = $"{ProtocolConstants.UdpChangePriorityPrefix}{managerUsername}:{safeName}:{newPriority}";

            // retry 2 puta
            for (int attempt = 0; attempt < 2; attempt++)
            {
                try
                {
                    socket.SendTo(Encoding.UTF8.GetBytes(req), remote);

                    var buffer = new byte[2048];
                    EndPoint from = new IPEndPoint(IPAddress.Any, 0);
                    int received = socket.ReceiveFrom(buffer, ref from);

                    string resp = Encoding.UTF8.GetString(buffer, 0, received).Trim();
                    if (resp == ProtocolConstants.UdpOk)
                        return true;

                    return false;
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
                {
                }
            }

            return false;
        }

    }
}
