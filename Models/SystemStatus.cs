using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading.Tasks;

namespace xsmbsocket.Models
{
  public class SystemStatus
{
    public int Connections { get; set; }

    public long Memory { get; set; }

    public DateTime Time { get; set; }

    public TimeSpan Uptime { get; set; }
}
}
