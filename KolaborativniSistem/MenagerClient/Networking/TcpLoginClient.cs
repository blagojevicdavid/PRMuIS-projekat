using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ManagerClient.Networking
{
    public sealed class TcpLoginClient : IDisposable
    {
        private Socket? _sock;

        public bool IsConnected => _sock != null && _sock.Connected;

        // async wrapper da ne blokira ui
        public Task ConnectAndIdentifyAsync(string serverIp, int tcpPort, string username, CancellationToken ct = default)
            => Task.Run(() => ConnectAndIdentify(serverIp, tcpPort, username), ct);

        public void ConnectAndIdentify(string serverIp, int tcpPort, string username)
        {
            Dispose();

            var ip = IPAddress.Parse(serverIp);
            _sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            // da ne visi zauvek
            _sock.ReceiveTimeout = 5000;
            _sock.SendTimeout = 5000;

            _sock.Connect(new IPEndPoint(ip, tcpPort));

            // identifikacija
            var msg = $"MENADZER:{username}\n";
            var data = Encoding.UTF8.GetBytes(msg);
            _sock.Send(data);

            // odgovor
            var buf = new byte[128];
            int n = _sock.Receive(buf);
            if (n == 0) throw new Exception("Server je zatvorio konekciju.");

            var reply = Encoding.UTF8.GetString(buf, 0, n).Trim();

            if (!reply.Equals("ID_OK", StringComparison.OrdinalIgnoreCase))
            {
                Dispose();
                throw new Exception($"TCP identifikacija neuspešna. Odgovor: {reply}");
            }
        }

        
        public void Send(string text)
        {
            if (_sock == null) throw new Exception("TCP nije povezan.");

            var data = Encoding.UTF8.GetBytes(text);
            _sock.Send(data);
        }


        public void Dispose()
        {
            try
            {
                if (_sock != null)
                {
                    try { _sock.Shutdown(SocketShutdown.Both); } catch { }
                    try { _sock.Close(); } catch { }
                    try { _sock.Dispose(); } catch { }
                }
            }
            finally
            {
                _sock = null;
            }
        }
    }
}
