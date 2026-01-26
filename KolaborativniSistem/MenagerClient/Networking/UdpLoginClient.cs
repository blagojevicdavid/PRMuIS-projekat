using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Net;
using System.Net.Sockets;
using Shared.Protocol;

namespace ManagerClient.Networking;

public sealed class UdpLoginClient
{
    public static string Login(string serverIp, int udpPort, string username)
    {
        using var client = new UdpClient();

        var serverEndpoint = new IPEndPoint(IPAddress.Parse(serverIp), udpPort);

        string message = ProtocolConstants.UdpLoginManagerPrefix + username;
        byte[] data = Encoding.UTF8.GetBytes(message);

        client.Send(data, data.Length, serverEndpoint);

        // Čekamo odgovor servera
        var remote = new IPEndPoint(IPAddress.Any, 0);
        byte[] responseBytes = client.Receive(ref remote);

        return Encoding.UTF8.GetString(responseBytes);
    }
}
