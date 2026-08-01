using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace xsmbsocket.Lotterys.Models
{
    [Table("lotterys")]
    public class Lottery
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("code", TypeName = "nvarchar(max)")]
        public string Code { get; set; }

        [Column("data", TypeName = "nvarchar(max)")]
        public string Data { get; set; }

        [Column("created_by")]
        public int? CreatedBy { get; set; }

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }

        [Column("updated_by")]
        public int? UpdatedBy { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [Column("deleted_by")]
        public int? DeletedBy { get; set; }

        [Column("deleted_at")]
        public DateTime? DeletedAt { get; set; }
    }
}
