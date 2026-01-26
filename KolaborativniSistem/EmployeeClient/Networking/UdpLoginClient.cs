using System.Net;
using System.Net.Sockets;
using System.Text;
using Shared.Protocol;

namespace EmployeeClient.Networking;

public sealed class UdpLoginClient
{
    public static string Login(string serverIp, int udpPort, string username)
    {
        using var client = new UdpClient();
        var serverEndpoint = new IPEndPoint(IPAddress.Parse(serverIp), udpPort);

        string message = ProtocolConstants.UdpLoginEmployeePrefix + username;
        byte[] data = Encoding.UTF8.GetBytes(message);

        client.Send(data, data.Length, serverEndpoint);

        var remote = new IPEndPoint(IPAddress.Any, 0);
        byte[] responseBytes = client.Receive(ref remote);

        return Encoding.UTF8.GetString(responseBytes).Trim();
    }
}
