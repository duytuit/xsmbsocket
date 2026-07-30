using System;
using System.Net.WebSockets;

namespace xsmbsocket.Models
{
    public class ClientInfo
    {
        public Guid Id { get; set; }

        public string IpAddress { get; set; }

        public DateTime ConnectedAt { get; set; }

        public DateTime LastSeen { get; set; }

        public WebSocket Socket { get; set; }

        public long SentBytes { get; set; }

        public long ReceivedBytes { get; set; }
    }
}