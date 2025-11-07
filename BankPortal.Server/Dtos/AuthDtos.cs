using System.ComponentModel.DataAnnotations;

namespace BankPortal.Dtos;

// Keep server + client regex in sync 
public class RegisterDto
{
    [Required(ErrorMessage = "Full name is required")]
    [RegularExpression(@"^[\p{L} ,.'-]{2,60}$", ErrorMessage = "2–60 letters; may include spaces and , . ' -")]
    public string FullName { get; set; } = null!;

    [Required(ErrorMessage = "ID number is required")]
    [RegularExpression(@"^[0-9A-Za-z\-]{6,20}$", ErrorMessage = "6–20 characters; letters/digits/hyphen only")]
    public string IdNumber { get; set; } = null!;

    [Required(ErrorMessage = "Account number is required")]
    [RegularExpression(@"^\d{8,20}$", ErrorMessage = "Digits only, 8–20 long")]
    public string AccountNumber { get; set; } = null!;

    [Required(ErrorMessage = "Username is required")]
    [RegularExpression(@"^[a-zA-Z0-9_.-]{3,30}$", ErrorMessage = "3–30; letters/digits/_ . -")]
    public string Username { get; set; } = null!;

    [Required(ErrorMessage = "Password is required")]
    [StringLength(64, MinimumLength = 8, ErrorMessage = "Password must be 8–64 characters")]
    public string Password { get; set; } = null!;
}

public class LoginDto
{
    [Required(ErrorMessage = "Username is required")]
    [RegularExpression(@"^[a-zA-Z0-9_.-]{3,30}$", ErrorMessage = "3–30; letters/digits/_ . -")]
    public string Username { get; set; } = null!;

    [Required(ErrorMessage = "Account number is required")]
    [RegularExpression(@"^\d{8,20}$", ErrorMessage = "Digits only, 8–20 long")]
    public string AccountNumber { get; set; } = null!;

    [Required(ErrorMessage = "Password is required")]
    [StringLength(64, MinimumLength = 8, ErrorMessage = "Password must be 8–64 characters")]
    public string Password { get; set; } = null!;
}
