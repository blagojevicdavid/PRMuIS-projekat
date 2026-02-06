using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Protocol
{
    public static class ProtocolConstants
    {
        public const string UdpLoginManagerPrefix = "MENADZER:";
        public const string UdpLoginEmployeePrefix = "ZAPOSLENI:";

        public const string UdpTcpInfoPrefix = "TCP:";

        public const string TcpSendPrefix = "SEND:";
        public const string TcpTakePrefix = "TAKE:";
        public const string TcpDonePrefix = "DONE:";

        public const string UdpAllTasksPrefix = "SVI:";
        public const string UdpTaskPrefix = "TASKS:";

        public const string UdpChangePriorityPrefix = "PRIORITY:";
        public const string UdpOk = "OK";
        public const string UdpErrPrefix = "ERR:";



    }
}
