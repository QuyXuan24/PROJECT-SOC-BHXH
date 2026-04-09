using System.ComponentModel.DataAnnotations;

namespace BHXH_Backend.Dtos
{
    public class PaymentRequestDto
    {
        [Required(ErrorMessage = "Mã số BHXH là bắt buộc")]
        [RegularExpression("^[0-9]{10}$", ErrorMessage = "Mã số BHXH phải đúng 10 chữ số.")]
        public string BhxhCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Số tiền là bắt buộc")]
        [Range(1, double.MaxValue, ErrorMessage = "Số tiền phải lớn hơn 0")]
        public decimal Amount { get; set; }

        public string Currency { get; set; } = "VND";

        [StringLength(500, ErrorMessage = "Nội dung không được quá 500 ký tự")]
        public string? Description { get; set; }
    }
}
