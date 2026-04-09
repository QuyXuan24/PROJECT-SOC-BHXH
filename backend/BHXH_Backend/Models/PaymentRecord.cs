using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BHXH_Backend.Models
{
    public class PaymentRecord
    {
        [Key]
        public int Id { get; set; }

        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }

        public int? BhxhRecordId { get; set; }

        [ForeignKey("BhxhRecordId")]
        public BhxhRecord? BhxhRecord { get; set; }

        public required string BhxhCode { get; set; }
        public decimal Amount { get; set; }
        public string? Description { get; set; }
        public required string Currency { get; set; }
        public required string PaymentCode { get; set; }
        public required string QrPayload { get; set; }
        public string Status { get; set; } = "Pending";
        public string? ProcessedBy { get; set; }
        public string? ReviewNote { get; set; }
        public bool BlockchainSynced { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
