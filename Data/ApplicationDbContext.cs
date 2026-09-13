using Microsoft.EntityFrameworkCore;

namespace AdminPortalApi.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<AdminPortal.Models.Entities.Employee> Employees { get; set; }
    }
}