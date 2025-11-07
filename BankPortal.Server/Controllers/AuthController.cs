using BankPortal.Data;
using BankPortal.Dtos;
using BankPortal.Models;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BankPortal.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher<User> _hasher;
    private readonly IAntiforgery _antiforgery;
    private readonly ILogger<AuthController> _log;
    private readonly IWebHostEnvironment _env;

    public AuthController(
        AppDbContext db,
        IPasswordHasher<User> hasher,
        IAntiforgery antiforgery,
        ILogger<AuthController> log,
        IWebHostEnvironment env)
    {
        _db = db; _hasher = hasher; _antiforgery = antiforgery; _log = log; _env = env;
    }

    // Issues a readable CSRF token cookie for SPA; header validated server-side
    [HttpGet("csrf")]
    public IActionResult GetCsrf()
    {
        var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
        Response.Cookies.Append("XSRF-TOKEN", tokens.RequestToken!, new CookieOptions
        {
            HttpOnly = false,           // SPA must read it to send X-CSRF-TOKEN
            SameSite = SameSiteMode.Lax,
            Secure = true
        });
        return Ok(new { ok = true });
    }

    // Simple "who am I" using HttpOnly auth cookie
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        if (!Request.Cookies.TryGetValue("AUTH", out var uidStr)) return Unauthorized();
        if (!int.TryParse(uidStr, out var uid)) return Unauthorized();
        var user = await _db.Users.FindAsync(uid);
        if (user == null) return Unauthorized();

        // UPDATED: include isEmployee so SPA can show staff portal safely
        return Ok(new { id = user.Id, username = user.Username, isEmployee = user.IsEmployee });
    }

    // Registration: validate, enforce unique username, hash password, persist
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        if (await _db.Users.AnyAsync(u => u.Username == dto.Username))
            return Conflict(new { error = "Username already exists" });

        var user = new User
        {
            FullName = dto.FullName,
            IdNumber = dto.IdNumber,
            AccountNumber = dto.AccountNumber,
            Username = dto.Username,
            // PBKDF2 with per-user salt via PasswordHasher<T>
            PasswordHash = _hasher.HashPassword(new User(), dto.Password)
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return Created(string.Empty, new { ok = true });
    }

    // Login: CSRF required; verify username+account, then check password hash
    [ValidateAntiForgeryToken]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        try
        {
            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.Username == dto.Username && u.AccountNumber == dto.AccountNumber);

            if (user == null)
                return Unauthorized(new { error = "Invalid credentials" });

            var result = _hasher.VerifyHashedPassword(
                user,
                user.PasswordHash ?? string.Empty,
                dto.Password ?? string.Empty);

            if (result == PasswordVerificationResult.Failed)
                return Unauthorized(new { error = "Invalid credentials" });

            // Issue HttpOnly+Secure cookie (not accessible to JS)
            Response.Cookies.Append("AUTH", user.Id.ToString(), new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Path = "/"
            });

            // UPDATED: include isEmployee in the response
            return Ok(new
            {
                ok = true,
                user = new
                {
                    user.Id,
                    user.Username,
                    isEmployee = user.IsEmployee
                }
            });
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Login failed for username {Username}", dto.Username);
            var msg = _env.IsDevelopment() ? ex.Message : "Server error during login";
            return StatusCode(500, new { error = msg });
        }
    }

    // Logout: clear the auth cookie
    [ValidateAntiForgeryToken]
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete("AUTH", new CookieOptions { Path = "/" });
        return Ok(new { ok = true });
    }
}

/* -------------------------------------------------------------------------
   Bibliography
   ------------------------------------------------------------------------- 

Microsoft (2024) ‘Antiforgery in ASP.NET Core’. Available at: https://learn.microsoft.com/aspnet/core/security/anti-request-forgery (Accessed: 10 October 2025).

Microsoft (2024) ‘Enforce HTTPS in ASP.NET Core’. Available at: https://learn.microsoft.com/aspnet/core/security/enforcing-ssl (Accessed: 10 October 2025).

Microsoft (2024) ‘Rate limiting in ASP.NET Core’. Available at: https://learn.microsoft.com/aspnet/core/performance/rate-limit (Accessed: 10 October 2025).

OWASP (2024) ‘Cheat Sheet Series: Cross-Site Request Forgery (CSRF) Prevention’. Available at: https://cheatsheetseries.owasp.org/ (Accessed: 10 October 2025).

OWASP (2024) ‘Cheat Sheet Series: Content Security Policy’. Available at: https://cheatsheetseries.owasp.org/ (Accessed: 10 October 2025).

OWASP (2024) ‘Cheat Sheet Series: SQL Injection Prevention’. Available at: https://cheatsheetseries.owasp.org/ (Accessed: 10 October 2025).

OWASP (2024) ‘Cheat Sheet Series: Clickjacking Defense’. Available at: https://cheatsheetseries.owasp.org/ (Accessed: 10 October 2025).

OWASP (2024) ‘Cheat Sheet Series: Password Storage’. Available at: https://cheatsheetseries.owasp.org/ (Accessed: 10 October 2025).

*/

