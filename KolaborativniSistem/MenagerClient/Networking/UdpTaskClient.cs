using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

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

            var buffer = new byte[8192];
            EndPoint from = new IPEndPoint(IPAddress.Any, 0);

            int received = socket.ReceiveFrom(buffer, ref from);
            string resp = Encoding.UTF8.GetString(buffer, 0, received).Trim();

            if (!resp.StartsWith(ProtocolConstants.UdpTaskPrefix))
                return new List<ZadatakProjekta>();

            string payload = resp.Substring(ProtocolConstants.UdpTaskPrefix.Length);
            if (string.IsNullOrWhiteSpace(payload))
                return new List<ZadatakProjekta>();

            var result = new List<ZadatakProjekta>();

            var taskStrings = payload.Split(';', StringSplitOptions.RemoveEmptyEntries);

            foreach (var taskStr in taskStrings)
            {
                // separator je '|'
                var parts = taskStr.Split('|');

                if (parts.Length != 5 && parts.Length != 6) continue;

                string naziv = parts[0].Trim();
                string zaposleni = parts[1].Trim();

                if (!DateTime.TryParse(parts[2].Trim(), out var rok)) continue;
                if (!int.TryParse(parts[3].Trim(), out var prioritet)) continue;
                if (!int.TryParse(parts[4].Trim(), out var st)) continue;

                string komentar = parts.Length == 6 ? (parts[5] ?? "").Trim() : "";

                result.Add(new ZadatakProjekta
                {
                    Naziv = naziv,
                    Zaposleni = zaposleni,
                    Rok = rok,
                    Prioritet = prioritet,
                    Status = (StatusZadatka)st,
                    Komentar = komentar
                });
            }

            return result;
        }
    }
}
