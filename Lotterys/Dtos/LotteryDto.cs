
using xsmbsocket.Lotterys.Models;
using System;

namespace xsmbsocket.Lotterys.Dtos
{
    public class LotteryDto
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string Data { get; set; }
        public int? CreatedBy { get; set; }
        public int? UpdatedBy { get; set; }
        public int? DeletedBy { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
    }
}
