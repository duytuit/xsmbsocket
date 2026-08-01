

using Microsoft.EntityFrameworkCore;
using xsmbsocket.Lotterys.Models;

namespace xsmbsocket.Shares.BaseRepository
{
    public class XoSoDBContext : DbContext
    {
        public DbSet<Lottery> Lotteries { get; set; }
        public XoSoDBContext(DbContextOptions<XoSoDBContext> options) : base(options)
        {
            
        }

        protected override void OnModelCreating(ModelBuilder modelBuider)
        {
            modelBuider.Entity<Lottery>().HasQueryFilter(e => e.DeletedAt == null);
        }           
    }
}
