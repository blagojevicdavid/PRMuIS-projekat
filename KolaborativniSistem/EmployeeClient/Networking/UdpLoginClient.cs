using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace EmployeeClient.Networking
{
    public class UdpLoginClient
    {
        private readonly string _serverIp = "127.0.0.1";
        private readonly int _serverPort = 5000;

        public bool Login(string username, string password)
        {
            using (UdpClient client = new UdpClient())
            {
                string message = $"ZAPOSLENI|{username}|{password}";   //treba li uopste password?
                byte[] data = Encoding.UTF8.GetBytes(message);

                client.Send(data, data.Length, _serverIp, _serverPort);

                IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                byte[] responseData = client.Receive(ref remoteEndPoint);
                string response = Encoding.UTF8.GetString(responseData);

                return response == "OK";
            }
        }
    }
}
