using BHXH_Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace BHXH_Backend.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<BhxhRecord>()
                .Property(r => r.Salary)
                .HasPrecision(18, 2);

            modelBuilder.Entity<PaymentRecord>()
                .Property(p => p.Amount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<BlockedIp>()
                .HasIndex(b => new { b.IpAddress, b.IsActive });

            modelBuilder.Entity<Incident>()
                .HasIndex(i => i.IncidentCode)
                .IsUnique();

            modelBuilder.Entity<Incident>()
                .HasIndex(i => new { i.Status, i.Severity, i.CreatedAt });
        }

        public DbSet<SystemLog> SystemLogs { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<BhxhRecord> BhxhRecords { get; set; }
        public DbSet<PaymentRecord> PaymentRecords { get; set; }
        public DbSet<BlockedIp> BlockedIps { get; set; }
        public DbSet<Incident> Incidents { get; set; }
    }
}
