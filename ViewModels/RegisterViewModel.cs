using System.ComponentModel.DataAnnotations;

namespace Kawin_67091861_002.ViewModels
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "กรุณากรอกชื่อ")]
        [StringLength(50, ErrorMessage = "ชื่อต้องไม่เกิน 50 ตัวอักษร")]
        [Display(Name = "ชื่อ")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "กรุณากรอกนามสกุล")]
        [StringLength(50, ErrorMessage = "นามสกุลต้องไม่เกิน 50 ตัวอักษร")]
        [Display(Name = "นามสกุล")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "กรุณากรอกอีเมล")]
        [EmailAddress(ErrorMessage = "รูปแบบอีเมลไม่ถูกต้อง")]
        [Display(Name = "อีเมล")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "เบอร์โทรศัพท์")]
        public string? Phone { get; set; }

        [Required(ErrorMessage = "กรุณากรอกรหัสผ่าน")]
        [MinLength(8, ErrorMessage = "รหัสผ่านต้องมีอย่างน้อย 8 ตัวอักษร")]
        [DataType(DataType.Password)]
        [Display(Name = "รหัสผ่าน")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "กรุณายืนยันรหัสผ่าน")]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "รหัสผ่านไม่ตรงกัน")]
        [Display(Name = "ยืนยันรหัสผ่าน")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Display(Name = "วันเกิด")]
        public string? BirthDate { get; set; }

        [Display(Name = "ยอมรับเงื่อนไขและข้อตกลง")]
        public bool AgreeTerms { get; set; }
    }
}
