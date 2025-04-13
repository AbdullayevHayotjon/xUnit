using System.ComponentModel.DataAnnotations;

namespace xUnit.Models
{
    public class UserRegisterDto
    {
        [Required(ErrorMessage = "Ism majburiy")]
        [MaxLength(50)]
        public string FirstName { get; set; }

        [Required(ErrorMessage = "Familiya majburiy")]
        [MaxLength(50)]
        public string LastName { get; set; }

        [Required(ErrorMessage = "Email majburiy")]
        [EmailAddress(ErrorMessage = "Email formati noto‘g‘ri")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Parol majburiy")]
        [MinLength(6, ErrorMessage = "Parol kamida 6 ta belgidan iborat bo‘lishi kerak")]
        public string Password { get; set; }
    }

}
