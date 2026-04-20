using System.ComponentModel.DataAnnotations;

namespace BHXH_Backend.Dtos
{
    public class ProcessRecordDto
    {
        [Required(ErrorMessage = "Action là bắt buộc")]
        [RegularExpression("^(Approved|Rejected|Cancelled)$", ErrorMessage = "Hành động chỉ được là 'Approved', 'Rejected' hoặc 'Cancelled'")]
        public string Action { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Ghi chú tối đa 500 ký tự")]
        public string? Note { get; set; }
    }
}
