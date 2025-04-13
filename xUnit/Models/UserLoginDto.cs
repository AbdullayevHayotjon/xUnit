using System.ComponentModel.DataAnnotations;

public class UserLoginDto
{
    [Required(ErrorMessage = "Email majburiy")]
    [EmailAddress(ErrorMessage = "Email formati noto‘g‘ri")]
    public string Email { get; set; }

    [Required(ErrorMessage = "Parol majburiy")]
    [MinLength(6, ErrorMessage = "Parol kamida 6 ta belgidan iborat bo‘lishi kerak")]
    public string Password { get; set; }
}
