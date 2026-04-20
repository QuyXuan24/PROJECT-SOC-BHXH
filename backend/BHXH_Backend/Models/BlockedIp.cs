using System.ComponentModel.DataAnnotations;

namespace BHXH_Backend.Models
{
    public class BlockedIp
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(64)]
        public string IpAddress { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? BlockedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ExpiresAt { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime? LastHitAt { get; set; }
    }
}
