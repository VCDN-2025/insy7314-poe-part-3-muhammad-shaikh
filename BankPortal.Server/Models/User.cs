namespace BankPortal.Models;

public class User
{
    public int Id { get; set; }
    public string FullName { get; set; } = null!;
    public string IdNumber { get; set; } = null!;
    public string AccountNumber { get; set; } = null!;
    public string Username { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsEmployee { get; set; } = false;

}
