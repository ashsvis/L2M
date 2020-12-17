using System;
using System.Collections.Generic;
using System.Net;

namespace L2M
{
    public class TcpTuning
    {
        public Guid ChannelId { get; set; }
        public IPAddress Address { get; set; } = new IPAddress(new byte[] { 127, 0, 0, 1 });
        public int Port { get; set; } = 502;
        public int SendTimeout { get; set; } = 5000;
        public int ReceiveTimeout { get; set; } = 5000;
        public int WaitForConnect { get; set; } = 50;
        public int WaitForAnswer { get; set; } = 200;
        public IEnumerable<RequestData> Parameters { get; set; } = new List<RequestData>();
    }
}
