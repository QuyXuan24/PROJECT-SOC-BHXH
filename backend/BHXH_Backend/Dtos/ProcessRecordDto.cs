using System.ComponentModel.DataAnnotations;

namespace BHXH_Backend.Dtos
{
    public class ProcessRecordDto
    {
        [Required(ErrorMessage = "Action là bắt buộc")]
        // Chỉ cho phép "Approved" hoặc "Rejected" để tránh dữ liệu rác
        [RegularExpression("^(Approved|Rejected)$", ErrorMessage = "Hành động chỉ được là 'Approved' hoặc 'Rejected'")]
        public string Action { get; set; } = string.Empty;
    }
}