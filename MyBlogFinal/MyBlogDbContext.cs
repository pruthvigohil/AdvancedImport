using Microsoft.EntityFrameworkCore;
using AdvancedImport.Classes;

namespace AdvancedImport
{
    public class MyBlogDbContext : DbContext
    {

        public MyBlogDbContext(DbContextOptions<MyBlogDbContext> dbContextOptions) : base(dbContextOptions)
        {

        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<dynamic>().HasNoKey();
        }

        public virtual DbSet<SalesRecords> SalesRecords { get; set; }
    }
}
