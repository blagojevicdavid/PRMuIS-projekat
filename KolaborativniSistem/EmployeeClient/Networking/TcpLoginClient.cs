using Shared.Protocol;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace EmployeeClient.Networking
{
    public sealed class TcpLoginClient : IDisposable
    {
        private Socket? _sock;

        public bool IsConnected => _sock != null && _sock.Connected;

        public Task ConnectAndIdentifyAsync(string serverIp, int tcpPort, string username, CancellationToken ct = default)
            => Task.Run(() => ConnectAndIdentify(serverIp, tcpPort, username), ct);

        public void ConnectAndIdentify(string serverIp, int tcpPort, string username)
        {
            Dispose();

            var ip = IPAddress.Parse(serverIp);
            _sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            {
                ReceiveTimeout = 5000,
                SendTimeout = 5000
            };

            _sock.Connect(new IPEndPoint(ip, tcpPort));

            // identifikacija za zaposlenog (OSTAJE ISTO)
            var msg = ProtocolConstants.UdpLoginEmployeePrefix + username + "\n";
            _sock.Send(Encoding.UTF8.GetBytes(msg));

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

        public string SendAndReceive(string text, int bufferSize = 8192)
        {
            if (_sock == null) throw new Exception("TCP nije povezan.");

            _sock.Send(Encoding.UTF8.GetBytes(text));

            var buf = new byte[bufferSize];
            int n = _sock.Receive(buf);
            if (n == 0) throw new Exception("Server je zatvorio konekciju.");

            return Encoding.UTF8.GetString(buf, 0, n).Trim();
        }

        // ✅ JSON helper (ovo je poenta)
        public string SendJsonAndReceive(object payload, int bufferSize = 8192)
        {
            if (_sock == null) throw new Exception("TCP nije povezan.");

            string json = JsonSerializer.Serialize(payload);
            // newline (server/klijent često rade line-based)
            return SendAndReceive(json + "\n", bufferSize);
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
