using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BHXH_Backend.Models
{
    [Table("otp_codes")]
    public class OtpCode
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("user_id")]
        public int? UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        [Required]
        [MaxLength(200)]
        [Column("email")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(6)]
        [Column("otp_code")]
        public string OtpValue { get; set; } = string.Empty;

        [Column("expire_time")]
        public DateTime ExpireTime { get; set; }

        [Column("is_used")]
        public bool IsUsed { get; set; }

        [Required]
        [MaxLength(20)]
        [Column("purpose")]
        public string Purpose { get; set; } = string.Empty;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
