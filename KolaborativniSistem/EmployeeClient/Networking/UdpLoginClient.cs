using System.Net;
using System.Net.Sockets;
using System.Text;

namespace EmployeeClient.Networking;

public static class UdpLoginClient
{
    public static string Login(string serverIp, int serverPort, string username)
    {
        using var client = new UdpClient();
        string message = $"ZAPOSLENI:{username}";

        byte[] data = Encoding.UTF8.GetBytes(message);

        client.Send(data, data.Length, serverIp, serverPort);

        IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
        byte[] responseData = client.Receive(ref remoteEndPoint);

        return Encoding.UTF8.GetString(responseData).Trim();
    }
}
