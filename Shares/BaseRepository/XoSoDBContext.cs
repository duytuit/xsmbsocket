

using Microsoft.EntityFrameworkCore;

namespace xsmbsocket.Shares.BaseRepository
{
    public class XoSoDBContext : DbContext
    {

        public XoSoDBContext(DbContextOptions<XoSoDBContext> options) : base(options)
        {

        }

        protected override void OnModelCreating(ModelBuilder modelBuider)
        {
           // modelBuider.Entity<User>().HasQueryFilter(e => e.DeletedAt == null);
           
        }           
    }
}
