using Microsoft.EntityFrameworkCore;
using BHXH_Backend.Models;

namespace BHXH_Backend.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }
        public DbSet<SystemLog> SystemLogs { get; set; }
        public DbSet<User> Users { get; set; } // Tạo bảng Users trong SQL
    }
}