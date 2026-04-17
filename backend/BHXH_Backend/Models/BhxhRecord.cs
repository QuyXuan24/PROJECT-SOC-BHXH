using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BHXH_Backend.Models
{
    public class BhxhRecord
    {
        [Key]
        public int Id { get; set; }

        public string? ProcessedBy { get; set; }
        public string? ReviewNote { get; set; }
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        public required string FullName { get; set; }
        public DateTime DateOfBirth { get; set; }
        public required string Gender { get; set; }

        public required string Cccd { get; set; }
        public required string PhoneNumber { get; set; }
        public required string Address { get; set; }
        public required string BhxhCode { get; set; }

        [MaxLength(64)]
        public string CccdHash { get; set; } = string.Empty;

        [MaxLength(64)]
        public string BhxhCodeHash { get; set; } = string.Empty;

        public string? CompanyName { get; set; }
        public decimal? Salary { get; set; }

        public string Status { get; set; } = "Pending";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
