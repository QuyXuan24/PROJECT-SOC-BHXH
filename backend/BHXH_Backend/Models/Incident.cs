using System.ComponentModel.DataAnnotations;

namespace BHXH_Backend.Models
{
    public class Incident
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(40)]
        public string IncidentCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Severity { get; set; } = "Medium";

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "Open";

        [MaxLength(80)]
        public string? Category { get; set; }

        [MaxLength(64)]
        public string? SourceIp { get; set; }

        [MaxLength(100)]
        public string? Username { get; set; }

        public int? RelatedLogId { get; set; }

        [Required]
        [MaxLength(2000)]
        public string Description { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string CreatedBy { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public DateTime? ResolvedAt { get; set; }

        [MaxLength(1000)]
        public string? ResolutionNote { get; set; }
    }
}
