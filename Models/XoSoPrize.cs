using System.Collections.Generic;

namespace xsmbsocket.Models
{
    public class XoSoPrize
    {
        public string PrizeName { get; set; }

        public List<string> Numbers { get; set; }
            = new List<string>();
    }
}