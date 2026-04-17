using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BHXH_Backend.Models
{
    [Table("bhyt")]
    public class BhytRecord
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        [Required]
        [MaxLength(40)]
        [Column("card_number")]
        public string CardNumber { get; set; } = string.Empty;

        [MaxLength(250)]
        [Column("registered_hospital")]
        public string? RegisteredHospital { get; set; }

        [Column("valid_from")]
        public DateTime? ValidFrom { get; set; }

        [Column("valid_to")]
        public DateTime? ValidTo { get; set; }

        [MaxLength(50)]
        [Column("benefit_rate")]
        public string? BenefitRate { get; set; }

        [MaxLength(50)]
        [Column("status")]
        public string Status { get; set; } = "Inactive";

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
