using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CollaborativeServer.Core
{
    public sealed class ServerConfig
    {
        public string BindIP { get; init; } = "0.0.0.0";
        public int UdpPort { get; init; } = 50032;
        public int TcpPort { get; init; } = 50005;

        public int SelectTimeoutMs { get; init; } = 250;
    }
}
