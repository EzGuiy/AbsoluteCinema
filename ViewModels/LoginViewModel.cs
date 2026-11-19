using System.ComponentModel.DataAnnotations;

namespace Kawin_67091861_002.ViewModels
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "กรุณากรอกอีเมลหรือชื่อผู้ใช้งาน")]
        [Display(Name = "อีเมลหรือชื่อผู้ใช้งาน")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "กรุณากรอกรหัสผ่าน")]
        [DataType(DataType.Password)]
        [Display(Name = "รหัสผ่าน")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "จดจำฉัน")]
        public bool RememberMe { get; set; }
    }
}
